using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Client;
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
        internal static uint ParsePlayerId(PlayerId playerId) => NetworkIdCodec.Numeric.Encode(playerId);

        internal static ulong ParseWorldId(WorldId worldId) => NetworkIdCodec.Numeric.Encode(worldId);

        public IBattleLogicTransport CreateGatewayRemoteTransport(
            BattleStartPlan plan,
            uint localPlayerId,
            ulong roomId,
            IDispatcher callbackDispatcher,
            IDispatcher ioDispatcher)
        {
            var gateway = plan.Gateway;
            var gatewayOptions = NetworkTransportOptionsFactory.Create(new NetworkTransportOptionsFactoryOptions
            {
                Host = gateway.Host,
                Port = gateway.Port,
                TransportFactory = () => new TcpTransport(),
                IdCodec = NetworkIdCodec.Numeric,
                RoomId = roomId,
                SessionToken = gateway.SessionToken,
                BattleId = gateway.BattleId,
                PublicRoomId = gateway.JoinRoomId,
                UseFrameSyncInput = SessionSimRuntimeTuning.ShouldUseFrameSyncInput(plan.Sync.SyncMode)
            });

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

            return MultiplayerClientSession.CreateBattleTransport(
                gatewayOptions, callbackDispatcher, ioDispatcher, connect: false);
        }

    }
}
