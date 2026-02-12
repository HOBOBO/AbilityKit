using System.Collections.Generic;

namespace AbilityKit.Ability.Host.Extensions.Moba.RoomSync
{
    public sealed class MobaRoomSyncServerOutbox : IMobaRoomSyncServerOutbox
    {
        private readonly Queue<MobaRoomSnapshotMessage> _snapshots = new Queue<MobaRoomSnapshotMessage>();

        public int Count => _snapshots.Count;

        public void Enqueue(in MobaRoomSnapshotMessage snapshot)
        {
            _snapshots.Enqueue(snapshot);
        }

        public bool TryDequeue(out MobaRoomSnapshotMessage snapshot)
        {
            if (_snapshots.Count > 0)
            {
                snapshot = _snapshots.Dequeue();
                return true;
            }

            snapshot = default;
            return false;
        }

        public void Clear()
        {
            _snapshots.Clear();
        }
    }
}
