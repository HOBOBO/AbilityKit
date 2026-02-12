using System.Collections.Generic;

namespace AbilityKit.Ability.Host.Extensions.Moba.RoomSync
{
    public sealed class MobaRoomSyncServerDeltaOutbox : IMobaRoomSyncServerDeltaOutbox
    {
        private readonly Queue<MobaRoomChangedMessage> _deltas = new Queue<MobaRoomChangedMessage>();

        public int Count => _deltas.Count;

        public void Enqueue(in MobaRoomChangedMessage delta)
        {
            _deltas.Enqueue(delta);
        }

        public bool TryDequeue(out MobaRoomChangedMessage delta)
        {
            if (_deltas.Count > 0)
            {
                delta = _deltas.Dequeue();
                return true;
            }

            delta = default;
            return false;
        }

        public void Clear()
        {
            _deltas.Clear();
        }
    }
}
