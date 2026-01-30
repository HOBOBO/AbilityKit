using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaActorSpawnSnapshotService : IWorldStateSnapshotProvider
    {
        private bool _hasSnapshot;
        private bool _sent;
        private byte[] _snapshotPayload;

        private FrameIndex _lastFrame;
        private readonly List<MobaActorSpawnSnapshotCodec.Entry> _pending = new List<MobaActorSpawnSnapshotCodec.Entry>(64);

        public void PublishSpawnPayload(byte[] payload)
        {
            _snapshotPayload = payload;
            _hasSnapshot = payload != null && payload.Length > 0;
            _sent = false;
        }

        public void Enqueue(in MobaActorSpawnSnapshotCodec.Entry entry)
        {
            if (entry.NetId <= 0) return;
            _pending.Add(entry);
        }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            // At most once per frame.
            if (frame.Value == _lastFrame.Value)
            {
                snapshot = default;
                return false;
            }
            _lastFrame = frame;

            // 1) One-shot bulk payload (enter-game).
            if (_hasSnapshot && !_sent)
            {
                snapshot = new WorldStateSnapshot((int)MobaOpCode.ActorSpawnSnapshot, _snapshotPayload);
                _sent = true;
                return true;
            }

            // 2) Incremental spawns.
            if (_pending.Count > 0)
            {
                try
                {
                    var payload = MobaActorSpawnSnapshotCodec.Serialize(_pending.ToArray());
                    _pending.Clear();
                    snapshot = new WorldStateSnapshot((int)MobaOpCode.ActorSpawnSnapshot, payload);
                    return true;
                }
                catch
                {
                    // Keep pending entries to retry next frame.
                }
            }

            snapshot = default;
            return false;
        }

        public void Dispose()
        {
            _hasSnapshot = false;
            _sent = false;
            _snapshotPayload = null;
            _pending.Clear();
        }
    }
}
