using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateSync;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Rollback
{
    public sealed class MobaDerivedSkillRollbackProvider : IRollbackStateProvider, IMobaStateRecoveryProvider
    {
        public const int DefaultKey = 10016;
        private readonly MobaDerivedSkillService _derived;

        public MobaDerivedSkillRollbackProvider(MobaDerivedSkillService derived)
        {
            _derived = derived ?? throw new ArgumentNullException(nameof(derived));
        }

        public int Key => DefaultKey;
        public string Name => "DerivedSkills";
        public byte[] Export(FrameIndex frame) => ExportState(frame);
        public void Import(FrameIndex frame, byte[] payload) => ImportState(frame, payload);

        public byte[] ExportState(FrameIndex frame)
        {
            var snapshot = _derived.CaptureRollbackSnapshot();
            var source = snapshot.Entries ?? Array.Empty<MobaDerivedSkillLinkSnapshot>();
            var entries = new MobaDerivedSkillRollbackEntry[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                var retain = source[i].Retain;
                entries[i] = new MobaDerivedSkillRollbackEntry(
                    source[i].ChildRuntimeId,
                    source[i].Depth,
                    retain.RetainId,
                    retain.Runtime.RuntimeId,
                    retain.Runtime.Generation,
                    retain.Runtime.RootTraceContextId,
                    (int)retain.Child.Kind,
                    retain.Child.ChildId,
                    retain.Child.TraceContextId,
                    retain.Child.ConfigId);
            }
            return MemoryPackSerializer.Serialize(new MobaDerivedSkillRollbackPayload(1, entries));
        }

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                _derived.RestoreRollbackSnapshot(default);
                return;
            }
            var state = MemoryPackSerializer.Deserialize<MobaDerivedSkillRollbackPayload>(payload);
            if (state.Version != 1) throw new InvalidOperationException($"Unsupported derived skill rollback payload version '{state.Version}'.");
            var source = state.Entries ?? Array.Empty<MobaDerivedSkillRollbackEntry>();
            var entries = new MobaDerivedSkillLinkSnapshot[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                var value = source[i];
                var parent = new MobaSkillCastRuntimeHandle(value.ParentRuntimeId, value.ParentGeneration, value.ParentTraceContextId);
                var child = new MobaSkillRuntimeChildRef(
                    (MobaSkillRuntimeChildKind)value.ChildKind,
                    value.ChildRuntimeId,
                    value.ChildTraceContextId,
                    value.ChildConfigId);
                var retain = new MobaSkillRuntimeRetainHandle(value.RetainId, in parent, in child);
                entries[i] = new MobaDerivedSkillLinkSnapshot(value.ChildRuntimeId, retain, value.Depth);
            }
            _derived.RestoreRollbackSnapshot(new MobaDerivedSkillServiceSnapshot(entries));
        }

        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            var payload = ExportState(frame);
            hash.AddInt(Key);
            hash.AddInt(payload.Length);
            for (var i = 0; i < payload.Length; i++) hash.AddByte(payload[i]);
        }
    }

    [MemoryPackable]
    internal readonly partial struct MobaDerivedSkillRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly MobaDerivedSkillRollbackEntry[] Entries;
        [MemoryPackConstructor]
        public MobaDerivedSkillRollbackPayload(int version, MobaDerivedSkillRollbackEntry[] entries)
        { Version = version; Entries = entries ?? Array.Empty<MobaDerivedSkillRollbackEntry>(); }
    }

    [MemoryPackable]
    internal readonly partial struct MobaDerivedSkillRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly long ChildRuntimeId;
        [MemoryPackOrder(1)] public readonly int Depth;
        [MemoryPackOrder(2)] public readonly long RetainId;
        [MemoryPackOrder(3)] public readonly long ParentRuntimeId;
        [MemoryPackOrder(4)] public readonly int ParentGeneration;
        [MemoryPackOrder(5)] public readonly long ParentTraceContextId;
        [MemoryPackOrder(6)] public readonly int ChildKind;
        [MemoryPackOrder(7)] public readonly long ChildId;
        [MemoryPackOrder(8)] public readonly long ChildTraceContextId;
        [MemoryPackOrder(9)] public readonly int ChildConfigId;

        public MobaDerivedSkillRollbackEntry(
            long childRuntimeId, int depth, long retainId,
            long parentRuntimeId, int parentGeneration, long parentTraceContextId,
            int childKind, long childId, long childTraceContextId, int childConfigId)
        {
            ChildRuntimeId = childRuntimeId; Depth = depth; RetainId = retainId;
            ParentRuntimeId = parentRuntimeId; ParentGeneration = parentGeneration; ParentTraceContextId = parentTraceContextId;
            ChildKind = childKind; ChildId = childId; ChildTraceContextId = childTraceContextId; ChildConfigId = childConfigId;
        }
    }
}
