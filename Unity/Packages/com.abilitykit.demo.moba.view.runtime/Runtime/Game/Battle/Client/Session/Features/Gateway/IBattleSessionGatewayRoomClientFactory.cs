using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Sdk;

namespace AbilityKit.Game.Flow
{
    internal interface IBattleSessionGatewayRoomClientFactory
    {
        IGatewayRoomClient CreateGatewayRoomClient(IConnection connection, GatewayRoomOpCodes opCodes);
    }

    internal sealed class DefaultBattleSessionGatewayRoomClientFactory : IBattleSessionGatewayRoomClientFactory
    {
        public IGatewayRoomClient CreateGatewayRoomClient(IConnection connection, GatewayRoomOpCodes opCodes)
        {
            return connection is NetworkSdkClient sdkClient
                ? new GatewayRoomClient(sdkClient, opCodes)
                : new GatewayRoomClient(connection, opCodes);
        }
    }
}
