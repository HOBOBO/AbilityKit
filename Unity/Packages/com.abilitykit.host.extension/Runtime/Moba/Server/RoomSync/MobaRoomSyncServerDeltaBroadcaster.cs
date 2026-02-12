using System;
using AbilityKit.Ability.Host.Extensions.Moba.Room;

namespace AbilityKit.Ability.Host.Extensions.Moba.RoomSync
{
    public sealed class MobaRoomSyncServerDeltaBroadcaster : IDisposable
    {
        private readonly IMobaRoomOrchestrator _room;
        private readonly MobaRoomSyncServerDeltaOutbox _outbox;

        private int _lastEnqueuedRevision;

        public IMobaRoomSyncServerDeltaOutbox Outbox => _outbox;

        public MobaRoomSyncServerDeltaBroadcaster(IMobaRoomOrchestrator room)
        {
            _room = room ?? throw new ArgumentNullException(nameof(room));
            _outbox = new MobaRoomSyncServerDeltaOutbox();

            _lastEnqueuedRevision = 0;

            _room.AddChanged(OnRoomChanged);
        }

        public void Dispose()
        {
            _room.RemoveChanged(OnRoomChanged);
        }

        private void OnRoomChanged(MobaRoomChangedArgs args)
        {
            if (args.Revision > 0 && args.Revision == _lastEnqueuedRevision) return;

            var msg = MobaRoomChangedMessage.FromArgs(in args);
            _outbox.Enqueue(in msg);

            if (args.Revision > 0) _lastEnqueuedRevision = args.Revision;
        }
    }
}
