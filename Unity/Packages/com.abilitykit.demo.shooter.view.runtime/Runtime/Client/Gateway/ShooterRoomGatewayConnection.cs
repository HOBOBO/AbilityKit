#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Sdk;

namespace AbilityKit.Demo.Shooter.View
{
    /// <summary>
    /// Room-control request transport for the room gateway. After P2.2 this connection is ROOM-ONLY:
    /// battle data (input submit + snapshot/event push + ack/resync) moved to the dedicated battle
    /// <see cref="NetworkTransport"/> driven by <see cref="ShooterBattleDataPlane"/>. The battle-state
    /// members (<see cref="CurrentSession"/>, <see cref="LastPushResult"/>, <see cref="SnapshotPushDispatched"/>)
    /// are retained as a FACADE populated from the battle data plane, so existing
    /// <c>GatewayConnection.X</c> consumers keep working. <see cref="OnServerPushReceived"/> no longer
    /// applies battle snapshots (so room-connection battle pushes, if any, are not double-applied).
    /// </summary>
    public sealed class ShooterRoomGatewayConnection :
        IShooterRoomGatewayRequestTransport,
        IShooterRoomGatewayPushTransport,
        IDisposable
    {
        private readonly Func<uint, ArraySegment<byte>, TimeSpan?, CancellationToken, Task<ArraySegment<byte>>> _sendRequestAsync;
        private readonly Action<Action<uint, ArraySegment<byte>>> _unsubscribeServerPush;
        private readonly NetworkSdkClient? _ownedSdkClient;
        private ShooterClientSession? _session;
        private bool _disposed;

        /// <summary>
        /// Compatibility path for injected connections. The SDK owns the single request dispatcher
        /// and the supplied connection for this facade's lifetime.
        /// </summary>
        public ShooterRoomGatewayConnection(IConnection connection)
            : this(CreateOwnedSdkClient(connection), null, ownsSdkClient: true)
        {
        }

        public ShooterRoomGatewayConnection(IConnection connection, ShooterClientSession? session)
            : this(CreateOwnedSdkClient(connection), session, ownsSdkClient: true)
        {
        }

        public ShooterRoomGatewayConnection(NetworkSdkClient sdkClient)
            : this(sdkClient, null, ownsSdkClient: false)
        {
        }

        public ShooterRoomGatewayConnection(NetworkSdkClient sdkClient, ShooterClientSession? session)
            : this(sdkClient, session, ownsSdkClient: false)
        {
        }

        private ShooterRoomGatewayConnection(
            NetworkSdkClient sdkClient,
            ShooterClientSession? session,
            bool ownsSdkClient)
        {
            if (sdkClient == null) throw new ArgumentNullException(nameof(sdkClient));

            _sendRequestAsync = sdkClient.SendRawRequestAsync;
            _unsubscribeServerPush = handler => sdkClient.ServerPushReceived -= handler;
            _ownedSdkClient = ownsSdkClient ? sdkClient : null;
            _session = session;
            sdkClient.ServerPushReceived += OnServerPushReceived;
        }

        public event Action<uint, ArraySegment<byte>, ShooterSnapshotApplyResult>? SnapshotPushDispatched;

        public event Action<uint, ArraySegment<byte>>? ServerPushReceived;

        public ShooterSnapshotApplyResult LastPushResult { get; private set; } = ShooterSnapshotApplyResult.Ignored;

        public ShooterClientSession? CurrentSession => _session;

        public void AttachSession(ShooterClientSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public void AttachBattle(ShooterClientBattleHandle battle)
        {
            _session = (battle ?? throw new ArgumentNullException(nameof(battle))).Session;
        }

        public Task<ArraySegment<byte>> SendRequestAsync(uint opCode, ArraySegment<byte> payload, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return _sendRequestAsync(opCode, payload, timeout, cancellationToken);
        }

        private void OnServerPushReceived(uint opCode, ArraySegment<byte> payload)
        {
            if (_disposed)
            {
                return;
            }

            // Room control plane only. Battle pushes are handled by ShooterBattleDataPlane on the
            // dedicated battle connection; this connection no longer applies battle snapshots.
            ServerPushReceived?.Invoke(opCode, payload);
        }

        /// <summary>Populates the battle-state facade from the battle data plane
        /// (<see cref="LastPushResult"/> / <see cref="SnapshotPushDispatched"/>), so existing
        /// <c>GatewayConnection.X</c> telemetry consumers keep working post-P2.2.</summary>
        internal void NotifyBattlePushDispatched(uint opCode, ArraySegment<byte> payload, ShooterSnapshotApplyResult result)
        {
            if (_disposed)
            {
                return;
            }

            LastPushResult = result;
            SnapshotPushDispatched?.Invoke(opCode, payload, result);
        }

        private static NetworkSdkClient CreateOwnedSdkClient(IConnection connection) =>
            new NetworkSdkBuilder()
                .UseOwnedConnectionFactory(
                    () => connection ?? throw new ArgumentNullException(nameof(connection)))
                .Build();

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
            _unsubscribeServerPush(OnServerPushReceived);
            _ownedSdkClient?.Dispose();
        }
    }
}
