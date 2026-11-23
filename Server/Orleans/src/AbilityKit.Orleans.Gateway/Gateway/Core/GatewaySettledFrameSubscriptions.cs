using System.Collections.Concurrent;
using System.Threading.Channels;
using AbilityKit.Orleans.Contracts.FrameSync;
using AbilityKit.Protocol.Moba.SettledFrames;
using MemoryPack;
using Microsoft.Extensions.Logging;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Core;

public sealed partial class GatewayFrameSyncSubscriptionManager
{
    private readonly ConcurrentDictionary<long, SettledSubscription> _settledSubscriptions = new();

    internal async Task<bool> SubscribeSettledAsync(long connectionId, string accountId, FrameSyncSettledBinding binding,
        bool spectator, IBattleFrameSyncGrain grain)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_sessionRegistry.TryGetSession(connectionId, out var session) || session?.IsConnected != true)
                return false;
            if (_settledSubscriptions.TryGetValue(connectionId, out var current) &&
                current.Matches(accountId, binding) && current.Spectator == spectator)
                return true;

            if (_settledSubscriptions.TryRemove(connectionId, out var previous))
                await RemoveSettledSubscriptionAsync(previous).ConfigureAwait(false);
            if (_subscriptions.TryGetValue(connectionId, out var legacy))
                await RemoveSubscriptionAsync(connectionId, legacy).ConfigureAwait(false);

            var observer = new SettledPushObserver(connectionId, binding, this);
            var reference = _clusterClient.CreateObjectReference<ISettledFrameSyncObserver>(observer);
            var subscribed = false;
            try
            {
                subscribed = await grain.SubscribeSettledAsync(binding.RuntimeGeneration, reference).ConfigureAwait(false);
                if (subscribed)
                    _settledSubscriptions[connectionId] = new SettledSubscription(accountId, binding, spectator, grain, observer, reference);
                return subscribed;
            }
            finally
            {
                if (!subscribed)
                {
                    _clusterClient.DeleteObjectReference<ISettledFrameSyncObserver>(reference);
                    await observer.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    internal bool HasSettledBinding(GatewaySessionContext context, FrameSyncSettledBinding binding, bool requiresPlayer) =>
        _settledSubscriptions.TryGetValue(context.ConnectionId, out var current) &&
        current.Matches(context.AccountId, binding) && (!requiresPlayer || !current.Spectator);

    private async Task RemoveSettledSubscriptionAsync(SettledSubscription subscription)
    {
        try
        {
            await subscription.Grain.UnsubscribeSettledAsync(subscription.Reference).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to unsubscribe settled frames for room {RoomId}", subscription.Binding.RoomId);
        }
        finally
        {
            _clusterClient.DeleteObjectReference<ISettledFrameSyncObserver>(subscription.Reference);
            await subscription.Observer.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed record SettledSubscription(string AccountId, FrameSyncSettledBinding Binding, bool Spectator,
        IBattleFrameSyncGrain Grain, SettledPushObserver Observer, ISettledFrameSyncObserver Reference)
    {
        public bool Matches(string? accountId, FrameSyncSettledBinding binding) => AccountId == accountId &&
            Binding.RoomId == binding.RoomId && Binding.WorldId == binding.WorldId && Binding.BattleId == binding.BattleId &&
            Binding.RuntimeGeneration == binding.RuntimeGeneration;
    }

    private sealed class SettledPushObserver : ISettledFrameSyncObserver, IAsyncDisposable
    {
        private readonly Channel<FrameSyncSettledFrame> _queue = Channel.CreateBounded<FrameSyncSettledFrame>(
            new BoundedChannelOptions(128) { SingleReader = true, FullMode = BoundedChannelFullMode.DropOldest });
        private readonly CancellationTokenSource _lifetime = new();
        private readonly FrameSyncSettledBinding _binding;
        private readonly Task _worker;

        public SettledPushObserver(long connectionId, FrameSyncSettledBinding binding, GatewayFrameSyncSubscriptionManager owner)
        {
            _binding = binding;
            _worker = Task.Run(async () =>
            {
                await foreach (var frame in _queue.Reader.ReadAllAsync(_lifetime.Token).ConfigureAwait(false))
                {
                    if (!owner._sessionRegistry.TryGetSession(connectionId, out var session) || session?.IsConnected != true)
                        continue;
                    try
                    {
                        var payload = MemoryPackSerializer.Serialize(GatewaySettledFrameWire.ToWire(frame));
                        if (payload.Length <= SettledFrameProtocol.MaxPayloadBytes)
                            await session.SendServerPushAsync(SettledFrameProtocol.FramePushed, payload, _lifetime.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
                    catch (Exception exception)
                    {
                        owner._logger.LogWarning(exception, "Failed to send settled frame {Frame} to {ConnectionId}",
                            frame.Settlement.Frame, connectionId);
                    }
                }
            });
        }

        public void OnSettledFramePushed(FrameSyncSettledFrame frame)
        {
            if (frame.RoomId == _binding.RoomId && frame.WorldId == _binding.WorldId && frame.BattleId == _binding.BattleId &&
                frame.Settlement.RuntimeGeneration == _binding.RuntimeGeneration)
                _queue.Writer.TryWrite(frame);
        }

        public async ValueTask DisposeAsync()
        {
            _queue.Writer.TryComplete();
            _lifetime.Cancel();
            try { await _worker.ConfigureAwait(false); }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
            finally { _lifetime.Dispose(); }
        }
    }
}

internal static class GatewaySettledFrameWire
{
    public static WireSettledIdentity Identity(ulong roomId, ulong worldId, string battleId, Guid generation) => new()
    {
        Version = SettledFrameProtocol.Version, RoomId = roomId, WorldId = worldId, BattleId = battleId,
        RuntimeGeneration = generation.ToString("N")
    };

    public static WireSettledFrame ToWire(FrameSyncSettledFrame frame) => new()
    {
        Identity = Identity(frame.RoomId, frame.WorldId, frame.BattleId, frame.Settlement.RuntimeGeneration),
        Frame = frame.Settlement.Frame,
        InitialCheckpoint = frame.Settlement.IsInitialCheckpoint,
        Commands = frame.Settlement.Commands.Select(command => new WireSettledCommand
        {
            Ordinal = command.Ordinal, Disposition = (byte)command.Disposition, PlayerId = command.PlayerId,
            OpCode = command.OpCode, ExecutedPayload = command.ExecutedPayload.ToArray()
        }).ToArray(),
        Decisions = frame.Settlement.Decisions.Select(decision => new WireSettledDecision
        {
            Ordinal = decision.Ordinal, ActorId = decision.ActorId, EntityVersion = decision.EntityVersion,
            PredictionKey = decision.PredictionKey, SkillId = decision.SkillId, Accepted = decision.Accepted,
            AuthoritativeFrame = decision.AuthoritativeFrame, Reason = decision.Reason, InterruptEpoch = decision.InterruptEpoch,
            AuthoritySequence = decision.AuthoritySequence
        }).ToArray()
    };
}
