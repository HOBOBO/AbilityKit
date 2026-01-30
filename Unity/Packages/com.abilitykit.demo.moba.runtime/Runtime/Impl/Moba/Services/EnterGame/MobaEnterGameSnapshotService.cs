using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaEnterGameSnapshotService : IWorldStateSnapshotProvider
    {
        private bool _hasSnapshot;
        private bool _sent;
        private byte[] _snapshotPayload;

        public void PublishEnterGameResPayload(byte[] payload)
        {
            _snapshotPayload = payload;
            _hasSnapshot = payload != null && payload.Length > 0;
            _sent = false;
        }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            if (!_hasSnapshot || _sent)
            {
                snapshot = default;
                return false;
            }

            snapshot = new WorldStateSnapshot((int)MobaOpCode.EnterGameSnapshot, _snapshotPayload);
            _sent = true;
            return true;
        }

        public void Dispose()
        {
            _hasSnapshot = false;
            _sent = false;
            _snapshotPayload = null;
        }
    }
}
