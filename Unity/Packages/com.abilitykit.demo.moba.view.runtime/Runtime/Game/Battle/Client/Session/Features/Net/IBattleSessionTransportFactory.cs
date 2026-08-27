using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Sdk.Observability;
using AbilityKit.Protocol.Moba;

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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            MobaProtocolDecoderModule.Register(NetworkTrafficMonitor.Default.Decoders);
            gatewayOptions.TrafficObserver = NetworkTrafficMonitor.Default;
            gatewayOptions.ConfigureTrafficCapture = options =>
            {
                options.ConnectionId = "moba-battle-primary";
                options.Role = "battle";
                options.CatalogId = "abilitykit.moba.battle";
                options.TransportName = "tcp";
                options.MaximumPayloadPreviewBytes = 65536;
                options.FilterFactory = NetworkTrafficMonitor.Default.CreateSamplingFilter;
            };
#endif

            return new NetworkTransport(gatewayOptions, callbackDispatcher, ioDispatcher);
        }
    }
}
