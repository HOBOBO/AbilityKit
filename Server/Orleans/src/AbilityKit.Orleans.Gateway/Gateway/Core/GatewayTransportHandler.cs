using System.Buffers.Binary;
using System.Collections.Concurrent;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway.Abstractions;
using Microsoft.Extensions.Logging;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Core;

/// <summary>
/// Gateway 传输层事件处理
/// </summary>
public sealed class GatewayTransportHandler : IGatewayTransportEvents
{
    private readonly IGatewaySessionRegistry _sessionRegistry;
    private readonly IGatewayRequestRouter _router;
    private readonly IClusterClient _clusterClient;
    private readonly GatewayFrameSyncSubscriptionManager _frameSyncSubscriptions;
    private readonly ConcurrentDictionary<long, ConnectionState> _sessions = new();

    private readonly GatewayBackgroundTaskQueue _backgroundTasks;
    private readonly ILogger<GatewayTransportHandler> _logger;

    public GatewayTransportHandler(
        IGatewaySessionRegistry sessionRegistry,
        IGatewayRequestRouter router,
        IClusterClient clusterClient,
        GatewayBackgroundTaskQueue backgroundTasks,
        GatewayFrameSyncSubscriptionManager frameSyncSubscriptions,
        ILogger<GatewayTransportHandler> logger)
    {
        _sessionRegistry = sessionRegistry;
        _router = router;
        _clusterClient = clusterClient;
        _backgroundTasks = backgroundTasks;
        _frameSyncSubscriptions = frameSyncSubscriptions;
        _logger = logger;
    }

    public void OnConnected(IGatewayTransportSession session)
    {
        RegisterSession(session);
    }

    public void OnRequest(long connectionId, uint opCode, uint seq, byte[] payload)
    {
        if (!_sessions.TryGetValue(connectionId, out var connection))
            return;

        connection.Enqueue(cancellationToken =>
            ProcessRequestAsync(
                connection.Session,
                opCode,
                seq,
                payload,
                cancellationToken));
    }

    private async Task ProcessRequestAsync(
        IGatewayTransportSession session,
        uint opCode,
        uint seq,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _router.RouteAsync(
                session.Context,
                opCode,
                seq,
                payload,
                cancellationToken);
            var responsePayload = BuildResponsePayload(response);
            await session.SendResponseAsync(
                opCode,
                response.Seq,
                responsePayload,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Gateway request processing failed. OpCode={OpCode}, Seq={Seq}, ConnectionId={ConnectionId}, PayloadLength={PayloadLength}",
                opCode,
                seq,
                session.ConnectionId,
                payload.Length);
        }
    }

    private static byte[] BuildResponsePayload(GatewayResponse response)
    {
        var payload = response.Payload ?? Array.Empty<byte>();
        var responsePayload = new byte[sizeof(int) + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(responsePayload.AsSpan(0, sizeof(int)), response.StatusCode);
        payload.CopyTo(responsePayload.AsSpan(sizeof(int)));
        return responsePayload;
    }

    public void OnClosed(long connectionId)
    {
        if (_sessions.TryRemove(connectionId, out var connection))
        {
            connection.Cancel();
            MarkRoomMemberOffline(connection.Session.Context);
        }

        _sessionRegistry.Unregister(connectionId);
        _frameSyncSubscriptions.OnConnectionClosed(connectionId);
    }

    private void MarkRoomMemberOffline(GatewaySessionContext context)
    {
        var accountId = context.AccountId;
        var roomId = context.RoomId;
        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(roomId))
        {
            return;
        }

        _backgroundTasks.TryQueue(async _ =>
        {
            var room = _clusterClient.GetGrain<IRoomGrain>(roomId);
            await room.MarkOfflineAsync(accountId);
        });
    }

    internal void RegisterSession(IGatewayTransportSession session)
    {
        var connection = new ConnectionState(session);
        if (_sessions.TryGetValue(session.ConnectionId, out var previous))
        {
            previous.Cancel();
        }

        _sessions[session.ConnectionId] = connection;
        _sessionRegistry.Register(session.ConnectionId, session);
    }

    private sealed class ConnectionState
    {
        private readonly object _sync = new();
        private readonly CancellationTokenSource _cancellation = new();
        private Task _requestTail = Task.CompletedTask;

        public ConnectionState(IGatewayTransportSession session)
        {
            Session = session;
        }

        public IGatewayTransportSession Session { get; }

        public void Enqueue(Func<CancellationToken, Task> request)
        {
            lock (_sync)
            {
                if (_cancellation.IsCancellationRequested)
                {
                    return;
                }

                _requestTail = RunAfterAsync(
                    _requestTail,
                    request,
                    _cancellation.Token);
            }
        }

        public void Cancel()
        {
            lock (_sync)
            {
                _cancellation.Cancel();
            }
        }

        private static async Task RunAfterAsync(
            Task previous,
            Func<CancellationToken, Task> request,
            CancellationToken cancellationToken)
        {
            await previous.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await request(cancellationToken).ConfigureAwait(false);
        }
    }
}
