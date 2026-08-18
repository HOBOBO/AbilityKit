using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Ability.Behavior;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>
    /// Restores the controller selection and provenance of each actor.
    /// The BehaviorRuntime instance id is deliberately not serialized: runtime ids are local
    /// allocations, so a rollback rebuilds the controller through MobaBrainService instead.
    /// </summary>
    public sealed class MobaBrainRollbackProvider : IRollbackStateProvider
    {
        public const int DefaultKey = 10006;

        private readonly MobaActorRegistry _actors;
        private readonly MobaBrainService _brains;

        public MobaBrainRollbackProvider(MobaActorRegistry actors, MobaBrainService brains)
        {
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _brains = brains ?? throw new ArgumentNullException(nameof(brains));
        }

        public int Key => DefaultKey;

        public byte[] Export(FrameIndex frame)
        {
            var entries = new List<MobaBrainRollbackEntry>(16);
            foreach (var pair in _actors.Entries)
            {
                var actor = pair.Value;
                if (actor == null) continue;
                if (!actor.hasActorBrain)
                {
                    entries.Add(new MobaBrainRollbackEntry(pair.Key, false, 0, 0, 0, 0));
                    continue;
                }

                var brain = actor.actorBrain;
                var snapshotType = string.Empty;
                byte[] behaviorSnapshot = null;
                if (brain.BehaviorInstanceId > 0
                    && _brains.TryGetBehavior(brain.BehaviorInstanceId, out var runtime)
                    && runtime?.Decision is IBehaviorRuntimeSnapshot snapshot)
                {
                    try
                    {
                        snapshotType = snapshot.SnapshotType ?? string.Empty;
                        behaviorSnapshot = snapshot.CaptureSnapshot();
                    }
                    catch
                    {
                        snapshotType = string.Empty;
                        behaviorSnapshot = null;
                    }
                }

                entries.Add(new MobaBrainRollbackEntry(
                    pair.Key,
                    true,
                    brain.BrainId,
                    brain.OwnerActorId,
                    brain.SourceKind,
                    brain.SourceId,
                    snapshotType,
                    behaviorSnapshot));
            }

            entries.Sort((left, right) => left.ActorId.CompareTo(right.ActorId));
            return MemoryPackSerializer.Serialize(new MobaBrainRollbackPayload(2, entries.ToArray()));
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0) return;

            MobaBrainRollbackPayload snapshot;
            try
            {
                snapshot = MemoryPackSerializer.Deserialize<MobaBrainRollbackPayload>(payload);
            }
            catch (MemoryPackSerializationException)
            {
                var legacy = MemoryPackSerializer.Deserialize<MobaBrainRollbackPayloadV1>(payload);
                var legacyEntries = legacy.Entries ?? Array.Empty<MobaBrainRollbackEntryV1>();
                var converted = new MobaBrainRollbackEntry[legacyEntries.Length];
                for (var i = 0; i < legacyEntries.Length; i++)
                {
                    var entry = legacyEntries[i];
                    converted[i] = new MobaBrainRollbackEntry(
                        entry.ActorId, entry.HasBrain, entry.BrainId, entry.OwnerActorId,
                        entry.SourceKind, entry.SourceId);
                }
                snapshot = new MobaBrainRollbackPayload(1, converted);
            }
            if (snapshot.Version != 1 && snapshot.Version != 2)
                throw new InvalidOperationException($"Unsupported Brain rollback payload version '{snapshot.Version}'.");

            var entries = snapshot.Entries ?? Array.Empty<MobaBrainRollbackEntry>();
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (!_actors.TryGet(entry.ActorId, out var actor) || actor == null) continue;

                if (!entry.HasBrain)
                {
                    if (actor.hasActorBrain) _brains.DeactivateBrain(actor);
                    continue;
                }

                // Runtime ids are local allocations, so recreate the controller first. A decision
                // that implements IBehaviorRuntimeSnapshot can then restore its own execution state.
                if (_brains.ActivateBrain(actor, entry.BrainId, entry.SourceKind, entry.SourceId)
                    && actor.hasActorBrain
                    && actor.actorBrain.OwnerActorId != entry.OwnerActorId)
                {
                    actor.ReplaceActorBrain(
                        actor.actorBrain.BrainId,
                        entry.OwnerActorId,
                        actor.actorBrain.SourceKind,
                        actor.actorBrain.SourceId,
                        actor.actorBrain.BehaviorInstanceId);
                }

                if (snapshot.Version >= 2 && entry.BehaviorSnapshot != null
                    && entry.BehaviorSnapshot.Length > 0)
                {
                    _brains.TryRestoreBehaviorSnapshot(actor, entry.BehaviorSnapshotType, entry.BehaviorSnapshot);
                }
            }
        }

        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            var payload = Export(frame);
            hash.AddInt(Key);
            hash.AddInt(payload.Length);
            for (var i = 0; i < payload.Length; i++) hash.AddByte(payload[i]);
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaBrainRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly MobaBrainRollbackEntry[] Entries;

        [MemoryPackConstructor]
        public MobaBrainRollbackPayload(int version, MobaBrainRollbackEntry[] entries)
        {
            Version = version;
            Entries = entries;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaBrainRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly int ActorId;
        [MemoryPackOrder(1)] public readonly bool HasBrain;
        [MemoryPackOrder(2)] public readonly int BrainId;
        [MemoryPackOrder(3)] public readonly int OwnerActorId;
        [MemoryPackOrder(4)] public readonly int SourceKind;
        [MemoryPackOrder(5)] public readonly int SourceId;
        [MemoryPackOrder(6)] public readonly string BehaviorSnapshotType;
        [MemoryPackOrder(7)] public readonly byte[] BehaviorSnapshot;

        public MobaBrainRollbackEntry(
            int actorId,
            bool hasBrain,
            int brainId,
            int ownerActorId,
            int sourceKind,
            int sourceId,
            string behaviorSnapshotType = "",
            byte[] behaviorSnapshot = null)
        {
            ActorId = actorId;
            HasBrain = hasBrain;
            BrainId = brainId;
            OwnerActorId = ownerActorId;
            SourceKind = sourceKind;
            SourceId = sourceId;
            BehaviorSnapshotType = behaviorSnapshotType ?? "";
            BehaviorSnapshot = behaviorSnapshot;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaBrainRollbackPayloadV1
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly MobaBrainRollbackEntryV1[] Entries;

        [MemoryPackConstructor]
        public MobaBrainRollbackPayloadV1(int version, MobaBrainRollbackEntryV1[] entries)
        {
            Version = version;
            Entries = entries;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaBrainRollbackEntryV1
    {
        [MemoryPackOrder(0)] public readonly int ActorId;
        [MemoryPackOrder(1)] public readonly bool HasBrain;
        [MemoryPackOrder(2)] public readonly int BrainId;
        [MemoryPackOrder(3)] public readonly int OwnerActorId;
        [MemoryPackOrder(4)] public readonly int SourceKind;
        [MemoryPackOrder(5)] public readonly int SourceId;

        public MobaBrainRollbackEntryV1(
            int actorId,
            bool hasBrain,
            int brainId,
            int ownerActorId,
            int sourceKind,
            int sourceId)
        {
            ActorId = actorId;
            HasBrain = hasBrain;
            BrainId = brainId;
            OwnerActorId = ownerActorId;
            SourceKind = sourceKind;
            SourceId = sourceId;
        }
    }
}
