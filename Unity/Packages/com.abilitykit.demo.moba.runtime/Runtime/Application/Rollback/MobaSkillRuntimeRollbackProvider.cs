using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Demo.Moba.Components;
using MemoryPack;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Rollback
{
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaSkillRuntimeRollbackProvider : IRollbackStateProvider, IRollbackStatePreflightProvider, IMobaStateRecoveryProvider
    {
        public const int DefaultKey = 10012;
        private const int LocalPayloadVersion = 1;
        private const int RuntimePayloadVersion = 2;
        private readonly MobaSkillCastRuntimeService _runtimes;

        public MobaSkillRuntimeRollbackProvider(MobaSkillCastRuntimeService runtimes)
        {
            _runtimes = runtimes ?? throw new ArgumentNullException(nameof(runtimes));
        }

        public int Key => DefaultKey;
        public string Name => "SkillRuntime";
        public byte[] Export(FrameIndex frame)
        {
            var contexts = _runtimes.ExecutionContextRegistry;
            var state = _runtimes.CaptureRollbackSnapshot();
            var contextNodes = CaptureExecutionContexts(contexts, in state);
            return MemoryPackSerializer.Serialize(new MobaSkillRuntimeLocalRollbackPayload(
                LocalPayloadVersion,
                SerializeRuntimeState(in state),
                contextNodes,
                contexts != null ? contexts.NextContextId : 0L));
        }

        public void ValidateImport(FrameIndex frame, byte[] payload)
        {
            var state = ReadLocalRollback(payload);
            var runtimeState = MemoryPackSerializer.Deserialize<MobaSkillRuntimeRollbackPayload>(state.RuntimeState);
            if (runtimeState.Version != RuntimePayloadVersion)
                throw new InvalidOperationException("Unsupported skill runtime state in local rollback payload.");

            if (state.ExecutionContextNodes != null)
            {
                var contexts = _runtimes.ExecutionContextRegistry ??
                    throw new InvalidOperationException("Skill rollback execution-context registry is unavailable.");
                contexts.ValidatePredictionRetraction(state.ExecutionContextNextId);
                var contextNodes = ToExecutionContextNodes(state.ExecutionContextNodes);
                ValidateExecutionContextScope(contexts, in runtimeState, contextNodes, state.ExecutionContextNextId);
                contexts.ValidateLifecycleRestore(contextNodes);
            }
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            var state = ReadLocalRollback(payload);
            ValidateImport(frame, payload);
            ImportState(frame, state.RuntimeState);

            var contextNodes = ToExecutionContextNodes(state.ExecutionContextNodes);
            if (state.ExecutionContextNodes != null)
            {
                _runtimes.ExecutionContextRegistry.RetractPredictionForRestore(state.ExecutionContextNextId);
                _runtimes.ExecutionContextRegistry.RestoreLifecycle(contextNodes);
            }
        }

        private static MobaSkillRuntimeLocalRollbackPayload ReadLocalRollback(byte[] payload)
        {
            if (payload == null || payload.Length == 0) throw new InvalidOperationException("Missing local skill rollback payload.");
            var state = MemoryPackSerializer.Deserialize<MobaSkillRuntimeLocalRollbackPayload>(payload);
            if (state.Version != LocalPayloadVersion ||
                state.RuntimeState == null || state.RuntimeState.Length == 0 ||
                ((state.ExecutionContextNodes == null && state.ExecutionContextNextId != 0L) ||
                 (state.ExecutionContextNodes != null && state.ExecutionContextNextId <= 0L)))
                throw new InvalidOperationException("Unsupported local skill rollback payload.");
            return state;
        }

        private static MobaSkillExecutionContextRollbackEntry[] CaptureExecutionContexts(
            MobaExecutionContextRegistry contexts,
            in MobaSkillCastRuntimeServiceSnapshot state)
        {
            if (contexts == null) return null;
            var nodes = new List<MobaSkillExecutionContextRollbackEntry>();
            var ids = new HashSet<long>();
            foreach (var runtime in state.Runtimes ?? Array.Empty<MobaSkillCastRuntimeSnapshot>())
            {
                CaptureRoot(runtime);
                foreach (var child in runtime.Children ?? Array.Empty<MobaSkillRuntimeChildRef>())
                    CaptureChild(runtime.RootContextId, in child);
            }
            nodes.Sort((left, right) => left.ContextId.CompareTo(right.ContextId));
            return nodes.ToArray();

            void CaptureRoot(MobaSkillCastRuntimeSnapshot runtime)
            {
                if (runtime.RootContextId == 0L || !ids.Add(runtime.RootContextId)) return;
                if (!contexts.TryGet(runtime.RootContextId, out var node))
                    throw new InvalidOperationException(
                        $"Skill runtime execution context {runtime.RootContextId} is missing at rollback capture.");
                if (node.Kind != MobaExecutionKind.SkillCast || node.ConfigId != runtime.SkillId)
                    throw new InvalidOperationException(
                        $"Skill runtime execution context {runtime.RootContextId} does not match skill {runtime.SkillId}.");
                nodes.Add(new MobaSkillExecutionContextRollbackEntry(in node));
            }

            void CaptureChild(long expectedRootId, in MobaSkillRuntimeChildRef child)
            {
                if (child.ContextId == 0L || !ids.Add(child.ContextId)) return;
                if (!contexts.TryGet(child.ContextId, out var node))
                    throw new InvalidOperationException(
                        $"Skill child execution context {child.ContextId} is missing at rollback capture.");
                if (node.RootContextId != expectedRootId || !MatchesChild(in node, in child))
                    throw new InvalidOperationException(
                        $"Skill child execution context {child.ContextId} does not match its runtime child reference.");
                nodes.Add(new MobaSkillExecutionContextRollbackEntry(in node));
            }
        }

        private static void ValidateExecutionContextScope(
            MobaExecutionContextRegistry contexts,
            in MobaSkillRuntimeRollbackPayload runtimeState,
            MobaExecutionContextNode[] nodes,
            long nextContextId)
        {
            var expectedIds = new HashSet<long>();
            foreach (var runtime in runtimeState.Runtimes ?? Array.Empty<MobaSkillRuntimeRollbackEntry>())
            {
                AddRoot(runtime);
                foreach (var child in runtime.Children ?? Array.Empty<MobaSkillRuntimeChildRollbackEntry>())
                    AddChild(runtime.RootContextId, in child);
            }

            foreach (var node in nodes)
            {
                if (node.ContextId >= nextContextId || !expectedIds.Remove(node.ContextId))
                    throw new InvalidOperationException(
                        $"Execution-context node {node.ContextId} does not match local skill rollback scope.");
            }
            if (expectedIds.Count != 0)
                throw new InvalidOperationException("Local skill rollback is missing referenced execution-context nodes.");

            void AddRoot(MobaSkillRuntimeRollbackEntry runtime)
            {
                if (runtime.RootContextId == 0L) return;
                if (!contexts.TryGet(runtime.RootContextId, out var node) ||
                    node.Kind != MobaExecutionKind.SkillCast || node.ConfigId != runtime.SkillId)
                    throw new InvalidOperationException(
                        $"Skill runtime execution context {runtime.RootContextId} is missing or does not match skill {runtime.SkillId}.");
                expectedIds.Add(runtime.RootContextId);
            }

            void AddChild(long expectedRootId, in MobaSkillRuntimeChildRollbackEntry child)
            {
                if (child.ContextId == 0L) return;
                if (!contexts.TryGet(child.ContextId, out var node) ||
                    node.RootContextId != expectedRootId || !MatchesChild(in node, in child))
                    throw new InvalidOperationException(
                        $"Skill child execution context {child.ContextId} is missing or does not match its runtime child reference.");
                expectedIds.Add(child.ContextId);
            }
        }

        private static bool MatchesChild(in MobaExecutionContextNode node, in MobaSkillRuntimeChildRef child)
        {
            return MatchesChild(node.Kind, node.ConfigId, child.Kind, child.ConfigId);
        }

        private static bool MatchesChild(in MobaExecutionContextNode node, in MobaSkillRuntimeChildRollbackEntry child)
        {
            return MatchesChild(node.Kind, node.ConfigId, (MobaSkillRuntimeChildKind)child.Kind, child.ConfigId);
        }

        private static bool MatchesChild(
            MobaExecutionKind executionKind,
            int executionConfigId,
            MobaSkillRuntimeChildKind childKind,
            int childConfigId)
        {
            if (childConfigId != 0 && executionConfigId != childConfigId) return false;
            switch (childKind)
            {
                case MobaSkillRuntimeChildKind.Effect:
                    return executionKind == MobaExecutionKind.EffectExecution || executionKind == MobaExecutionKind.EffectAction;
                case MobaSkillRuntimeChildKind.Buff:
                    return executionKind == MobaExecutionKind.BuffApply;
                case MobaSkillRuntimeChildKind.Projectile:
                case MobaSkillRuntimeChildKind.ProjectileLauncher:
                    return executionKind == MobaExecutionKind.ProjectileLaunch;
                case MobaSkillRuntimeChildKind.Area:
                    return executionKind == MobaExecutionKind.AreaSpawn;
                case MobaSkillRuntimeChildKind.Summon:
                    return executionKind == MobaExecutionKind.SummonSpawn;
                case MobaSkillRuntimeChildKind.Periodic:
                    return executionKind == MobaExecutionKind.BuffTick;
                case MobaSkillRuntimeChildKind.Presentation:
                    return executionKind == MobaExecutionKind.PresentationPlay;
                case MobaSkillRuntimeChildKind.SkillRuntime:
                    return executionKind == MobaExecutionKind.SkillCast;
                default:
                    return false;
            }
        }

        private static MobaExecutionContextNode[] ToExecutionContextNodes(
            MobaSkillExecutionContextRollbackEntry[] entries)
        {
            entries = entries ?? Array.Empty<MobaSkillExecutionContextRollbackEntry>();
            var nodes = new MobaExecutionContextNode[entries.Length];
            for (var i = 0; i < nodes.Length; i++) nodes[i] = entries[i].ToNode();
            return nodes;
        }

        public byte[] ExportState(FrameIndex frame)
        {
            var snapshot = _runtimes.CaptureRollbackSnapshot();
            return SerializeRuntimeState(in snapshot);
        }

        private static byte[] SerializeRuntimeState(in MobaSkillCastRuntimeServiceSnapshot snapshot)
        {
            var runtimes = new MobaSkillRuntimeRollbackEntry[snapshot.Runtimes.Length];
            for (var i = 0; i < runtimes.Length; i++) runtimes[i] = ToSerializable(in snapshot.Runtimes[i]);
            var retains = new MobaSkillRuntimeRetainRollbackEntry[snapshot.Retains.Length];
            for (var i = 0; i < retains.Length; i++) retains[i] = ToSerializable(in snapshot.Retains[i]);
            return MemoryPackSerializer.Serialize(new MobaSkillRuntimeRollbackPayload(
                RuntimePayloadVersion, snapshot.NextRuntimeId, snapshot.NextRetainId, snapshot.NextGeneration, runtimes, retains));
        }

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                _runtimes.RestoreRollbackSnapshot(new MobaSkillCastRuntimeServiceSnapshot(1L, 1L, 1, null, null));
                return;
            }
            var state = MemoryPackSerializer.Deserialize<MobaSkillRuntimeRollbackPayload>(payload);
            if (state.Version != RuntimePayloadVersion)
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
                children[i] = new MobaSkillRuntimeChildRollbackEntry((int)child.Kind, child.ChildId, child.ContextId, child.ConfigId);
            }
            var boards = new MobaSkillRuntimeBlackboardRollbackEntry[value.BlackboardEntries.Length];
            for (var i = 0; i < boards.Length; i++) boards[i] = ToSerializable(in value.BlackboardEntries[i]);
            return new MobaSkillRuntimeRollbackEntry(
                value.RuntimeId, value.Generation, value.RootContextId, value.SkillId, value.SkillSlot,
                value.SkillLevel, value.Sequence, value.CasterActorId, value.TargetActorId,
                value.AimPos.X, value.AimPos.Y, value.AimPos.Z, value.AimDir.X, value.AimDir.Y, value.AimDir.Z,
                (int)value.Stage, value.PipelineEnded, value.IsEnding, value.IsEnded, (int)value.EndReason, children, boards);
        }

        private static MobaSkillCastRuntimeSnapshot FromSerializable(in MobaSkillRuntimeRollbackEntry value)
        {
            var childSource = value.Children ?? Array.Empty<MobaSkillRuntimeChildRollbackEntry>();
            var children = new MobaSkillRuntimeChildRef[childSource.Length];
            for (var i = 0; i < children.Length; i++)
                children[i] = new MobaSkillRuntimeChildRef((MobaSkillRuntimeChildKind)childSource[i].Kind, childSource[i].ChildId, childSource[i].ContextId, childSource[i].ConfigId);
            var boardSource = value.BlackboardEntries ?? Array.Empty<MobaSkillRuntimeBlackboardRollbackEntry>();
            var boards = new MobaSkillRuntimeBlackboardSnapshotEntry[boardSource.Length];
            for (var i = 0; i < boards.Length; i++) boards[i] = FromSerializable(in boardSource[i]);
            return new MobaSkillCastRuntimeSnapshot(
                value.RuntimeId, value.Generation, value.RootContextId, value.SkillId, value.SkillSlot,
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
                value.RetainId, value.Runtime.RuntimeId, value.Runtime.Generation, value.Runtime.RootContextId,
                (int)value.Child.Kind, value.Child.ChildId, value.Child.ContextId, value.Child.ConfigId);
        }

        private static MobaSkillRuntimeRetainHandle FromSerializable(in MobaSkillRuntimeRetainRollbackEntry value)
        {
            var runtime = new MobaSkillCastRuntimeHandle(value.RuntimeId, value.Generation, value.RootContextId);
            var child = new MobaSkillRuntimeChildRef((MobaSkillRuntimeChildKind)value.ChildKind, value.ChildId, value.ChildContextId, value.ChildConfigId);
            return new MobaSkillRuntimeRetainHandle(value.RetainId, in runtime, in child);
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaSkillRuntimeLocalRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly byte[] RuntimeState;
        [MemoryPackOrder(2)] public readonly MobaSkillExecutionContextRollbackEntry[] ExecutionContextNodes;
        [MemoryPackOrder(3)] public readonly long ExecutionContextNextId;
        [MemoryPackConstructor]
        public MobaSkillRuntimeLocalRollbackPayload(
            int version,
            byte[] runtimeState,
            MobaSkillExecutionContextRollbackEntry[] executionContextNodes = null,
            long executionContextNextId = 0L)
        {
            Version = version;
            RuntimeState = runtimeState;
            ExecutionContextNodes = executionContextNodes;
            ExecutionContextNextId = executionContextNextId;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaSkillExecutionContextRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly long ContextId;
        [MemoryPackOrder(1)] public readonly long ParentContextId;
        [MemoryPackOrder(2)] public readonly long RootContextId;
        [MemoryPackOrder(3)] public readonly long OwnerContextId;
        [MemoryPackOrder(4)] public readonly int Kind;
        [MemoryPackOrder(5)] public readonly int ConfigId;
        [MemoryPackOrder(6)] public readonly int SourceActorId;
        [MemoryPackOrder(7)] public readonly int TargetActorId;
        [MemoryPackOrder(8)] public readonly int CreatedFrame;
        [MemoryPackOrder(9)] public readonly int EndedFrame;
        [MemoryPackOrder(10)] public readonly int EndReason;
        [MemoryPackOrder(11)] public readonly bool IsEnded;
        [MemoryPackOrder(12)] public readonly int TriggerId;
        [MemoryPackOrder(13)] public readonly int OriginKind;
        [MemoryPackOrder(14)] public readonly int OriginConfigId;
        [MemoryPackOrder(15)] public readonly int CastFlowId;
        [MemoryPackOrder(16)] public readonly uint CombatFlags;

        public MobaSkillExecutionContextRollbackEntry(
            long contextId,
            long parentContextId,
            long rootContextId,
            long ownerContextId,
            int kind,
            int configId,
            int sourceActorId,
            int targetActorId,
            int createdFrame,
            int endedFrame,
            int endReason,
            bool isEnded,
            int triggerId,
            int originKind,
            int originConfigId,
            int castFlowId,
            uint combatFlags = 0u)
        {
            ContextId = contextId;
            ParentContextId = parentContextId;
            RootContextId = rootContextId;
            OwnerContextId = ownerContextId;
            Kind = kind;
            ConfigId = configId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            CreatedFrame = createdFrame;
            EndedFrame = endedFrame;
            EndReason = endReason;
            IsEnded = isEnded;
            TriggerId = triggerId;
            OriginKind = originKind;
            OriginConfigId = originConfigId;
            CastFlowId = castFlowId;
            CombatFlags = combatFlags;
        }

        public MobaSkillExecutionContextRollbackEntry(in MobaExecutionContextNode node)
            : this(
                node.ContextId,
                node.ParentContextId,
                node.RootContextId,
                node.OwnerContextId,
                (int)node.Kind,
                node.ConfigId,
                node.SourceActorId,
                node.TargetActorId,
                node.CreatedFrame,
                node.EndedFrame,
                node.EndReason,
                node.IsEnded,
                node.TriggerId,
                (int)node.OriginKind,
                node.OriginConfigId,
                node.CastFlowId,
                (uint)node.CombatFlags)
        {
        }

        public MobaExecutionContextNode ToNode()
        {
            return new MobaExecutionContextNode(
                ContextId,
                ParentContextId,
                RootContextId,
                OwnerContextId,
                (MobaExecutionKind)Kind,
                ConfigId,
                SourceActorId,
                TargetActorId,
                CreatedFrame,
                EndedFrame,
                EndReason,
                IsEnded,
                TriggerId,
                (MobaExecutionKind)OriginKind,
                OriginConfigId,
                CastFlowId,
                (MobaCombatExecutionFlags)CombatFlags);
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
        [MemoryPackOrder(2)] public readonly long RootContextId; [MemoryPackOrder(3)] public readonly int SkillId;
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
        public MobaSkillRuntimeRollbackEntry(long runtimeId, int generation, long rootContextId, int skillId, int skillSlot, int skillLevel, int sequence, int casterActorId, int targetActorId, float aimX, float aimY, float aimZ, float dirX, float dirY, float dirZ, int stage, bool pipelineEnded, bool isEnding, bool isEnded, int endReason, MobaSkillRuntimeChildRollbackEntry[] children, MobaSkillRuntimeBlackboardRollbackEntry[] blackboardEntries)
        { RuntimeId=runtimeId;Generation=generation;RootContextId=rootContextId;SkillId=skillId;SkillSlot=skillSlot;SkillLevel=skillLevel;Sequence=sequence;CasterActorId=casterActorId;TargetActorId=targetActorId;AimX=aimX;AimY=aimY;AimZ=aimZ;DirX=dirX;DirY=dirY;DirZ=dirZ;Stage=stage;PipelineEnded=pipelineEnded;IsEnding=isEnding;IsEnded=isEnded;EndReason=endReason;Children=children;BlackboardEntries=blackboardEntries; }
    }

    [MemoryPackable]
    public readonly partial struct MobaSkillRuntimeChildRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly int Kind; [MemoryPackOrder(1)] public readonly long ChildId;
        [MemoryPackOrder(2)] public readonly long ContextId; [MemoryPackOrder(3)] public readonly int ConfigId;
        public MobaSkillRuntimeChildRollbackEntry(int kind, long childId, long contextId, int configId)
        { Kind=kind;ChildId=childId;ContextId=contextId;ConfigId=configId; }
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
        [MemoryPackOrder(2)] public readonly int Generation;
        [MemoryPackOrder(3)] public readonly long RootContextId;
        [MemoryPackOrder(4)] public readonly int ChildKind; [MemoryPackOrder(5)] public readonly long ChildId;
        [MemoryPackOrder(6)] public readonly long ChildContextId; [MemoryPackOrder(7)] public readonly int ChildConfigId;
        public MobaSkillRuntimeRetainRollbackEntry(long retainId,long runtimeId,int generation,long rootContextId,int childKind,long childId,long childContextId,int childConfigId)
        {RetainId=retainId;RuntimeId=runtimeId;Generation=generation;RootContextId=rootContextId;ChildKind=childKind;ChildId=childId;ChildContextId=childContextId;ChildConfigId=childConfigId;}
    }
}
