using System;
using AbilityKit.Game.Battle;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol.Room;

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

            var connOptions = new ConnectionOptions
            {
                FrameCodec = LengthPrefixedFrameCodec.Instance,
                EnableKickHandling = true,
                KickPushOpCode = RoomGatewayOpCodes.SessionKicked
            };

            return new ConnectionManager(() => new TcpTransport(), connOptions, callbackDispatcher, ioDispatcher);
        }
    }
}
