#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterRoomGatewayConnection :
        IShooterRoomGatewayRequestTransport,
        IShooterRoomGatewayPushTransport,
        IDisposable
    {
        private static readonly TimeSpan AutomaticFullStateSyncTimeout = TimeSpan.FromSeconds(10);

        private readonly IConnection _connection;
        private readonly RequestClient _requestClient;
        private ShooterClientSession? _session;
        private ShooterClientBattleHandle? _battle;
        private long _lastReliableEventAckRequested;
        private bool _disposed;

        public ShooterRoomGatewayConnection(IConnection connection)
            : this(connection, null)
        {
        }

        public ShooterRoomGatewayConnection(IConnection connection, ShooterClientSession? session)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _requestClient = new RequestClient(_connection);
            _session = session;
            _connection.ServerPushReceived += OnServerPushReceived;
        }

        public event Action<uint, ArraySegment<byte>, ShooterSnapshotApplyResult>? SnapshotPushDispatched;

        public event Action<uint, ArraySegment<byte>>? ServerPushReceived;

        public ShooterSnapshotApplyResult LastPushResult { get; private set; } = ShooterSnapshotApplyResult.Ignored;

        public ShooterClientSession? CurrentSession => _session;

        public void AttachSession(ShooterClientSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _battle = null;
            _lastReliableEventAckRequested = session.LastReliableEventAck;
        }

        public void AttachBattle(ShooterClientBattleHandle battle)
        {
            _battle = battle ?? throw new ArgumentNullException(nameof(battle));
            _session = battle.Session;
            _lastReliableEventAckRequested = battle.Session.LastReliableEventAck;
        }

        public Task<ArraySegment<byte>> SendRequestAsync(uint opCode, ArraySegment<byte> payload, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return _requestClient.SendRequestAsync(opCode, payload, timeout, cancellationToken);
        }

        private void OnServerPushReceived(uint opCode, ArraySegment<byte> payload)
        {
            if (_disposed)
            {
                return;
            }

            ServerPushReceived?.Invoke(opCode, payload);

            var session = _session;
            var result = session == null
                ? ShooterSnapshotApplyResult.Ignored
                : session.ApplyGatewayPush(opCode, payload);

            LastPushResult = result;
            SnapshotPushDispatched?.Invoke(opCode, payload, result);
            AcknowledgeReliableBattleEventsIfNeededAsync();
            RequestFullSnapshotResyncIfNeededAsync();
        }

        private void AcknowledgeReliableBattleEventsIfNeededAsync()
        {
            var battle = _battle;
            if (battle == null
                || battle.Session.NeedsReliableEventResync
                || battle.Session.LastReliableEventAck <= _lastReliableEventAckRequested)
            {
                return;
            }

            _lastReliableEventAckRequested = battle.Session.LastReliableEventAck;
            _ = AcknowledgeReliableBattleEventsAsync(battle, _lastReliableEventAckRequested);
        }

        private async Task AcknowledgeReliableBattleEventsAsync(ShooterClientBattleHandle battle, long requestedSequence)
        {
            try
            {
                var result = await battle.AcknowledgeReliableBattleEventsAsync().ConfigureAwait(false);
                if (!result.Success)
                {
                    if (_lastReliableEventAckRequested == requestedSequence)
                    {
                        _lastReliableEventAckRequested = Math.Max(0L, result.AcceptedAckSequence);
                    }

                    await battle.RequestFullSnapshotBaselineAsync("ReliableEventGap").ConfigureAwait(false);
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch
            {
                if (_lastReliableEventAckRequested == requestedSequence)
                {
                    _lastReliableEventAckRequested = Math.Max(0L, requestedSequence - 1L);
                }
            }
        }

        private void RequestFullSnapshotResyncIfNeededAsync()
        {
            var battle = _battle;
            if (battle == null)
            {
                return;
            }

            _ = RequestFullSnapshotResyncIfNeededAsync(battle);
        }

        private async Task RequestFullSnapshotResyncIfNeededAsync(ShooterClientBattleHandle battle)
        {
            try
            {
                await battle.RequestFullSnapshotResyncIfNeededAsync(AutomaticFullStateSyncTimeout).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ShooterRoomGatewayConnection));
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _connection.ServerPushReceived -= OnServerPushReceived;
            _requestClient.Dispose();
        }
    }
}
