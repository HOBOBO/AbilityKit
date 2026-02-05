using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Transport;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private BattleLogicSession StartBattleLogicSession(BattleLogicSessionOptions opts)
        {
            if (_plan.HostMode == BattleStartConfig.BattleHostMode.GatewayRemote && _plan.UseGatewayTransport)
            {
                if (!uint.TryParse(_plan.PlayerId, out var localPlayerId))
                {
                    throw new InvalidOperationException($"GatewayRemote requires numeric PlayerId. playerId='{_plan.PlayerId}'");
                }

                var roomId = _plan.NumericRoomId;
                if (roomId == 0 && !ulong.TryParse(_plan.WorldId, out roomId))
                {
                    throw new InvalidOperationException($"GatewayRemote requires numeric WorldId(roomId). worldId='{_plan.WorldId}'");
                }

                var gatewayOptions = GatewayRemoteBattleTransportOptionsFactory.Create(
                    host: _plan.GatewayHost,
                    port: _plan.GatewayPort,
                    transportFactory: () => new TcpTransport(),
                    playerIdToUInt: pid => uint.TryParse(pid.Value, out var n) ? n : localPlayerId,
                    playerIdFromUInt: n => new PlayerId(n.ToString()),
                    worldIdToUlong: wid => ulong.TryParse(wid.Value, out var n) ? n : roomId,
                    worldIdFromUlong: n => new WorldId(n.ToString()),
                    roomId: roomId,
                    sessionToken: _plan.GatewaySessionToken);

                var transport = new GatewayBattleLogicTransport(gatewayOptions, _unityDispatcher, _networkIoDispatcher);
                return BattleLogicSessionHost.Start(opts, remoteTransport: transport);
            }

            return BattleLogicSessionHost.Start(opts);
        }
    }
}
