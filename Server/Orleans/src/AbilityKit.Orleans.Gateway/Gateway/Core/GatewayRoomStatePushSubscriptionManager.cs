using System.Collections.Concurrent;
using System.Threading.Channels;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway.Abstractions;
using Microsoft.Extensions.Logging;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Core;

/// <summary>
/// Binds a lobby connection to its RoomGrain through an Orleans client observer.
/// Room snapshots are coalesced because every push contains the complete authoritative state.
/// </summary>
public sealed class GatewayRoomStatePushSubscriptionManager
{
    private readonly IClusterClient _clusterClient;
    private readonly IGatewaySessionRegistry _sessionRegistry;
    private readonly GatewayBackgroundTaskQueue _backgroundTasks;
    private readonly ILogger<GatewayRoomStatePushSubscriptionManager> _logger;
    private readonly ConcurrentDictionary<long, Subscription> _subscriptions = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public GatewayRoomStatePushSubscriptionManager(
        IClusterClient clusterClient,
        IGatewaySessionRegistry sessionRegistry,
        GatewayBackgroundTaskQueue backgroundTasks,
        ILogger<GatewayRoomStatePushSubscriptionManager> logger)
    {
        _clusterClient = clusterClient;
        _sessionRegistry = sessionRegistry;
        _backgroundTasks = backgroundTasks;
        _logger = logger;
    }

    public async Task EnsureBoundAsync(long connectionId, string roomId, string accountId)
    {
        if (connectionId <= 0) throw new ArgumentOutOfRangeException(nameof(connectionId));
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_subscriptions.TryGetValue(connectionId, out var current))
            {
                if (string.Equals(current.RoomId, roomId, StringComparison.Ordinal) &&
                    string.Equals(current.AccountId, accountId, StringComparison.Ordinal))
                {
                    return;
                }

                await RemoveSubscriptionAsync(connectionId, current).ConfigureAwait(false);
            }

            var room = _clusterClient.GetGrain<IRoomGrain>(roomId);
            var bindingId = Guid.NewGuid().ToString("N");
            var pump = new ConnectionRoomStatePushPump(
                connectionId,
                _sessionRegistry,
                _logger);
            var observer = new ConnectionRoomStatePushObserver(pump);
            var observerReference =
                _clusterClient.CreateObjectReference<IRoomStateGatewayPushObserver>(observer);
            try
            {
                await room.BindStatePushObserverAsync(
                    accountId,
                    bindingId,
                    observerReference).ConfigureAwait(false);
                _subscriptions[connectionId] = new Subscription(
                    roomId,
                    accountId,
                    bindingId,
                    room,
                    observerReference,
                    pump);
            }
            catch
            {
                await pump.DisposeAsync().ConfigureAwait(false);
                _clusterClient.DeleteObjectReference<IRoomStateGatewayPushObserver>(observerReference);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task UnbindAsync(long connectionId)
    {
        return RemoveForConnectionAsync(connectionId);
    }

    public void OnConnectionClosed(long connectionId)
    {
        _backgroundTasks.TryQueue(async _ =>
        {
            await RemoveForConnectionAsync(connectionId).ConfigureAwait(false);
        });
    }

    private async Task RemoveForConnectionAsync(long connectionId)
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
    }

    private async Task RemoveSubscriptionAsync(long connectionId, Subscription subscription)
    {
        _subscriptions.TryRemove(new KeyValuePair<long, Subscription>(connectionId, subscription));
        try
        {
            await subscription.Room.UnbindStatePushObserverAsync(
                subscription.AccountId,
                subscription.BindingId).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to unbind room state push observer. ConnectionId={ConnectionId} RoomId={RoomId}",
                connectionId,
                subscription.RoomId);
        }
        finally
        {
            _clusterClient.DeleteObjectReference<IRoomStateGatewayPushObserver>(
                subscription.ObserverReference);
            await subscription.Pump.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed record Subscription(
        string RoomId,
        string AccountId,
        string BindingId,
        IRoomGrain Room,
        IRoomStateGatewayPushObserver ObserverReference,
        ConnectionRoomStatePushPump Pump);

    private sealed class ConnectionRoomStatePushObserver : IRoomStateGatewayPushObserver
    {
        private readonly ConnectionRoomStatePushPump _pump;

        public ConnectionRoomStatePushObserver(ConnectionRoomStatePushPump pump)
        {
            _pump = pump;
        }

        public void OnPush(uint opCode, byte[] payload)
        {
            _pump.TryEnqueue(opCode, payload);
        }
    }

    private sealed class ConnectionRoomStatePushPump : IAsyncDisposable
    {
        private readonly long _connectionId;
        private readonly IGatewaySessionRegistry _sessionRegistry;
        private readonly ILogger _logger;
        private readonly Channel<PushItem> _queue;
        private readonly CancellationTokenSource _lifetime = new();
        private readonly Task _worker;

        public ConnectionRoomStatePushPump(
            long connectionId,
            IGatewaySessionRegistry sessionRegistry,
            ILogger logger)
        {
            _connectionId = connectionId;
            _sessionRegistry = sessionRegistry;
            _logger = logger;
            _queue = Channel.CreateBounded<PushItem>(new BoundedChannelOptions(64)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
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
                if (!_sessionRegistry.TryGetSession(_connectionId, out var session) ||
                    session == null ||
                    !session.IsConnected)
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
                        "Failed to send room state push. ConnectionId={ConnectionId} OpCode={OpCode}",
                        _connectionId,
                        item.OpCode);
                }
            }
        }

        private readonly record struct PushItem(uint OpCode, byte[] Payload);
    }
}
