using System;
using AbilityKit.Ability.Host.Extensions.Moba.Room;

namespace AbilityKit.Ability.Host.Extensions.Moba.RoomSync
{
    public sealed class MobaRoomSyncServerBroadcaster : IDisposable
    {
        private readonly IMobaRoomOrchestrator _room;
        private readonly MobaRoomSyncServerOutbox _outbox;

        private int _lastEnqueuedRevision;

        public IMobaRoomSyncServerOutbox Outbox => _outbox;

        public MobaRoomSyncServerBroadcaster(IMobaRoomOrchestrator room)
        {
            _room = room ?? throw new ArgumentNullException(nameof(room));
            _outbox = new MobaRoomSyncServerOutbox();

            _lastEnqueuedRevision = 0;

            _room.AddChanged(OnRoomChanged);
        }

        public void Dispose()
        {
            _room.RemoveChanged(OnRoomChanged);
        }

        private void OnRoomChanged(MobaRoomChangedArgs args)
        {
            var snap = _room.Snapshot;

            if (snap.Revision > 0 && snap.Revision == _lastEnqueuedRevision) return;

            _outbox.Enqueue(new MobaRoomSnapshotMessage(in snap));

            if (snap.Revision > 0) _lastEnqueuedRevision = snap.Revision;
        }
    }
}
