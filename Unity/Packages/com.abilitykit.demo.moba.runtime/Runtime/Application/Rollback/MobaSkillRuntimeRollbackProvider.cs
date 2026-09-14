using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Demo.Moba.Components;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Rollback
{
    public sealed class MobaSkillRuntimeRollbackProvider : IRollbackStateProvider, IMobaStateRecoveryProvider
    {
        public const int DefaultKey = 10012;
        private readonly MobaSkillCastRuntimeService _runtimes;

        public MobaSkillRuntimeRollbackProvider(MobaSkillCastRuntimeService runtimes)
        {
            _runtimes = runtimes ?? throw new ArgumentNullException(nameof(runtimes));
        }

        public int Key => DefaultKey;
        public string Name => "SkillRuntime";
        public byte[] Export(FrameIndex frame) => ExportState(frame);
        public void Import(FrameIndex frame, byte[] payload) => ImportState(frame, payload);

        public byte[] ExportState(FrameIndex frame)
        {
            var snapshot = _runtimes.CaptureRollbackSnapshot();
            var runtimes = new MobaSkillRuntimeRollbackEntry[snapshot.Runtimes.Length];
            for (var i = 0; i < runtimes.Length; i++) runtimes[i] = ToSerializable(in snapshot.Runtimes[i]);
            var retains = new MobaSkillRuntimeRetainRollbackEntry[snapshot.Retains.Length];
            for (var i = 0; i < retains.Length; i++) retains[i] = ToSerializable(in snapshot.Retains[i]);
            return MemoryPackSerializer.Serialize(new MobaSkillRuntimeRollbackPayload(
                2, snapshot.NextRuntimeId, snapshot.NextRetainId, snapshot.NextGeneration, runtimes, retains));
        }

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                _runtimes.RestoreRollbackSnapshot(new MobaSkillCastRuntimeServiceSnapshot(1L, 1L, 1, null, null));
                return;
            }
            var state = MemoryPackSerializer.Deserialize<MobaSkillRuntimeRollbackPayload>(payload);
            if (state.Version != 1 && state.Version != 2)
                throw new InvalidOperationException($"Unsupported skill runtime rollback payload version '{state.Version}'.");
            var source = state.Runtimes ?? Array.Empty<MobaSkillRuntimeRollbackEntry>();
            var runtimes = new MobaSkillCastRuntimeSnapshot[source.Length];
            for (var i = 0; i < source.Length; i++) runtimes[i] = FromSerializable(in source[i]);
            var retainSource = state.Retains ?? Array.Empty<MobaSkillRuntimeRetainRollbackEntry>();
            var retains = new MobaSkillRuntimeRetainHandle[retainSource.Length];
            for (var i = 0; i < retainSource.Length; i++) retains[i] = FromSerializable(in retainSource[i]);
            _runtimes.RestoreRollbackSnapshot(new MobaSkillCastRuntimeServiceSnapshot(
                state.NextRuntimeId, state.NextRetainId, state.NextGeneration, runtimes, retains));
        }

        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            var payload = ExportState(frame);
            hash.AddInt(Key);
            hash.AddInt(payload.Length);
            for (var i = 0; i < payload.Length; i++) hash.AddByte(payload[i]);
        }

        private static MobaSkillRuntimeRollbackEntry ToSerializable(in MobaSkillCastRuntimeSnapshot value)
        {
            var children = new MobaSkillRuntimeChildRollbackEntry[value.Children.Length];
            for (var i = 0; i < children.Length; i++)
            {
                var child = value.Children[i];
                children[i] = new MobaSkillRuntimeChildRollbackEntry((int)child.Kind, child.ChildId, child.TraceContextId, child.ConfigId);
            }
            var boards = new MobaSkillRuntimeBlackboardRollbackEntry[value.BlackboardEntries.Length];
            for (var i = 0; i < boards.Length; i++) boards[i] = ToSerializable(in value.BlackboardEntries[i]);
            return new MobaSkillRuntimeRollbackEntry(
                value.RuntimeId, value.Generation, value.RootTraceContextId, value.SkillId, value.SkillSlot,
                value.SkillLevel, value.Sequence, value.CasterActorId, value.TargetActorId,
                value.AimPos.X, value.AimPos.Y, value.AimPos.Z, value.AimDir.X, value.AimDir.Y, value.AimDir.Z,
                (int)value.Stage, value.PipelineEnded, value.IsEnding, value.IsEnded, (int)value.EndReason, children, boards);
        }

        private static MobaSkillCastRuntimeSnapshot FromSerializable(in MobaSkillRuntimeRollbackEntry value)
        {
            var childSource = value.Children ?? Array.Empty<MobaSkillRuntimeChildRollbackEntry>();
            var children = new MobaSkillRuntimeChildRef[childSource.Length];
            for (var i = 0; i < children.Length; i++)
                children[i] = new MobaSkillRuntimeChildRef((MobaSkillRuntimeChildKind)childSource[i].Kind, childSource[i].ChildId, childSource[i].TraceContextId, childSource[i].ConfigId);
            var boardSource = value.BlackboardEntries ?? Array.Empty<MobaSkillRuntimeBlackboardRollbackEntry>();
            var boards = new MobaSkillRuntimeBlackboardSnapshotEntry[boardSource.Length];
            for (var i = 0; i < boards.Length; i++) boards[i] = FromSerializable(in boardSource[i]);
            return new MobaSkillCastRuntimeSnapshot(
                value.RuntimeId, value.Generation, value.RootTraceContextId, value.SkillId, value.SkillSlot,
                value.SkillLevel, value.Sequence, value.CasterActorId, value.TargetActorId,
                new Vec3(value.AimX, value.AimY, value.AimZ), new Vec3(value.DirX, value.DirY, value.DirZ),
                (SkillCastStage)value.Stage, value.PipelineEnded, value.IsEnding, value.IsEnded,
                (MobaSkillRuntimeEndReason)value.EndReason, children, boards);
        }

        private static MobaSkillRuntimeBlackboardRollbackEntry ToSerializable(in MobaSkillRuntimeBlackboardSnapshotEntry entry)
        {
            var key = entry.Key;
            var value = entry.Value;
            return new MobaSkillRuntimeBlackboardRollbackEntry(
                key.Id, key.Name, (int)key.ValueKind, (int)key.Scope, (int)key.Flags, key.OwnerModuleId,
                entry.ScopeOwnerId, value.IntValue, value.LongValue, value.FloatValue, value.DoubleValue,
                value.BoolValue, value.StringValue, value.Vec3Value.X, value.Vec3Value.Y, value.Vec3Value.Z,
                entry.ActorIds, entry.ContextIds, entry.IsSnapshotCaptured);
        }

        private static MobaSkillRuntimeBlackboardSnapshotEntry FromSerializable(in MobaSkillRuntimeBlackboardRollbackEntry entry)
        {
            var kind = (MobaSkillRuntimeValueKind)entry.ValueKind;
            var key = new MobaSkillRuntimeBlackboardKey(entry.KeyId, entry.Name, kind,
                (MobaSkillRuntimeBlackboardScope)entry.Scope, (MobaSkillRuntimeBlackboardFlags)entry.Flags, entry.OwnerModuleId);
            var vector = new Vec3(entry.VecX, entry.VecY, entry.VecZ);
            var value = kind switch
            {
                MobaSkillRuntimeValueKind.Int => MobaSkillRuntimeValue.FromInt(entry.IntValue),
                MobaSkillRuntimeValueKind.ActorId => MobaSkillRuntimeValue.FromActorId(entry.IntValue),
                MobaSkillRuntimeValueKind.Long => MobaSkillRuntimeValue.FromLong(entry.LongValue),
                MobaSkillRuntimeValueKind.ContextId => MobaSkillRuntimeValue.FromContextId(entry.LongValue),
                MobaSkillRuntimeValueKind.Float => MobaSkillRuntimeValue.FromFloat(entry.FloatValue),
                MobaSkillRuntimeValueKind.Double => MobaSkillRuntimeValue.FromDouble(entry.DoubleValue),
                MobaSkillRuntimeValueKind.Bool => MobaSkillRuntimeValue.FromBool(entry.BoolValue),
                MobaSkillRuntimeValueKind.String => MobaSkillRuntimeValue.FromString(entry.StringValue),
                MobaSkillRuntimeValueKind.Vec3 => MobaSkillRuntimeValue.FromVec3(in vector),
                _ => default,
            };
            return new MobaSkillRuntimeBlackboardSnapshotEntry(
                in key, entry.ScopeOwnerId, in value, entry.ActorIds, entry.ContextIds, entry.IsSnapshotCaptured);
        }

        private static MobaSkillRuntimeRetainRollbackEntry ToSerializable(in MobaSkillRuntimeRetainHandle value)
        {
            return new MobaSkillRuntimeRetainRollbackEntry(
                value.RetainId, value.Runtime.RuntimeId, value.Runtime.Generation, value.Runtime.RootTraceContextId,
                (int)value.Child.Kind, value.Child.ChildId, value.Child.TraceContextId, value.Child.ConfigId);
        }

        private static MobaSkillRuntimeRetainHandle FromSerializable(in MobaSkillRuntimeRetainRollbackEntry value)
        {
            var runtime = new MobaSkillCastRuntimeHandle(value.RuntimeId, value.Generation, value.RootTraceContextId);
            var child = new MobaSkillRuntimeChildRef((MobaSkillRuntimeChildKind)value.ChildKind, value.ChildId, value.ChildTraceContextId, value.ChildConfigId);
            return new MobaSkillRuntimeRetainHandle(value.RetainId, in runtime, in child);
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaSkillRuntimeRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly long NextRuntimeId;
        [MemoryPackOrder(2)] public readonly long NextRetainId;
        [MemoryPackOrder(3)] public readonly int NextGeneration;
        [MemoryPackOrder(4)] public readonly MobaSkillRuntimeRollbackEntry[] Runtimes;
        [MemoryPackOrder(5)] public readonly MobaSkillRuntimeRetainRollbackEntry[] Retains;
        [MemoryPackConstructor]
        public MobaSkillRuntimeRollbackPayload(int version, long nextRuntimeId, long nextRetainId, int nextGeneration, MobaSkillRuntimeRollbackEntry[] runtimes, MobaSkillRuntimeRetainRollbackEntry[] retains)
        { Version = version; NextRuntimeId = nextRuntimeId; NextRetainId = nextRetainId; NextGeneration = nextGeneration; Runtimes = runtimes; Retains = retains; }
    }

    [MemoryPackable]
    public readonly partial struct MobaSkillRuntimeRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly long RuntimeId; [MemoryPackOrder(1)] public readonly int Generation;
        [MemoryPackOrder(2)] public readonly long RootTraceContextId; [MemoryPackOrder(3)] public readonly int SkillId;
        [MemoryPackOrder(4)] public readonly int SkillSlot; [MemoryPackOrder(5)] public readonly int SkillLevel;
        [MemoryPackOrder(6)] public readonly int Sequence; [MemoryPackOrder(7)] public readonly int CasterActorId;
        [MemoryPackOrder(8)] public readonly int TargetActorId; [MemoryPackOrder(9)] public readonly float AimX;
        [MemoryPackOrder(10)] public readonly float AimY; [MemoryPackOrder(11)] public readonly float AimZ;
        [MemoryPackOrder(12)] public readonly float DirX; [MemoryPackOrder(13)] public readonly float DirY;
        [MemoryPackOrder(14)] public readonly float DirZ; [MemoryPackOrder(15)] public readonly int Stage;
        [MemoryPackOrder(16)] public readonly bool PipelineEnded; [MemoryPackOrder(17)] public readonly bool IsEnding;
        [MemoryPackOrder(18)] public readonly bool IsEnded; [MemoryPackOrder(19)] public readonly int EndReason;
        [MemoryPackOrder(20)] public readonly MobaSkillRuntimeChildRollbackEntry[] Children;
        [MemoryPackOrder(21)] public readonly MobaSkillRuntimeBlackboardRollbackEntry[] BlackboardEntries;
        [MemoryPackConstructor]
        public MobaSkillRuntimeRollbackEntry(long runtimeId, int generation, long rootTraceContextId, int skillId, int skillSlot, int skillLevel, int sequence, int casterActorId, int targetActorId, float aimX, float aimY, float aimZ, float dirX, float dirY, float dirZ, int stage, bool pipelineEnded, bool isEnding, bool isEnded, int endReason, MobaSkillRuntimeChildRollbackEntry[] children, MobaSkillRuntimeBlackboardRollbackEntry[] blackboardEntries)
        { RuntimeId=runtimeId;Generation=generation;RootTraceContextId=rootTraceContextId;SkillId=skillId;SkillSlot=skillSlot;SkillLevel=skillLevel;Sequence=sequence;CasterActorId=casterActorId;TargetActorId=targetActorId;AimX=aimX;AimY=aimY;AimZ=aimZ;DirX=dirX;DirY=dirY;DirZ=dirZ;Stage=stage;PipelineEnded=pipelineEnded;IsEnding=isEnding;IsEnded=isEnded;EndReason=endReason;Children=children;BlackboardEntries=blackboardEntries; }
    }

    [MemoryPackable]
    public readonly partial struct MobaSkillRuntimeChildRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly int Kind; [MemoryPackOrder(1)] public readonly long ChildId;
        [MemoryPackOrder(2)] public readonly long TraceContextId; [MemoryPackOrder(3)] public readonly int ConfigId;
        public MobaSkillRuntimeChildRollbackEntry(int kind, long childId, long traceContextId, int configId)
        { Kind=kind;ChildId=childId;TraceContextId=traceContextId;ConfigId=configId; }
    }

    [MemoryPackable]
    public readonly partial struct MobaSkillRuntimeBlackboardRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly int KeyId; [MemoryPackOrder(1)] public readonly string Name;
        [MemoryPackOrder(2)] public readonly int ValueKind; [MemoryPackOrder(3)] public readonly int Scope;
        [MemoryPackOrder(4)] public readonly int Flags; [MemoryPackOrder(5)] public readonly int OwnerModuleId;
        [MemoryPackOrder(6)] public readonly long ScopeOwnerId; [MemoryPackOrder(7)] public readonly int IntValue;
        [MemoryPackOrder(8)] public readonly long LongValue; [MemoryPackOrder(9)] public readonly float FloatValue;
        [MemoryPackOrder(10)] public readonly double DoubleValue; [MemoryPackOrder(11)] public readonly bool BoolValue;
        [MemoryPackOrder(12)] public readonly string StringValue; [MemoryPackOrder(13)] public readonly float VecX;
        [MemoryPackOrder(14)] public readonly float VecY; [MemoryPackOrder(15)] public readonly float VecZ;
        [MemoryPackOrder(16)] public readonly int[] ActorIds; [MemoryPackOrder(17)] public readonly long[] ContextIds;
        [MemoryPackOrder(18)] public readonly bool IsSnapshotCaptured;
        [MemoryPackConstructor]
        public MobaSkillRuntimeBlackboardRollbackEntry(int keyId,string name,int valueKind,int scope,int flags,int ownerModuleId,long scopeOwnerId,int intValue,long longValue,float floatValue,double doubleValue,bool boolValue,string stringValue,float vecX,float vecY,float vecZ,int[] actorIds,long[] contextIds,bool isSnapshotCaptured)
        {KeyId=keyId;Name=name;ValueKind=valueKind;Scope=scope;Flags=flags;OwnerModuleId=ownerModuleId;ScopeOwnerId=scopeOwnerId;IntValue=intValue;LongValue=longValue;FloatValue=floatValue;DoubleValue=doubleValue;BoolValue=boolValue;StringValue=stringValue;VecX=vecX;VecY=vecY;VecZ=vecZ;ActorIds=actorIds;ContextIds=contextIds;IsSnapshotCaptured=isSnapshotCaptured;}
    }

    [MemoryPackable]
    public readonly partial struct MobaSkillRuntimeRetainRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly long RetainId; [MemoryPackOrder(1)] public readonly long RuntimeId;
        [MemoryPackOrder(2)] public readonly int Generation; [MemoryPackOrder(3)] public readonly long RootTraceContextId;
        [MemoryPackOrder(4)] public readonly int ChildKind; [MemoryPackOrder(5)] public readonly long ChildId;
        [MemoryPackOrder(6)] public readonly long ChildTraceContextId; [MemoryPackOrder(7)] public readonly int ChildConfigId;
        public MobaSkillRuntimeRetainRollbackEntry(long retainId,long runtimeId,int generation,long rootTraceContextId,int childKind,long childId,long childTraceContextId,int childConfigId)
        {RetainId=retainId;RuntimeId=runtimeId;Generation=generation;RootTraceContextId=rootTraceContextId;ChildKind=childKind;ChildId=childId;ChildTraceContextId=childTraceContextId;ChildConfigId=childConfigId;}
    }
}
