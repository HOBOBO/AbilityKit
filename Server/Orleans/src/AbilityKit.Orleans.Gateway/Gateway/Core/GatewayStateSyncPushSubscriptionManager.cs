using System.Collections.Concurrent;
using System.Threading.Channels;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Gateway.Abstractions;
using Microsoft.Extensions.Logging;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Core;

public sealed class GatewayStateSyncPushSubscriptionManager
{
    private readonly IClusterClient _clusterClient;
    private readonly IGatewaySessionRegistry _sessionRegistry;
    private readonly GatewayBackgroundTaskQueue _backgroundTasks;
    private readonly ILogger<GatewayStateSyncPushSubscriptionManager> _logger;
    private readonly ConcurrentDictionary<long, Subscription> _subscriptions = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public GatewayStateSyncPushSubscriptionManager(
        IClusterClient clusterClient,
        IGatewaySessionRegistry sessionRegistry,
        GatewayBackgroundTaskQueue backgroundTasks,
        ILogger<GatewayStateSyncPushSubscriptionManager> logger)
    {
        _clusterClient = clusterClient;
        _sessionRegistry = sessionRegistry;
        _backgroundTasks = backgroundTasks;
        _logger = logger;
    }

    public async Task EnsureBoundAsync(long connectionId, string observerKey)
    {
        if (connectionId <= 0) throw new ArgumentOutOfRangeException(nameof(connectionId));
        if (string.IsNullOrWhiteSpace(observerKey)) throw new ArgumentException("observerKey is required.", nameof(observerKey));

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_subscriptions.TryGetValue(connectionId, out var current))
            {
                if (string.Equals(current.ObserverKey, observerKey, StringComparison.Ordinal))
                {
                    return;
                }

                await RemoveSubscriptionAsync(connectionId, current).ConfigureAwait(false);
            }

            var observerGrain = _clusterClient.GetGrain<IStateSyncObserverGrain>(observerKey);
            var bindingId = Guid.NewGuid().ToString("N");
            var pushPump = new ConnectionStateSyncPushPump(
                connectionId,
                _sessionRegistry,
                _logger);
            var observer = new ConnectionStateSyncPushObserver(pushPump);
            var observerReference = _clusterClient.CreateObjectReference<IStateSyncGatewayPushObserver>(observer);
            try
            {
                await observerGrain.BindGatewayPushObserverAsync(bindingId, observerReference).ConfigureAwait(false);
                _subscriptions[connectionId] = new Subscription(
                    observerKey,
                    bindingId,
                    observerGrain,
                    observer,
                    observerReference,
                    pushPump);
            }
            catch
            {
                await pushPump.DisposeAsync().ConfigureAwait(false);
                _clusterClient.DeleteObjectReference<IStateSyncGatewayPushObserver>(observerReference);
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
            await subscription.ObserverGrain
                .UnbindGatewayPushObserverAsync(subscription.BindingId)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to unbind state sync push observer. ConnectionId={ConnectionId} ObserverKey={ObserverKey}",
                connectionId,
                subscription.ObserverKey);
        }
        finally
        {
            _clusterClient.DeleteObjectReference<IStateSyncGatewayPushObserver>(subscription.ObserverReference);
            await subscription.PushPump.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed record Subscription(
        string ObserverKey,
        string BindingId,
        IStateSyncObserverGrain ObserverGrain,
        ConnectionStateSyncPushObserver Observer,
        IStateSyncGatewayPushObserver ObserverReference,
        ConnectionStateSyncPushPump PushPump);

    private sealed class ConnectionStateSyncPushObserver : IStateSyncGatewayPushObserver
    {
        private readonly ConnectionStateSyncPushPump _pushPump;

        public ConnectionStateSyncPushObserver(ConnectionStateSyncPushPump pushPump)
        {
            _pushPump = pushPump;
        }

        public void OnPush(uint opCode, byte[] payload)
        {
            _pushPump.TryEnqueue(opCode, payload);
        }
    }

    private sealed class ConnectionStateSyncPushPump : IAsyncDisposable
    {
        private readonly long _connectionId;
        private readonly IGatewaySessionRegistry _sessionRegistry;
        private readonly ILogger _logger;
        private readonly Channel<PushItem> _queue;
        private readonly CancellationTokenSource _lifetime = new();
        private readonly Task _worker;

        public ConnectionStateSyncPushPump(
            long connectionId,
            IGatewaySessionRegistry sessionRegistry,
            ILogger logger)
        {
            _connectionId = connectionId;
            _sessionRegistry = sessionRegistry;
            _logger = logger;
            _queue = Channel.CreateUnbounded<PushItem>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
            _worker = Task.Run(ProcessAsync);
        }

        public bool TryEnqueue(uint opCode, byte[] payload)
        {
            return payload != null && _queue.Writer.TryWrite(new PushItem(opCode, payload));
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
            await foreach (var item in _queue.Reader.ReadAllAsync(_lifetime.Token))
            {
                if (!_sessionRegistry.TryGetSession(_connectionId, out var session)
                    || session == null
                    || !session.IsConnected)
                {
                    continue;
                }

                try
                {
                    await session.SendServerPushAsync(
                        item.OpCode,
                        item.Payload,
                        _lifetime.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Failed to send state sync push. ConnectionId={ConnectionId} OpCode={OpCode}",
                        _connectionId,
                        item.OpCode);
                }
            }
        }

        private readonly record struct PushItem(uint OpCode, byte[] Payload);
    }
}
