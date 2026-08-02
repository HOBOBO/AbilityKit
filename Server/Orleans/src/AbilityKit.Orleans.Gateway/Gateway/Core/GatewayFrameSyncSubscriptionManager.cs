using System.Collections.Concurrent;
using System.Threading.Channels;
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

            var pushPump = new ConnectionFramePushPump(
                connectionId,
                _sessionRegistry,
                _logger);
            var observer = new ConnectionFrameSyncObserver(pushPump);
            var observerReference = _clusterClient.CreateObjectReference<IFrameSyncObserver>(observer);
            var grain = _clusterClient.GetGrain<IBattleFrameSyncGrain>(roomId.ToString());
            try
            {
                await grain.SubscribeAsync(observerReference).ConfigureAwait(false);
                _subscriptions[connectionId] = new Subscription(
                    roomId,
                    grain,
                    observer,
                    observerReference,
                    pushPump);
            }
            catch
            {
                await pushPump.DisposeAsync().ConfigureAwait(false);
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
            await subscription.PushPump.DisposeAsync().ConfigureAwait(false);
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
        IFrameSyncObserver ObserverReference,
        ConnectionFramePushPump PushPump);

    private sealed class ConnectionFrameSyncObserver : IFrameSyncObserver
    {
        private readonly ConnectionFramePushPump _pushPump;

        public ConnectionFrameSyncObserver(ConnectionFramePushPump pushPump)
        {
            _pushPump = pushPump;
        }

        public void OnFramePushed(FramePushedEvent evt)
        {
            _pushPump.TryEnqueue(evt);
        }
    }

    private sealed class ConnectionFramePushPump : IAsyncDisposable
    {
        private const int ReorderWindowFrames = 8;

        private readonly long _connectionId;
        private readonly IGatewaySessionRegistry _sessionRegistry;
        private readonly ILogger _logger;
        private readonly Channel<FramePushedEvent> _queue;
        private readonly CancellationTokenSource _lifetime = new();
        private readonly Task _worker;

        public ConnectionFramePushPump(
            long connectionId,
            IGatewaySessionRegistry sessionRegistry,
            ILogger logger)
        {
            _connectionId = connectionId;
            _sessionRegistry = sessionRegistry;
            _logger = logger;
            _queue = Channel.CreateUnbounded<FramePushedEvent>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
            _worker = Task.Run(ProcessAsync);
        }

        public bool TryEnqueue(FramePushedEvent evt)
        {
            return evt != null && _queue.Writer.TryWrite(evt);
        }

        public async ValueTask DisposeAsync()
        {
            _queue.Writer.TryComplete();
            _lifetime.Cancel();
            try
            {
                await _worker.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _lifetime.Dispose();
            }
        }

        private async Task ProcessAsync()
        {
            var pending = new SortedDictionary<int, FramePushedEvent>();
            var lastSentFrame = -1;
            await foreach (var evt in _queue.Reader.ReadAllAsync(_lifetime.Token))
            {
                if (evt.Frame <= lastSentFrame) continue;
                pending[evt.Frame] = evt;

                while (pending.Count > 1)
                {
                    var first = pending.First();
                    if (lastSentFrame >= 0 &&
                        first.Key > lastSentFrame + 1 &&
                        pending.Count <= ReorderWindowFrames)
                    {
                        break;
                    }

                    pending.Remove(first.Key);
                    await SendAsync(first.Value, _lifetime.Token).ConfigureAwait(false);
                    lastSentFrame = first.Key;
                }
            }
        }

        private async Task SendAsync(FramePushedEvent evt, CancellationToken cancellationToken)
        {
            if (!_sessionRegistry.TryGetSession(_connectionId, out var session)
                || session == null
                || !session.IsConnected)
            {
                return;
            }

            var payload = SerializeFrame(evt);
            if (evt.Inputs is { Count: > 0 })
            {
                _logger.LogInformation(
                    "Sending authoritative input frame. ConnectionId={ConnectionId} RoomId={RoomId} WorldId={WorldId} Frame={Frame} InputCount={InputCount} PayloadBytes={PayloadBytes}",
                    _connectionId,
                    evt.RoomId,
                    evt.WorldId,
                    evt.Frame,
                    evt.Inputs.Count,
                    payload.Length);
            }

            try
            {
                await session.SendServerPushAsync(OpCodes.FramePushed, payload, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Failed to send frame sync push. ConnectionId={ConnectionId} Frame={Frame}",
                    _connectionId,
                    evt.Frame);
            }
        }
    }
}
