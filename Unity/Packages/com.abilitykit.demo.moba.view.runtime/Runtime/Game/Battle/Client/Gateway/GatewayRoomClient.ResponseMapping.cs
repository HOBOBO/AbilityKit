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
            return new GatewayRoomOperationResult(
                success,
                applied,
                errorCode,
                message,
                roomRevision,
                ToClientSnapshot(snapshot));
        }

        private static ClientRoomSnapshot ToClientSnapshot(RoomGatewaySnapshot snapshot)
        {
            return snapshot == null
                ? new ClientRoomSnapshot()
                : ClientRoomSnapshotMapper.ToClientSnapshot(snapshot);
        }

        private static RoomGatewayJoinKind ToJoinKind(RoomGatewaySessionEntryKind kind)
        {
            switch (kind)
            {
                case RoomGatewaySessionEntryKind.Reconnect:
                    return RoomGatewayJoinKind.Reconnect;
                case RoomGatewaySessionEntryKind.LateJoin:
                    return RoomGatewayJoinKind.LateJoin;
                default:
                    return RoomGatewayJoinKind.TeamLobby;
            }
        }

        private static GatewayWorldStartAnchor ToGatewayAnchor(in RoomGatewayWorldStartAnchor anchor)
        {
            return new GatewayWorldStartAnchor(
                anchor.StartServerTicks,
                anchor.ServerTickFrequency,
                anchor.StartFrame,
                anchor.FixedDeltaSeconds);
        }
    }
}
