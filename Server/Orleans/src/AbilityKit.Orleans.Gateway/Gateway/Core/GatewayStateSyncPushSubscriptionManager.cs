using System.Collections.Concurrent;
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
            var observer = new ConnectionStateSyncPushObserver(connectionId, this);
            var observerReference = _clusterClient.CreateObjectReference<IStateSyncGatewayPushObserver>(observer);
            try
            {
                await observerGrain.BindGatewayPushObserverAsync(bindingId, observerReference).ConfigureAwait(false);
                _subscriptions[connectionId] = new Subscription(
                    observerKey,
                    bindingId,
                    observerGrain,
                    observer,
                    observerReference);
            }
            catch
            {
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

    private void OnPush(long connectionId, uint opCode, byte[] payload)
    {
        _backgroundTasks.TryQueue(async cancellationToken =>
        {
            if (!_sessionRegistry.TryGetSession(connectionId, out var session)
                || session == null
                || !session.IsConnected)
            {
                return;
            }

            await session.SendServerPushAsync(opCode, payload, cancellationToken).ConfigureAwait(false);
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
        }
    }

    private sealed record Subscription(
        string ObserverKey,
        string BindingId,
        IStateSyncObserverGrain ObserverGrain,
        ConnectionStateSyncPushObserver Observer,
        IStateSyncGatewayPushObserver ObserverReference);

    private sealed class ConnectionStateSyncPushObserver : IStateSyncGatewayPushObserver
    {
        private readonly long _connectionId;
        private readonly GatewayStateSyncPushSubscriptionManager _owner;

        public ConnectionStateSyncPushObserver(
            long connectionId,
            GatewayStateSyncPushSubscriptionManager owner)
        {
            _connectionId = connectionId;
            _owner = owner;
        }

        public void OnPush(uint opCode, byte[] payload)
        {
            _owner.OnPush(_connectionId, opCode, payload);
        }
    }
}
