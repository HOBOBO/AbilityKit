using AbilityKit.Game.Flow;
using AbilityKit.Network.Room;

namespace AbilityKit.Game.Battle.Agent
{
    public sealed partial class GatewayRoomClient
    {
        private static GatewayRoomOperationResult ToOperationResult(
            bool success,
            bool applied,
            int errorCode,
            string message,
            long roomRevision,
            RoomGatewaySnapshot snapshot)
        {
            return GatewayRoomResponseMapper.ToOperationResult(
                success,
                applied,
                errorCode,
                message,
                roomRevision,
                snapshot);
        }

        private static ClientRoomSnapshot ToClientSnapshot(
            RoomGatewaySnapshot snapshot)
        {
            return GatewayRoomResponseMapper.ToClientSnapshot(snapshot);
        }

        private static RoomGatewayJoinKind ToJoinKind(
            RoomGatewaySessionEntryKind kind)
        {
            return GatewayRoomResponseMapper.ToJoinKind(kind);
        }

        private static GatewayWorldStartAnchor ToGatewayAnchor(
            in RoomGatewayWorldStartAnchor anchor)
        {
            return GatewayRoomResponseMapper.ToGatewayAnchor(in anchor);
        }
    }

}
