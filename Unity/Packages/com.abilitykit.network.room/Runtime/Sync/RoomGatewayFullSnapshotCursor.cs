#nullable enable

using AbilityKit.Protocol.Room;

namespace AbilityKit.Network.Room
{
    /// <summary>Accepts only newer complete snapshots for one subscribed world.</summary>
    public sealed class RoomGatewayFullSnapshotCursor
    {
        private int _minimumFrame;
        public int LastFrame { get; private set; } = -1;
        public ulong WorldId { get; private set; }

        public void Reset(ulong worldId)
        {
            WorldId = worldId;
            LastFrame = -1;
            _minimumFrame = 0;
        }

        public void RequireAtLeast(int minimumFrame)
        {
            if (minimumFrame < 0) throw new System.ArgumentOutOfRangeException(nameof(minimumFrame));
            if (minimumFrame > _minimumFrame)
                _minimumFrame = minimumFrame;
        }

        public bool TryAccept(in WireStateSyncSnapshotPush snapshot)
        {
            if (!CanAccept(in snapshot)) return false;
            LastFrame = snapshot.Frame;
            _minimumFrame = 0;
            return true;
        }

        public bool CanAccept(in WireStateSyncSnapshotPush snapshot) =>
            WorldId != 0 && snapshot.WorldId == WorldId &&
            snapshot.IsFullSnapshot && snapshot.Frame >= _minimumFrame &&
            snapshot.Frame > LastFrame;
    }
}
