using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Logging;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Services
{
    internal readonly struct MobaPendingActorSpawnEntry
    {
        public MobaPendingActorSpawnEntry(long sequence, in MobaActorSpawnSnapshotEntry entry)
        {
            Sequence = sequence;
            Entry = entry;
        }

        public long Sequence { get; }
        public MobaActorSpawnSnapshotEntry Entry { get; }
    }

    internal sealed class MobaActorSpawnPendingRollbackState
    {
        public MobaActorSpawnPendingRollbackState(FrameIndex frame, long nextSequence,
            MobaPendingActorSpawnEntry[] entries)
        {
            Frame = frame;
            NextSequence = nextSequence;
            Entries = entries ?? Array.Empty<MobaPendingActorSpawnEntry>();
        }

        public FrameIndex Frame { get; }
        public long NextSequence { get; }
        public MobaPendingActorSpawnEntry[] Entries { get; }
    }

    [MobaSnapshotEmitter(20)]
    [WorldService(typeof(MobaActorSpawnSnapshotService))]
    public sealed class MobaActorSpawnSnapshotService : IWorldStateSnapshotProvider, IMobaSnapshotEmitter
    {
        private bool _hasSnapshot;
        private bool _sent;
        private byte[] _snapshotPayload;

        private FrameIndex _lastFrame;
        private readonly MobaSnapshotBuffer<MobaPendingActorSpawnEntry> _pending = new MobaSnapshotBuffer<MobaPendingActorSpawnEntry>(64, 512);
        private long _nextPendingSequence = 1;

        public MobaActorSpawnSnapshotService()
        {
            _lastFrame = new FrameIndex(-999999);
        }

        public void PublishSpawnPayload(byte[] payload)
        {
            _snapshotPayload = payload;
            _hasSnapshot = payload != null && payload.Length > 0;
            _sent = false;
        }

        public void Enqueue(in MobaActorSpawnSnapshotEntry entry)
        {
            if (entry.NetId <= 0) return;
            if (_nextPendingSequence <= 0 || _nextPendingSequence == long.MaxValue)
                throw new InvalidOperationException("Actor spawn pending sequence exhausted.");
            _pending.Add(new MobaPendingActorSpawnEntry(_nextPendingSequence++, in entry));
        }

        public bool CancelPendingActorSpawn(int actorId)
        {
            if (actorId <= 0) return false;
            var items = _pending.Items;
            var removed = false;
            for (var i = items.Count - 1; i >= 0; i--)
            {
                if (items[i].Entry.NetId != actorId) continue;
                items.RemoveAt(i);
                removed = true;
            }
            return removed;
        }

        /// <summary>
        /// Builds a dynamic spawn entry from the authoritative registered Actor.
        /// Callers provide only the protocol kind/code and optional owner; position
        /// and entity version must come from the same logical entity instance.
        /// </summary>
        public bool TryEnqueueActorSpawn(
            MobaActorRegistry actors,
            int actorId,
            SpawnEntityKind kind,
            int code,
            int ownerNetId = 0)
        {
            if (actors == null || actorId <= 0 || code <= 0 || ownerNetId < 0 ||
                (kind != SpawnEntityKind.Character &&
                 kind != SpawnEntityKind.Projectile &&
                 kind != SpawnEntityKind.Minion &&
                 kind != SpawnEntityKind.Summon &&
                 kind != SpawnEntityKind.Building) ||
                !actors.TryGet(actorId, out var actor) || actor == null || !actor.hasTransform)
            {
                return false;
            }

            var position = actor.transform.Value.Position;
            var entry = new MobaActorSpawnSnapshotEntry
            {
                NetId = actorId,
                Kind = (int)kind,
                Code = code,
                OwnerNetId = ownerNetId,
                X = position.X,
                Y = position.Y,
                Z = position.Z,
                EntityVersion = actors.GetEntityVersion(actorId),
            };
            if (entry.EntityVersion <= 0) return false;

            Enqueue(in entry);
            return true;
        }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            if (_hasSnapshot && !_sent)
            {
                if (frame.Value == _lastFrame.Value)
                {
                    snapshot = default;
                    return false;
                }

                snapshot = new WorldStateSnapshot(AbilityKit.Protocol.Moba.MobaOpCodes.Snapshot.ActorSpawn, _snapshotPayload);
                _sent = true;
                _lastFrame = frame;
                return true;
            }

            if (_pending.Count > 0)
            {
                if (frame.Value == _lastFrame.Value)
                {
                    snapshot = default;
                    return false;
                }

                try
                {
                    var pending = _pending.ToArrayAndTrim();
                    var entries = new MobaActorSpawnSnapshotEntry[pending.Length];
                    for (var i = 0; i < pending.Length; i++) entries[i] = pending[i].Entry;
                    var payload = MobaActorSpawnSnapshotCodec.Serialize(entries);
                    _pending.Clear();
                    snapshot = new WorldStateSnapshot(AbilityKit.Protocol.Moba.MobaOpCodes.Snapshot.ActorSpawn, payload);
                    _lastFrame = frame;
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[MobaActorSpawnSnapshotService] serialize pending spawn snapshot failed (frame={frame.Value}, pending={_pending.Count})");
                }
            }

            snapshot = default;
            return false;
        }

        internal MobaActorSpawnPendingRollbackState CapturePendingRollbackState(FrameIndex frame)
        {
            return new MobaActorSpawnPendingRollbackState(frame, _nextPendingSequence,
                _pending.ToArrayAndTrim());
        }

        internal void RestorePendingRollbackState(MobaActorSpawnPendingRollbackState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.NextSequence <= 0) throw new InvalidOperationException("Invalid actor spawn pending sequence.");
            _pending.Clear();
            _pending.AddRange(new List<MobaPendingActorSpawnEntry>(state.Entries));
            _nextPendingSequence = state.NextSequence;
        }

        public void Dispose()
        {
            _hasSnapshot = false;
            _sent = false;
            _snapshotPayload = null;
            _pending.Clear();
            _nextPendingSequence = 1;
            _lastFrame = new FrameIndex(-999999);
        }
    }
}
