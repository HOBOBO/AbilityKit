using MemoryPack;
using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Core.Pooling;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateSync;

namespace AbilityKit.Demo.Moba.Rollback
{
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaActorTransformRollbackProvider : IRollbackStateProvider, IMobaStateRecoveryProvider
    {
        public const int DefaultKey = 10001;

        private static readonly ObjectPool<List<MobaActorTransformRollbackEntry>> s_entryListPool = Pools.GetPool(
            createFunc: () => new List<MobaActorTransformRollbackEntry>(16),
            onRelease: list => list.Clear(),
            defaultCapacity: 8,
            maxSize: 64,
            collectionCheck: false);

        private readonly MobaActorRegistry _registry;

        public MobaActorTransformRollbackProvider(MobaActorRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public int Key => DefaultKey;

        public string Name => "ActorTransform";

        public byte[] ExportState(FrameIndex frame)
        {
            return Export(frame);
        }

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            Import(frame, payload);
        }

        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            var entries = s_entryListPool.Get();
            try
            {
                foreach (var kv in _registry.Entries)
                {
                    var actorId = kv.Key;
                    var e = kv.Value;
                    if (e == null) continue;
                    if (!e.hasTransform) continue;
                    entries.Add(CreateEntry(actorId, e));
                }

                entries.Sort((a, b) => a.ActorId.CompareTo(b.ActorId));
                hash.AddInt(Key);
                hash.AddInt(entries.Count);

                for (int i = 0; i < entries.Count; i++)
                {
                    var it = entries[i];
                    AddEntryHash(it, ref hash);
                }
            }
            finally
            {
                s_entryListPool.Release(entries);
            }
        }

        public byte[] Export(FrameIndex frame)
        {
            var entries = s_entryListPool.Get();
            try
            {
                foreach (var kv in _registry.Entries)
                {
                    var actorId = kv.Key;
                    var e = kv.Value;
                    if (e == null) continue;
                    if (!e.hasTransform) continue;
                    entries.Add(CreateEntry(actorId, e));
                }

                entries.Sort((a, b) => a.ActorId.CompareTo(b.ActorId));
                var payloadEntries = entries.Count == 0 ? Array.Empty<MobaActorTransformRollbackEntry>() : entries.ToArray();
                return MemoryPackSerializer.Serialize(new MobaActorTransformRollbackPayload(3, payloadEntries));
            }
            finally
            {
                s_entryListPool.Release(entries);
            }
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0) return;

            var p = MemoryPackSerializer.Deserialize<MobaActorTransformRollbackPayload>(payload);
            if (p.Entries == null || p.Entries.Length == 0) return;

            for (int i = 0; i < p.Entries.Length; i++)
            {
                var it = p.Entries[i];
                if (_registry.TryGet(it.ActorId, out var e) && e != null)
                {
                    e.ReplaceTransform(it.Transform);
                    SyncMotionState(e, it.Transform);
                    if (p.Version >= 2)
                    {
                        SyncMoveInput(e, in it);
                    }
                    if (p.Version >= 3)
                    {
                        SyncMotionKinematics(e, in it);
                    }
                }
            }
        }

        private static MobaActorTransformRollbackEntry CreateEntry(int actorId, global::ActorEntity entity)
        {
            var hasMoveInput = entity.hasMoveInput;
            // v3: 捕获运动学状态——回滚前只恢复 Position/Forward 会把预测帧的 Velocity 残留进重模拟。
            var hasMotion = entity.hasMotion;
            var motionState = hasMotion ? entity.motion.State : default;
            return new MobaActorTransformRollbackEntry(
                actorId,
                entity.transform.Value,
                hasMoveInput,
                hasMoveInput ? entity.moveInput.Dx : 0f,
                hasMoveInput ? entity.moveInput.Dz : 0f,
                hasMotion,
                motionState.Velocity,
                motionState.Time);
        }

        private static void SyncMoveInput(
            global::ActorEntity entity,
            in MobaActorTransformRollbackEntry entry)
        {
            if (entry.HasMoveInput)
            {
                if (entity.hasMoveInput)
                {
                    entity.ReplaceMoveInput(entry.MoveDx, entry.MoveDz);
                }
                else
                {
                    entity.AddMoveInput(entry.MoveDx, entry.MoveDz);
                }
            }
            else if (entity.hasMoveInput)
            {
                entity.RemoveMoveInput();
            }
        }

        internal static void SyncMotionState(global::ActorEntity e, in Transform3 transform)
        {
            if (e == null || !e.hasMotion) return;

            var m = e.motion;
            var state = m.State;
            state.Position = transform.Position;
            state.Forward = transform.Forward;

            var output = m.Output;
            output.NewForward = state.Forward;

            e.ReplaceMotion(m.Pipeline, state, output, m.Solver, m.Policy, m.Events, m.Initialized, m.HitTriggerRuntime);
        }

        // v3: 恢复运动学状态（Velocity/Time）——与 SyncMotionState（Position/Forward）合成完整的 Motion 恢复。
        private static void SyncMotionKinematics(global::ActorEntity e, in MobaActorTransformRollbackEntry entry)
        {
            if (e == null || !e.hasMotion || !entry.HasMotionState) return;

            var m = e.motion;
            var state = m.State;
            state.Velocity = entry.MotionVelocity;
            state.Time = entry.MotionTime;
            e.ReplaceMotion(m.Pipeline, state, m.Output, m.Solver, m.Policy, m.Events, m.Initialized, m.HitTriggerRuntime);
        }

        private static void AddEntryHash(in MobaActorTransformRollbackEntry entry, ref MobaStateHashBuilder hash)
        {
            var t = entry.Transform;
            hash.AddInt(entry.ActorId);
            hash.AddFloat(t.Position.X);
            hash.AddFloat(t.Position.Y);
            hash.AddFloat(t.Position.Z);
            hash.AddFloat(t.Rotation.X);
            hash.AddFloat(t.Rotation.Y);
            hash.AddFloat(t.Rotation.Z);
            hash.AddFloat(t.Rotation.W);
            hash.AddFloat(t.Scale.X);
            hash.AddFloat(t.Scale.Y);
            hash.AddFloat(t.Scale.Z);
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaActorTransformRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly MobaActorTransformRollbackEntry[] Entries;

        [MemoryPackConstructor]
        public MobaActorTransformRollbackPayload(int version, MobaActorTransformRollbackEntry[] entries)
        {
            Version = version;
            Entries = entries;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaActorTransformRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly int ActorId;
        [MemoryPackOrder(1)] public readonly Transform3 Transform;
        [MemoryPackOrder(2)] public readonly bool HasMoveInput;
        [MemoryPackOrder(3)] public readonly float MoveDx;
        [MemoryPackOrder(4)] public readonly float MoveDz;
        [MemoryPackOrder(5)] public readonly bool HasMotionState;
        [MemoryPackOrder(6)] public readonly Vec3 MotionVelocity;
        [MemoryPackOrder(7)] public readonly float MotionTime;

        public MobaActorTransformRollbackEntry(
            int actorId,
            Transform3 transform,
            bool hasMoveInput,
            float moveDx,
            float moveDz,
            bool hasMotionState,
            Vec3 motionVelocity,
            float motionTime)
        {
            ActorId = actorId;
            Transform = transform;
            HasMoveInput = hasMoveInput;
            MoveDx = moveDx;
            MoveDz = moveDz;
            HasMotionState = hasMotionState;
            MotionVelocity = motionVelocity;
            MotionTime = motionTime;
        }

        public MobaActorTransformRollbackEntry(
            int actorId,
            Transform3 transform,
            bool hasMoveInput,
            float moveDx,
            float moveDz)
            : this(actorId, transform, hasMoveInput, moveDx, moveDz, false, default, 0f)
        {
        }

        public MobaActorTransformRollbackEntry(int actorId, Transform3 transform)
            : this(actorId, transform, false, 0f, 0f)
        {
        }
    }
}
