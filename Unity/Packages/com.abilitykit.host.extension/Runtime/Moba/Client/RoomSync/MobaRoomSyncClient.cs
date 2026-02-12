using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Host.Extensions.Moba.RoomSync
{
    public sealed class MobaRoomSyncClient : IMobaRoomSyncClient
    {
        public int LastSeenRevision { get; private set; }

        public int LastAckClientSeq { get; private set; }

        public MobaRoomSnapshotMessage LastSnapshotMessage { get; private set; }

        public bool NeedSnapshot { get; private set; }

        public int MaxDeltaRevisionGap { get; set; }

        private readonly Queue<MobaRoomChangedMessage> _pendingDeltas = new Queue<MobaRoomChangedMessage>();

        public MobaRoomSyncClient()
        {
            LastSeenRevision = 0;
            LastAckClientSeq = 0;
            LastSnapshotMessage = default;

            NeedSnapshot = false;
            MaxDeltaRevisionGap = 1;
        }

        public void ApplySnapshot(in MobaRoomSnapshotMessage snapshot)
        {
            var rev = snapshot.Snapshot.Revision;
            if (rev >= LastSeenRevision)
            {
                LastSeenRevision = rev;
                LastSnapshotMessage = snapshot;

                NeedSnapshot = false;
                _pendingDeltas.Clear();
            }
        }

        public void ApplyCommandResult(in MobaRoomCommandResultMessage result)
        {
            var rev = result.Result.NewRevision;
            if (rev > 0 && rev > LastSeenRevision) LastSeenRevision = rev;
        }

        public void ApplyDelta(in MobaRoomChangedMessage delta)
        {
            var rev = delta.Revision;
            if (rev <= 0) return;

            var prev = LastSeenRevision;
            if (prev > 0)
            {
                var maxGap = MaxDeltaRevisionGap;
                if (maxGap < 1) maxGap = 1;

                if (rev > prev + maxGap) NeedSnapshot = true;
            }

            if (rev < prev) return;

            if (rev > LastSeenRevision) LastSeenRevision = rev;
            _pendingDeltas.Enqueue(delta);
        }

        public int PendingDeltaCount => _pendingDeltas.Count;

        public bool TryDequeueDelta(out MobaRoomChangedMessage delta)
        {
            if (_pendingDeltas.Count > 0)
            {
                delta = _pendingDeltas.Dequeue();
                return true;
            }

            delta = default;
            return false;
        }

        public bool TryBuildRequestSnapshot(string clientId, out MobaRoomRequestSnapshotMessage request)
        {
            if (!NeedSnapshot)
            {
                request = default;
                return false;
            }

            if (string.IsNullOrEmpty(clientId)) throw new ArgumentNullException(nameof(clientId));

            request = new MobaRoomRequestSnapshotMessage(clientId, LastSeenRevision);
            return true;
        }

        public void SetLastAckClientSeq(int lastAckClientSeq)
        {
            if (lastAckClientSeq > LastAckClientSeq) LastAckClientSeq = lastAckClientSeq;
        }

        public MobaRoomHello BuildHello(string clientId)
        {
            if (string.IsNullOrEmpty(clientId)) throw new ArgumentNullException(nameof(clientId));
            return new MobaRoomHello(clientId, LastSeenRevision, LastAckClientSeq);
        }
    }
}
