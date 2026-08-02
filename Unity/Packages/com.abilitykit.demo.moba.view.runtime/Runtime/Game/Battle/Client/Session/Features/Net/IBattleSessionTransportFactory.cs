using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Transport;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Game.Flow
{
    internal interface IBattleSessionTransportFactory
    {
        IBattleLogicTransport CreateGatewayRemoteTransport(
            BattleStartPlan plan,
            uint localPlayerId,
            ulong roomId,
            IDispatcher callbackDispatcher,
            IDispatcher ioDispatcher);
    }

    internal sealed class DefaultBattleSessionTransportFactory : IBattleSessionTransportFactory
    {
        public IBattleLogicTransport CreateGatewayRemoteTransport(
            BattleStartPlan plan,
            uint localPlayerId,
            ulong roomId,
            IDispatcher callbackDispatcher,
            IDispatcher ioDispatcher)
        {
            var gateway = plan.Gateway;
            var gatewayOptions = NetworkTransportOptionsFactory.Create(
                host: gateway.Host,
                port: gateway.Port,
                transportFactory: () => new TcpTransport(),
                playerIdToUInt: pid => uint.TryParse(pid.Value, out var n) ? n : localPlayerId,
                playerIdFromUInt: n => new PlayerId(n.ToString()),
                worldIdToUlong: wid => ulong.TryParse(wid.Value, out var n) ? n : roomId,
                worldIdFromUlong: n => new WorldId(n.ToString()),
                roomId: roomId,
                sessionToken: gateway.SessionToken,
                battleId: gateway.BattleId,
                publicRoomId: gateway.JoinRoomId,
                useFrameSyncInput: SessionSimRuntimeTuning.ShouldUseFrameSyncInput(plan.Sync.SyncMode));

            return new NetworkTransport(gatewayOptions, callbackDispatcher, ioDispatcher);
        }
    }
}
