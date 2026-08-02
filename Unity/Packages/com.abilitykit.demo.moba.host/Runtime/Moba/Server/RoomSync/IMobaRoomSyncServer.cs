using AbilityKit.Ability.Host.Extensions.Moba.Room;

namespace AbilityKit.Ability.Host.Extensions.Moba.RoomSync
{
    public interface IMobaRoomSyncServer
    {
        bool TryHandleHello(string clientId, in MobaRoomHello hello, out MobaRoomSnapshotMessage snapshotToSend);

        bool TryHandleRequestSnapshot(in MobaRoomRequestSnapshotMessage request, out MobaRoomSnapshotMessage snapshotToSend);

        bool TryHandle(in MobaRoomHello hello, out MobaRoomSnapshotMessage snapshotToSend);

        bool TryHandle(in MobaRoomRequestSnapshotMessage request, out MobaRoomSnapshotMessage snapshotToSend);

        MobaRoomCommandResultMessage HandleCommand(in MobaRoomCommandMessage command);

        MobaRoomCommandResultMessage Handle(in MobaRoomCommandMessage command);

        MobaRoomSnapshotMessage BuildSnapshotMessage();
    }
}
