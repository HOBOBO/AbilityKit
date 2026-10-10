using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Network.Sdk;
#if UNITY_EDITOR
using AbilityKit.Network.Sdk.Observability;
#endif

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Owns gateway connection, room preparation, and clock synchronization resources
    /// for one session generation.
    /// </summary>
    internal sealed class GatewaySessionRuntime : IDisposable
    {
        private readonly BattleSessionHandles _handles;
        private readonly IAbilityKitConnectionRegistry _connectionRegistry;
        private readonly IBattleSessionGatewayConnectionFactory _connectionFactory;
        private readonly IBattleSessionGatewayRoomClientFactory _clientFactory;
        private readonly NetworkConditionController _networkCondition;
        private readonly GatewayClockSynchronizer _clock = new GatewayClockSynchronizer();
        private readonly GatewayPreparationRuntime _preparation;
        private readonly SemaphoreSlim _preparationGate = new SemaphoreSlim(1, 1);

        private readonly object _ownershipToken = new object();
        private IConnection _connection;
        private IAbilityKitConnectionLease _connectionLease;
        private IGatewayRoomClient _client;
#if UNITY_EDITOR
        private IDisposable _packetInterception;
#endif

        internal GatewaySessionRuntime(
            BattleSessionHandles handles,
            IAbilityKitConnectionRegistry connectionRegistry,
            IBattleSessionGatewayConnectionFactory connectionFactory,
            IBattleSessionGatewayRoomClientFactory clientFactory,
            NetworkConditionController networkCondition)
        {
            _handles = handles ?? throw new ArgumentNullException(nameof(handles));
            _connectionRegistry = connectionRegistry ?? throw new ArgumentNullException(nameof(connectionRegistry));
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _networkCondition = networkCondition ?? throw new ArgumentNullException(nameof(networkCondition));
            _preparation = new GatewayPreparationRuntime(_clock);
        }

        internal IConnection Connection => _connection;
        internal IGatewayRoomClient Client => _client;
        internal bool IsBuilt => _connection != null;
        internal System.Threading.Tasks.Task PreparationTask => _preparation.Task;
        internal GatewayTimeSyncEwma ClockEstimate => _clock.Estimate;
        internal System.Collections.Generic.IReadOnlyDictionary<AbilityKit.Ability.World.Abstractions.WorldId, GatewayWorldStartAnchor> WorldStartAnchors =>
            _preparation.WorldStartAnchors;

        internal void Build(
            BattleStartPlan plan,
            IDispatcher callbackDispatcher,
            IDispatcher ioDispatcher)
        {
            Dispose();

            try
            {
                var gateway = plan.Gateway;
                var descriptor = new AbilityKitConnectionDescriptor(
                    AbilityKitConnectionRole.GatewayReliable,
                    gateway.Host,
                    gateway.Port,
                    "tcp");

                _connectionLease = _connectionRegistry.Acquire(
                    descriptor,
                    _ => _connectionFactory.CreateGatewayRoomConnection(
                        plan,
                        callbackDispatcher,
                        ioDispatcher));
                _connection = _connectionLease.Connection;
                if (_connection == null)
                {
                    throw new InvalidOperationException("Gateway connection registry returned null.");
                }

                _handles.GatewayRoom.ConnectionOwner = _ownershipToken;
                _handles.GatewayRoom.Conn = _connection;
#if UNITY_EDITOR
                if (_connection is NetworkSdkClient sdkClient)
                    _packetInterception = NetworkPacketInterception.Default.Attach(sdkClient, "abilitykit.room");
                if (_connection is ConnectionManager manager)
                    _packetInterception = NetworkPacketInterception.Default.Attach(manager, "abilitykit.room");
#endif
                if (_connectionLease.OwnsLifecycle)
                    _connection.Open(gateway.Host, gateway.Port);
                _networkCondition.Attach(_connection);

                var opCodes = new GatewayRoomOpCodes(
                    gateway.CreateRoomOpCode,
                    gateway.JoinRoomOpCode);
                _client = _clientFactory.CreateGatewayRoomClient(_connection, opCodes);
                if (_client == null)
                {
                    throw new InvalidOperationException("Gateway room client factory returned null.");
                }

                _handles.GatewayRoom.Client = _client;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal void Tick(float deltaTime)
        {
            if (_connectionLease != null && _connectionLease.OwnsLifecycle)
                _connection?.Tick(deltaTime);
        }

        internal void StartPreparation(
            BattleStartPlan plan,
            Action<BattleStartPlan> planPublished,
            Action<GatewayTimeSyncEwma, GatewayTimeSyncRuntimeOptions> clockSamplePublished,
            Action<Exception> clockFailurePublished)
        {
            if (_connection == null || _client == null)
            {
                throw new InvalidOperationException("Gateway session runtime must be built before room preparation starts.");
            }

            _preparation.Start(
                _connection,
                _client,
                _client,
                _client,
                plan,
                planPublished,
                clockSamplePublished,
                clockFailurePublished);
        }

        internal async Task StartNewPreparationAsync(
            BattleStartPlan plan,
            IDispatcher callbackDispatcher,
            IDispatcher ioDispatcher,
            Action resetPreviousState,
            Action<BattleStartPlan> planPublished,
            Action<GatewayTimeSyncEwma, GatewayTimeSyncRuntimeOptions> clockSamplePublished,
            Action<Exception> clockFailurePublished)
        {
            await _preparationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await StopAsync().ConfigureAwait(false);
                resetPreviousState?.Invoke();
                Build(plan, callbackDispatcher, ioDispatcher);
                StartPreparation(plan, planPublished, clockSamplePublished, clockFailurePublished);
            }
            finally
            {
                _preparationGate.Release();
            }
        }

        internal bool TryGetWorldStartAnchor(
            AbilityKit.Ability.World.Abstractions.WorldId worldId,
            out GatewayWorldStartAnchor anchor)
        {
            return _preparation.TryGetWorldStartAnchor(worldId, out anchor);
        }

        internal async System.Threading.Tasks.Task CompletePreparationAsync()
        {
            await _preparationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await _preparation.StopWorkAsync().ConfigureAwait(false);
            }
            finally
            {
                DisposeConnection();
                _preparationGate.Release();
            }
        }

        internal async Task StopPreparationAsync()
        {
            await _preparationGate.WaitAsync().ConfigureAwait(false);
            try { await StopAsync().ConfigureAwait(false); }
            finally { _preparationGate.Release(); }
        }

        internal async System.Threading.Tasks.Task StopAsync()
        {
            try
            {
                await _preparation.StopWorkAsync().ConfigureAwait(false);
            }
            finally
            {
                DisposeConnection();
            }
        }

        public void Dispose()
        {
            _preparation.Dispose();
            DisposeConnection();
        }

        private void DisposeConnection()
        {
            var client = _client;
            var connection = _connection;
            var connectionLease = _connectionLease;
            _client = null;
            _connection = null;
            _connectionLease = null;
#if UNITY_EDITOR
            _packetInterception?.Dispose();
            _packetInterception = null;
#endif

            var ownsPublishedConnection = ReferenceEquals(
                _handles.GatewayRoom.ConnectionOwner,
                _ownershipToken);

            if (ownsPublishedConnection)
            {
                _networkCondition.Detach();

                if (ReferenceEquals(_handles.GatewayRoom.Client, client))
                {
                    _handles.GatewayRoom.Client = null;
                }
            }

            try
            {
                client?.Dispose();
            }
            finally
            {
                if (ownsPublishedConnection)
                {
                    if (ReferenceEquals(_handles.GatewayRoom.Conn, connection))
                    {
                        _handles.GatewayRoom.Conn = null;
                    }

                    _handles.GatewayRoom.ConnectionOwner = null;
                }

                connectionLease?.Dispose();
            }
        }
    }
}
