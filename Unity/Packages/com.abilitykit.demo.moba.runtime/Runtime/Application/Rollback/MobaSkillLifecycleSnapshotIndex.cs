using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Services;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>
    /// Detached evidence from a target-frame lifecycle checkpoint. It never consults or mutates
    /// live registries, so relation providers can use it during the coordinator preflight pass.
    /// </summary>
    internal sealed class MobaSkillLifecycleSnapshotIndex
    {
        private MobaSkillLifecycleSnapshotIndex(HashSet<int> actorIds,
            Dictionary<long, MobaExecutionContextNode> contexts,
            MobaSkillRuntimeRetainRollbackEntry[] retains)
        {
            ActorIds = actorIds;
            Contexts = contexts;
            Retains = retains;
        }

        internal HashSet<int> ActorIds { get; }
        internal Dictionary<long, MobaExecutionContextNode> Contexts { get; }
        internal MobaSkillRuntimeRetainRollbackEntry[] Retains { get; }

        internal static MobaSkillLifecycleSnapshotIndex Read(FrameIndex frame, byte[] payload)
        {
            var checkpoint = MobaSkillLifecycleCheckpointCodec.Decode<MobaSkillLifecycleCheckpoint>(payload);
            var sections = MobaSkillLifecycleCheckpointCodec.ReadSections(checkpoint);
            if (checkpoint.Frame != frame.Value)
                throw new InvalidOperationException("Lifecycle checkpoint frame differs from the requested frame.");

            var actorIds = new HashSet<int>();
            foreach (var actorId in checkpoint.ActorIds)
                if (actorId <= 0 || !actorIds.Add(actorId))
                    throw new InvalidOperationException("Lifecycle checkpoint contains a duplicate or invalid actor identity.");

            var execution = MobaSkillLifecycleCheckpointCodec.Decode<MobaExecutionRegistryCheckpoint>(sections["execution-contexts"]);
            if (execution.Version != 1 || execution.Nodes == null ||
                execution.NextContextId < MobaExecutionContextRegistry.FirstExecutionContextId)
                throw new InvalidOperationException("Invalid lifecycle execution registry checkpoint.");

            var contexts = new Dictionary<long, MobaExecutionContextNode>();
            var entries = MemoryPackSerializer.Deserialize<MobaSkillExecutionContextRollbackEntry[]>(execution.Nodes);
            if (entries == null) throw new InvalidOperationException("Execution nodes are missing.");
            foreach (var entry in entries)
            {
                if (entry.ContextId < MobaExecutionContextRegistry.FirstExecutionContextId ||
                    entry.ContextId >= execution.NextContextId || entry.Kind <= 0 ||
                    !Enum.IsDefined(typeof(MobaExecutionKind), (MobaExecutionKind)entry.Kind) ||
                    entry.CreatedFrame < 0 || entry.CreatedFrame > frame.Value || entry.EndedFrame < 0 ||
                    entry.IsEnded && (entry.EndedFrame < entry.CreatedFrame || entry.EndedFrame > frame.Value) ||
                    !entry.IsEnded && (entry.EndedFrame != 0 || entry.EndReason != 0) ||
                    !contexts.TryAdd(entry.ContextId, entry.ToNode()))
                    throw new InvalidOperationException("Invalid or duplicate lifecycle execution node.");
            }

            foreach (var node in contexts.Values)
            {
                if (node.ParentContextId == node.ContextId ||
                    MobaExecutionContextRegistry.IsExecutionContextId(node.ParentContextId) &&
                    (node.ParentContextId >= node.ContextId || !contexts.ContainsKey(node.ParentContextId)) ||
                    MobaExecutionContextRegistry.IsExecutionContextId(node.RootContextId) && !contexts.ContainsKey(node.RootContextId) ||
                    MobaExecutionContextRegistry.IsExecutionContextId(node.OwnerContextId) && !contexts.ContainsKey(node.OwnerContextId))
                    throw new InvalidOperationException("Lifecycle execution topology has a missing or cyclic reference.");
            }

            var local = MemoryPackSerializer.Deserialize<MobaSkillRuntimeLocalRollbackPayload>(sections["skill-runtime"]);
            if (local.Version != 1 || local.RuntimeState == null || local.ExecutionContextNodes == null ||
                local.ExecutionContextNextId != execution.NextContextId)
                throw new InvalidOperationException("Skill and execution registry checkpoints differ.");
            var skills = MemoryPackSerializer.Deserialize<MobaSkillRuntimeRollbackPayload>(local.RuntimeState);
            if (skills.Version != MobaSkillRuntimeRollbackProvider.RuntimePayloadVersion ||
                skills.Runtimes == null || skills.Retains == null)
                throw new InvalidOperationException("Invalid lifecycle skill runtime state.");
            foreach (var node in local.ExecutionContextNodes)
                if (!contexts.ContainsKey(node.ContextId))
                    throw new InvalidOperationException("Skill context section differs from the execution registry checkpoint.");

            var runtimes = new Dictionary<long, MobaSkillRuntimeRollbackEntry>();
            foreach (var runtime in skills.Runtimes)
                if (runtime.RuntimeId <= 0 || !runtimes.TryAdd(runtime.RuntimeId, runtime))
                    throw new InvalidOperationException("Invalid or duplicate lifecycle skill runtime identity.");

            var retainIds = new HashSet<long>();
            foreach (var retain in skills.Retains)
            {
                if (retain.RetainId <= 0 || !retainIds.Add(retain.RetainId) ||
                    !runtimes.TryGetValue(retain.RuntimeId, out var runtime) ||
                    runtime.Generation != retain.Generation || runtime.RootContextId != retain.RootContextId)
                    throw new InvalidOperationException("Invalid lifecycle SkillRuntime retain identity.");
            }

            return new MobaSkillLifecycleSnapshotIndex(actorIds, contexts, skills.Retains);
        }

        internal void RequireSource(MobaExecutionKind kind, int sourceActorId, int configId,
            long sourceContextId, long rootContextId)
        {
            if (!Contexts.TryGetValue(sourceContextId, out var source) || source.IsEnded ||
                source.Kind != kind || source.SourceActorId != sourceActorId || source.ConfigId != configId)
                throw new InvalidOperationException("Relation source context is missing or mismatched in the target lifecycle checkpoint.");
            if (rootContextId != 0 && !Contexts.ContainsKey(rootContextId))
                throw new InvalidOperationException("Relation root context is missing in the target lifecycle checkpoint.");
        }

        internal void RequireRetain(in MobaSkillRuntimeRetainHandle expected)
        {
            if (!expected.IsValid) return;
            foreach (var retain in Retains)
                if (retain.RetainId == expected.RetainId && retain.RuntimeId == expected.Runtime.RuntimeId &&
                    retain.Generation == expected.Runtime.Generation && retain.RootContextId == expected.Runtime.RootContextId &&
                    retain.ChildKind == (int)expected.Child.Kind && retain.ChildId == expected.Child.ChildId &&
                    retain.ChildContextId == expected.Child.ContextId && retain.ChildConfigId == expected.Child.ConfigId)
                    return;
            throw new InvalidOperationException("Relation retain does not match the target lifecycle SkillRuntime checkpoint.");
        }
    }
}
