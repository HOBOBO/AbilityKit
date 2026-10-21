using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Logging;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Services
{
    [MobaSnapshotEmitter(20)]
    [WorldService(typeof(MobaActorSpawnSnapshotService))]
    public sealed class MobaActorSpawnSnapshotService : IWorldStateSnapshotProvider, IMobaSnapshotEmitter
    {
        private bool _hasSnapshot;
        private bool _sent;
        private byte[] _snapshotPayload;

        private FrameIndex _lastFrame;
        private readonly MobaSnapshotBuffer<MobaActorSpawnSnapshotEntry> _pending = new MobaSnapshotBuffer<MobaActorSpawnSnapshotEntry>(64, 512);

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
            _pending.Add(entry);
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
                 kind != SpawnEntityKind.Summon) ||
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
                    var entries = _pending.ToArrayAndTrim();
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

        public void Dispose()
        {
            _hasSnapshot = false;
            _sent = false;
            _snapshotPayload = null;
            _pending.Clear();
            _lastFrame = new FrameIndex(-999999);
        }
    }
}
