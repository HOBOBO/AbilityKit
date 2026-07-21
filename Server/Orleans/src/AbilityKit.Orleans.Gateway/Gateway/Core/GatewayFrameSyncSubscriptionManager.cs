using System.Collections.Concurrent;
using AbilityKit.Orleans.Contracts.FrameSync;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;
using Microsoft.Extensions.Logging;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Core;

public sealed class GatewayFrameSyncSubscriptionManager
{
    private readonly IClusterClient _clusterClient;
    private readonly IGatewaySessionRegistry _sessionRegistry;
    private readonly GatewayBackgroundTaskQueue _backgroundTasks;
    private readonly ILogger<GatewayFrameSyncSubscriptionManager> _logger;
    private readonly ConcurrentDictionary<long, Subscription> _subscriptions = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public GatewayFrameSyncSubscriptionManager(
        IClusterClient clusterClient,
        IGatewaySessionRegistry sessionRegistry,
        GatewayBackgroundTaskQueue backgroundTasks,
        ILogger<GatewayFrameSyncSubscriptionManager> logger)
    {
        _clusterClient = clusterClient;
        _sessionRegistry = sessionRegistry;
        _backgroundTasks = backgroundTasks;
        _logger = logger;
    }

    public async Task EnsureSubscribedAsync(long connectionId, ulong roomId)
    {
        if (connectionId <= 0) throw new ArgumentOutOfRangeException(nameof(connectionId));
        if (roomId == 0) throw new ArgumentOutOfRangeException(nameof(roomId));

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_subscriptions.TryGetValue(connectionId, out var current))
            {
                if (current.RoomId == roomId)
                {
                    return;
                }

                await RemoveSubscriptionAsync(connectionId, current).ConfigureAwait(false);
            }

            var observer = new ConnectionFrameSyncObserver(connectionId, this);
            var observerReference = _clusterClient.CreateObjectReference<IFrameSyncObserver>(observer);
            var grain = _clusterClient.GetGrain<IBattleFrameSyncGrain>(roomId.ToString());
            try
            {
                await grain.SubscribeAsync(observerReference).ConfigureAwait(false);
                _subscriptions[connectionId] = new Subscription(
                    roomId,
                    grain,
                    observer,
                    observerReference);
            }
            catch
            {
                _clusterClient.DeleteObjectReference<IFrameSyncObserver>(observerReference);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void OnConnectionClosed(long connectionId)
    {
        _backgroundTasks.TryQueue(async _ =>
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_subscriptions.TryGetValue(connectionId, out var subscription))
                {
                    await RemoveSubscriptionAsync(connectionId, subscription).ConfigureAwait(false);
                }
            }
            finally
            {
                _gate.Release();
            }
        });
    }

    private void OnFramePushed(long connectionId, FramePushedEvent evt)
    {
        var payload = SerializeFrame(evt);
        _backgroundTasks.TryQueue(async cancellationToken =>
        {
            if (!_sessionRegistry.TryGetSession(connectionId, out var session)
                || session == null
                || !session.IsConnected)
            {
                return;
            }

            if (evt.Inputs is { Count: > 0 })
            {
                _logger.LogInformation(
                    "Sending authoritative input frame. ConnectionId={ConnectionId} RoomId={RoomId} WorldId={WorldId} Frame={Frame} InputCount={InputCount} PayloadBytes={PayloadBytes}",
                    connectionId,
                    evt.RoomId,
                    evt.WorldId,
                    evt.Frame,
                    evt.Inputs.Count,
                    payload.Length);
            }

            await session.SendServerPushAsync(OpCodes.FramePushed, payload, cancellationToken).ConfigureAwait(false);
        });
    }

    private async Task RemoveSubscriptionAsync(long connectionId, Subscription subscription)
    {
        _subscriptions.TryRemove(new KeyValuePair<long, Subscription>(connectionId, subscription));
        try
        {
            await subscription.Grain.UnsubscribeAsync(subscription.ObserverReference).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to unsubscribe frame sync observer. ConnectionId={ConnectionId} RoomId={RoomId}",
                connectionId,
                subscription.RoomId);
        }
        finally
        {
            _clusterClient.DeleteObjectReference<IFrameSyncObserver>(subscription.ObserverReference);
        }
    }

    internal static byte[] SerializeFrame(FramePushedEvent evt)
    {
        var source = evt.Inputs;
        var inputs = new WireInputItem[source?.Count ?? 0];
        for (var index = 0; index < inputs.Length; index++)
        {
            var input = source![index];
            inputs[index] = new WireInputItem(input.PlayerId, input.OpCode, input.Payload ?? Array.Empty<byte>());
        }

        var push = new WireFramePushedPush(evt.RoomId, evt.WorldId, evt.Frame, inputs);
        return WireCustomBinary.Serialize(in push).ToArray();
    }

    private sealed record Subscription(
        ulong RoomId,
        IBattleFrameSyncGrain Grain,
        ConnectionFrameSyncObserver Observer,
        IFrameSyncObserver ObserverReference);

    private sealed class ConnectionFrameSyncObserver : IFrameSyncObserver
    {
        private readonly long _connectionId;
        private readonly GatewayFrameSyncSubscriptionManager _owner;

        public ConnectionFrameSyncObserver(
            long connectionId,
            GatewayFrameSyncSubscriptionManager owner)
        {
            _connectionId = connectionId;
            _owner = owner;
        }

        public void OnFramePushed(FramePushedEvent evt)
        {
            _owner.OnFramePushed(_connectionId, evt);
        }
    }
}
