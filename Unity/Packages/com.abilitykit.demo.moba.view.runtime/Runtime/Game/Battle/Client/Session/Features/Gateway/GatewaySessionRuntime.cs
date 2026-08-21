using System;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Conditioning;

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

        private readonly object _ownershipToken = new object();
        private IConnection _connection;
        private IGatewayRoomClient _client;

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

                _connection = _connectionRegistry.GetOrCreate(
                    descriptor,
                    _ => _connectionFactory.CreateGatewayRoomConnection(
                        plan,
                        callbackDispatcher,
                        ioDispatcher));
                if (_connection == null)
                {
                    throw new InvalidOperationException("Gateway connection registry returned null.");
                }

                _handles.GatewayRoom.ConnectionOwner = _ownershipToken;
                _handles.GatewayRoom.Conn = _connection;
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

        internal bool TryGetWorldStartAnchor(
            AbilityKit.Ability.World.Abstractions.WorldId worldId,
            out GatewayWorldStartAnchor anchor)
        {
            return _preparation.TryGetWorldStartAnchor(worldId, out anchor);
        }

        internal void CompletePreparation()
        {
            _preparation.StopWork();
            DisposeConnection();
        }

        internal async System.Threading.Tasks.Task CompletePreparationAsync()
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
            _client = null;
            _connection = null;

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
                    if (connection != null &&
                        _connectionRegistry.TryGet(
                            AbilityKitConnectionRole.GatewayReliable,
                            out var current) &&
                        ReferenceEquals(current, connection))
                    {
                        _connectionRegistry.Remove(
                            AbilityKitConnectionRole.GatewayReliable);
                    }

                    if (ReferenceEquals(_handles.GatewayRoom.Conn, connection))
                    {
                        _handles.GatewayRoom.Conn = null;
                    }

                    _handles.GatewayRoom.ConnectionOwner = null;
                }
            }
        }
    }
}
