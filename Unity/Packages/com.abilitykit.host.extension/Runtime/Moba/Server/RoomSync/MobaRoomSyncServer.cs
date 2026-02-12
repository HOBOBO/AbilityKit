using System;
using AbilityKit.Ability.Host.Extensions.Moba.Room;

namespace AbilityKit.Ability.Host.Extensions.Moba.RoomSync
{
    public sealed class MobaRoomSyncServer : IMobaRoomSyncServer
    {
        private readonly IMobaRoomOrchestrator _room;

        public MobaRoomSyncServer(IMobaRoomOrchestrator room)
        {
            _room = room ?? throw new ArgumentNullException(nameof(room));
        }

        public bool TryHandleHello(string clientId, in MobaRoomHello hello, out MobaRoomSnapshotMessage snapshotToSend)
        {
            var snap = _room.Snapshot;

            if (hello.LastSeenRevision < snap.Revision)
            {
                snapshotToSend = new MobaRoomSnapshotMessage(in snap);
                return true;
            }

            snapshotToSend = default;
            return false;
        }

        public bool TryHandle(in MobaRoomHello hello, out MobaRoomSnapshotMessage snapshotToSend)
        {
            return TryHandleHello(hello.ClientId, in hello, out snapshotToSend);
        }

        public bool TryHandleRequestSnapshot(in MobaRoomRequestSnapshotMessage request, out MobaRoomSnapshotMessage snapshotToSend)
        {
            var snap = _room.Snapshot;

            if (request.LastSeenRevision < snap.Revision)
            {
                snapshotToSend = new MobaRoomSnapshotMessage(in snap);
                return true;
            }

            snapshotToSend = default;
            return false;
        }

        public bool TryHandle(in MobaRoomRequestSnapshotMessage request, out MobaRoomSnapshotMessage snapshotToSend)
        {
            return TryHandleRequestSnapshot(in request, out snapshotToSend);
        }

        public MobaRoomCommandResultMessage HandleCommand(in MobaRoomCommandMessage command)
        {
            var result = _room.Apply(in command.Command);
            return new MobaRoomCommandResultMessage(command.ClientId, in result);
        }

        public MobaRoomCommandResultMessage Handle(in MobaRoomCommandMessage command)
        {
            return HandleCommand(in command);
        }

        public MobaRoomSnapshotMessage BuildSnapshotMessage()
        {
            var snap = _room.Snapshot;
            return new MobaRoomSnapshotMessage(in snap);
        }
    }
}
