using System;
using AbilityKit.Ability.Host;
using AbilityKit.Game.Battle;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleSessionConnectionRuntime
    {
        private readonly IBattleSessionTransportFactory _transportFactory;
        private readonly IBattleLogicSessionRegistry _sessionRegistry;
        private readonly NetworkConditionController _networkCondition;
        private readonly InputSubmissionDiagnosticsBinding _inputDiagnostics;

        internal BattleSessionConnectionRuntime(
            IBattleSessionTransportFactory transportFactory,
            IBattleLogicSessionRegistry sessionRegistry,
            NetworkConditionController networkCondition,
            InputSubmissionDiagnosticsBinding inputDiagnostics)
        {
            _transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
            _sessionRegistry = sessionRegistry ?? throw new ArgumentNullException(nameof(sessionRegistry));
            _networkCondition = networkCondition ?? throw new ArgumentNullException(nameof(networkCondition));
            _inputDiagnostics = inputDiagnostics ?? throw new ArgumentNullException(nameof(inputDiagnostics));
        }

        internal BattleLogicSession Start(BattleStartPlan plan, BattleLogicSessionOptions options,
            IDispatcher callbackDispatcher, IDispatcher ioDispatcher)
        {
            if (plan.HostMode != BattleHostMode.GatewayRemote || !plan.Gateway.UseGatewayTransport)
                return _sessionRegistry.Start(options);

            if (!uint.TryParse(plan.World.PlayerId, out var playerId))
                throw new InvalidOperationException(
                    $"GatewayRemote requires numeric PlayerId. playerId='{plan.World.PlayerId}'");

            var roomId = plan.Gateway.NumericRoomId;
            if (roomId == 0 && !ulong.TryParse(plan.World.WorldId, out roomId))
                throw new InvalidOperationException(
                    $"GatewayRemote requires numeric WorldId(roomId). worldId='{plan.World.WorldId}'");

            if (plan.Sync.SyncMode != BattleSyncMode.Lockstep)
                throw new InvalidOperationException(
                    $"MOBA GatewayRemote only supports Lockstep frame sync. SyncMode={plan.Sync.SyncMode}");

            var transport = _transportFactory.CreateGatewayRemoteTransport(
                plan, playerId, roomId, callbackDispatcher, ioDispatcher);
            try
            {
                if (transport is NetworkTransport networkTransport)
                {
                    _networkCondition.Attach(networkTransport.Connection);
                    _inputDiagnostics.Bind(networkTransport, plan.World.WorldId);
                }

            }
            catch
            {
                (transport as IDisposable)?.Dispose();
                throw;
            }

            return _sessionRegistry.Start(options, remoteTransport: transport);
        }
    }
}
