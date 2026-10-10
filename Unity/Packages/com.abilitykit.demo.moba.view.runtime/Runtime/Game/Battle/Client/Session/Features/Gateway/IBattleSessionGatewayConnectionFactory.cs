using System;
using AbilityKit.Game.Battle;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Client;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol.Room;
using AbilityKit.Network.Sdk;
#if UNITY_EDITOR
using AbilityKit.Network.Runtime.Observability;
using AbilityKit.Network.Sdk.Observability;
#endif

namespace AbilityKit.Game.Flow
{
    internal interface IBattleSessionGatewayConnectionFactory
    {
        IConnection CreateGatewayRoomConnection(
            BattleStartPlan plan,
            IDispatcher callbackDispatcher,
            IDispatcher ioDispatcher);
    }

    internal sealed class DefaultBattleSessionGatewayConnectionFactory : IBattleSessionGatewayConnectionFactory
    {
        private readonly Func<BattleStartPlan, IConnection> _customConnectionFactory;

        public DefaultBattleSessionGatewayConnectionFactory(Func<BattleStartPlan, IConnection> customConnectionFactory = null)
        {
            _customConnectionFactory = customConnectionFactory;
        }

        public IConnection CreateGatewayRoomConnection(
            BattleStartPlan plan,
            IDispatcher callbackDispatcher,
            IDispatcher ioDispatcher)
        {
            if (_customConnectionFactory != null)
            {
                var connection = _customConnectionFactory(plan);
                if (connection == null)
                {
                    throw new InvalidOperationException("Gateway connection factory returned null.");
                }

                return connection;
            }

            var connOptions = new ConnectionOptions();
#if UNITY_EDITOR
            RoomProtocolDecoderModule.Register(NetworkTrafficMonitor.Default.Decoders);
            AbilityKit.Protocol.Moba.MobaProtocolDecoderModule.Register(
                NetworkTrafficMonitor.Default.Decoders);
            connOptions.TrafficCapture = new NetworkTrafficCaptureOptions
            {
                Role = "battle-room",
                CatalogId = "abilitykit.room",
                TransportName = "tcp",
                MaximumPayloadPreviewBytes = 65536,
                ObserverFactory = _ => NetworkTrafficMonitor.Default
            };
#endif

            return new NetworkSdkBuilder()
                .UseTransportFactory(() => new TcpTransport())
                .ConfigureConnection(options =>
                {
                    GatewayRoomSdkConnectionPreset.Configure(options);
#if UNITY_EDITOR
                    options.TrafficCapture = connOptions.TrafficCapture;
#endif
                })
                .UseDispatchers(callbackDispatcher, ioDispatcher)
                .Build();
        }
    }
}
