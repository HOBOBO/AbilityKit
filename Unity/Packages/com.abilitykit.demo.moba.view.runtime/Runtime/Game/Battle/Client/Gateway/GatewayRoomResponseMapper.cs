using AbilityKit.Game.Flow;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Network.Room;

namespace AbilityKit.Game.Battle.Agent
{
    internal static class GatewayRoomResponseMapper
    {
        public static GatewayRoomOperationResult ToOperationResult(
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

        public static ClientRoomSnapshot ToClientSnapshot(
            RoomGatewaySnapshot snapshot)
        {
            return snapshot == null
                ? new ClientRoomSnapshot()
                : ClientRoomSnapshotMapper.ToClientSnapshot(snapshot);
        }

        public static ClientRoomSnapshot ToClientSnapshot(
            AbilityKit.Protocol.Room.WireRoomSnapshot snapshot)
        {
            return ClientRoomSnapshotMapper.ToClientSnapshot(snapshot);
        }

        public static RoomGatewayJoinKind ToJoinKind(
            RoomGatewaySessionEntryKind kind)
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

        public static GatewayWorldStartAnchor ToGatewayAnchor(
            in RoomGatewayWorldStartAnchor anchor)
        {
            return new GatewayWorldStartAnchor(
                anchor.StartServerTicks,
                anchor.ServerTickFrequency,
                anchor.StartFrame,
                anchor.FixedDeltaSeconds);
        }

        public static GatewayBattleInputResult ToBattleInputResult(
            in AbilityKit.Protocol.Room.WireSubmitBattleInputRes wire,
            ulong commandSequence)
        {
            return new GatewayBattleInputResult(
                wire.AcceptedFrame,
                wire.Success,
                wire.CurrentFrame,
                wire.Status,
                wire.Message,
                wire.ShouldResync,
                wire.ServerTicks,
                commandSequence);
        }

        public static GatewayStateSyncSnapshot ToGatewaySnapshot(
            in AbilityKit.Protocol.Room.WireStateSyncSnapshotPush push)
        {
            return GatewayStateSyncSnapshotMapper.Map(in push);
        }
    }
}
