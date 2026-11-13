using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Components;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>
    /// Capture and cross-domain preflight for the approved, bounded lifecycle group.
    /// Commit revalidates the detached plan and compensates the group's failed restore.
    /// </summary>
    public sealed partial class MobaSkillLifecycleRollbackPreparation
    {
        private readonly MobaActorRegistry _actors;
        private readonly MobaRuntimeContextService _contexts;
        private readonly MobaSkillCastRuntimeService _skills;
        private readonly SkillCastCoordinator _pipelines;
        private readonly MobaBuffStateRecoveryProvider _buffs;
        private readonly MobaContextEntityRollbackProvider _contextProvider;
        private readonly MobaSkillRuntimeRollbackProvider _skillProvider;
        private readonly MobaContinuousModifierCaptureRollbackProvider _captures;
        private readonly MobaTriggerModifierContinuousRollbackProvider _modifiers;
        private readonly MobaOwnerBlackboardRollbackProvider _owners;
        private readonly MobaContinuousManager _continuous;
        private readonly MobaTriggerIntervalContinuousService _intervals;
        private readonly MobaTriggerModifierContinuousService _triggerModifiers;

        public MobaSkillLifecycleRollbackPreparation(IWorld world, MobaOwnerBlackboardRollbackProvider owners)
        {
            if (world?.Services == null) throw new ArgumentNullException(nameof(world));
            _owners = owners ?? throw new ArgumentNullException(nameof(owners));
            var services = world.Services;
            _actors = services.Resolve<MobaActorRegistry>();
            _contexts = services.Resolve<MobaRuntimeContextService>();
            _skills = services.Resolve<MobaSkillCastRuntimeService>();
            _pipelines = services.Resolve<SkillCastCoordinator>();
            _buffs = services.Resolve<MobaBuffStateRecoveryProvider>();
            _continuous = services.Resolve<MobaContinuousManager>();
            _intervals = services.Resolve<MobaTriggerIntervalContinuousService>();
            _triggerModifiers = services.Resolve<MobaTriggerModifierContinuousService>();
            _contextProvider = new MobaContextEntityRollbackProvider(_contexts);
            _skillProvider = new MobaSkillRuntimeRollbackProvider(_skills);
            _captures = new MobaContinuousModifierCaptureRollbackProvider(services.Resolve<MobaContinuousManager>(),
                services.Resolve<MobaTriggerIntervalContinuousService>());
            _modifiers = new MobaTriggerModifierContinuousRollbackProvider(services.Resolve<MobaTriggerModifierContinuousService>());
        }

        public byte[] Capture(FrameIndex frame)
        {
            if (frame.Value < 0) throw new ArgumentOutOfRangeException(nameof(frame));
            _buffs.ValidateLocalLifecycleOwnership();
            var registry = _skills.ExecutionContextRegistry ?? throw new InvalidOperationException("Execution registry is unavailable.");
            var nodes = registry.CaptureLifecycleSnapshot();
            var entries = new MobaSkillExecutionContextRollbackEntry[nodes.Count];
            for (var i = 0; i < nodes.Count; i++) entries[i] = new MobaSkillExecutionContextRollbackEntry(nodes[i]);
            return MobaSkillLifecycleCheckpointCodec.Encode(new MobaSkillLifecycleCheckpoint
            {
                Format = MobaSkillLifecycleCheckpoint.FormatName, Version = 2, Frame = frame.Value,
                ActorIds = new List<int>(_actors.CopyActorIdsInOrder()).ToArray(),
                Sections = new[]
                {
                    Section("context-entities", _contextProvider.Export(frame)),
                    Section("execution-contexts", MobaSkillLifecycleCheckpointCodec.Encode(new MobaExecutionRegistryCheckpoint
                    { Version = 1, NextContextId = registry.NextContextId, Nodes = MemoryPackSerializer.Serialize(entries) })),
                    Section("skill-runtime", _skillProvider.Export(frame)),
                    Section("buffs", _buffs.ExportState(frame)),
                    Section("buff-local", MobaSkillLifecycleCheckpointCodec.Encode(_buffs.CaptureLocalRollbackEntries())),
                    Section("continuous", _captures.Export(frame)),
                    Section("trigger-modifiers", _modifiers.Export(frame)),
                    Section("pipelines", _pipelines.ExportRunnerRollbackState()),
                    Section("owner-blackboards", _owners.Export(frame)),
                    Section("continuous-local", CaptureContinuousLocal()),
                },
            });
        }

        public MobaSkillLifecycleRestorePlan Prepare(FrameIndex frame, byte[] payload)
        {
            _buffs.ValidateLocalLifecycleOwnership();
            var checkpoint = MobaSkillLifecycleCheckpointCodec.Decode<MobaSkillLifecycleCheckpoint>(payload);
            var sections = MobaSkillLifecycleCheckpointCodec.ReadSections(checkpoint);
            if (checkpoint.Frame != frame.Value) throw new InvalidOperationException("Lifecycle checkpoint frame differs from the requested frame.");
            var liveActors = new List<int>(_actors.CopyActorIdsInOrder());
            if (checkpoint.ActorIds.Length != liveActors.Count) throw new InvalidOperationException($"Lifecycle rollback requires the same actor roster. target=[{string.Join(",", checkpoint.ActorIds)}], live=[{string.Join(",", liveActors)}].");
            for (var i = 0; i < liveActors.Count; i++)
                if (checkpoint.ActorIds[i] <= 0 || checkpoint.ActorIds[i] != liveActors[i])
                    throw new InvalidOperationException($"Lifecycle rollback requires the same actor roster. target=[{string.Join(",", checkpoint.ActorIds)}], live=[{string.Join(",", liveActors)}].");
            _owners.ValidateImport(frame, sections["owner-blackboards"]);

            var execution = MobaSkillLifecycleCheckpointCodec.Decode<MobaExecutionRegistryCheckpoint>(sections["execution-contexts"]);
            var nodes = ValidateExecutionContexts(execution, frame);
            var local = MemoryPackSerializer.Deserialize<MobaSkillRuntimeLocalRollbackPayload>(sections["skill-runtime"]);
            if (local.Version != 1 || local.RuntimeState == null || local.ExecutionContextNodes == null ||
                local.ExecutionContextNextId != execution.NextContextId)
                throw new InvalidOperationException("Skill and execution registry checkpoints differ.");
            foreach (var node in local.ExecutionContextNodes)
                if (!nodes.TryGetValue(node.ContextId, out var expected) ||
                    !MemoryPackSerializer.Serialize(node).AsSpan().SequenceEqual(
                        MemoryPackSerializer.Serialize(new MobaSkillExecutionContextRollbackEntry(expected))))
                    throw new InvalidOperationException("Skill context section differs from the execution registry checkpoint.");
            var skills = MemoryPackSerializer.Deserialize<MobaSkillRuntimeRollbackPayload>(local.RuntimeState);
            ValidateSkills(skills, nodes, new HashSet<int>(checkpoint.ActorIds));
            ValidateLocalExecutionScope(local.ExecutionContextNodes, skills, nodes);
            // Validate Blackboard slots and runtime value representation in isolation.
            new MobaSkillRuntimeRollbackProvider(new MobaSkillCastRuntimeService()).ImportState(frame, local.RuntimeState);

            var contexts = MemoryPackSerializer.Deserialize<MobaContextEntityRollbackPayload>(sections["context-entities"]);
            if (contexts.Version != 1 || contexts.NextEntityId < 1 || contexts.EntityIds == null)
                throw new InvalidOperationException("Invalid lifecycle context entity checkpoint.");
            var contextIds = new HashSet<long>();
            foreach (var id in contexts.EntityIds)
                if (id <= 0 || id >= contexts.NextEntityId || !contextIds.Add(id))
                    throw new InvalidOperationException("Invalid or duplicate target context entity.");
            var metadata = MobaSkillLifecycleCheckpointCodec.Decode<MobaBuffLocalRollbackEntry[]>(sections["buff-local"]);
            var staged = _buffs.PrepareLocalRollback(sections["buffs"], metadata, skills, nodes, contextIds);
            var buffState = MemoryPackSerializer.Deserialize<MobaBuffStateRecoveryPayload>(sections["buffs"]);
            var ownedContextIds = new HashSet<long>();
            foreach (var buff in buffState.Entries ?? Array.Empty<MobaBuffStateRecoveryEntry>())
                if (buff.RuntimeContextId != 0) ownedContextIds.Add(buff.RuntimeContextId);
            // Only the group's declared Buff contexts can be reconstructed. Unknown contexts stay strict.
            foreach (var id in contextIds)
                if (!ownedContextIds.Contains(id) && !_contexts.Registry.Exists(id))
                    throw new InvalidOperationException("A target context outside Buff lifecycle ownership is missing.");

            _captures.ValidateLifecycleImport(sections["continuous"], staged);
            _modifiers.ValidateImport(frame, sections["trigger-modifiers"]);
            _pipelines.ValidateRunnerRollbackState(sections["pipelines"]);
            ValidateRunners(sections["pipelines"], skills, nodes);
            PrepareTarget(frame, sections, staged);
            return new MobaSkillLifecycleRestorePlan(payload, frame.Value, liveActors.Count,
                skills.Runtimes?.Length ?? 0, metadata.Length, staged.Count);
        }

        private Dictionary<long, MobaExecutionContextNode> ValidateExecutionContexts(MobaExecutionRegistryCheckpoint state, FrameIndex frame)
        {
            if (state.Version != 1 || state.Nodes == null || state.NextContextId < MobaExecutionContextRegistry.FirstExecutionContextId)
                throw new InvalidOperationException("Invalid lifecycle execution registry checkpoint.");
            _skills.ExecutionContextRegistry.ValidatePredictionRetraction(state.NextContextId);
            var nodes = new Dictionary<long, MobaExecutionContextNode>();
            foreach (var entry in MemoryPackSerializer.Deserialize<MobaSkillExecutionContextRollbackEntry[]>(state.Nodes)
                ?? throw new InvalidOperationException("Execution nodes are missing."))
            {
                if (entry.ContextId < MobaExecutionContextRegistry.FirstExecutionContextId || entry.ContextId >= state.NextContextId ||
                    entry.Kind <= 0 || entry.Kind > byte.MaxValue || !Enum.IsDefined(typeof(MobaExecutionKind), (MobaExecutionKind)entry.Kind) ||
                    entry.CreatedFrame < 0 || entry.CreatedFrame > frame.Value || entry.EndedFrame < 0 ||
                    entry.IsEnded && (entry.EndedFrame < entry.CreatedFrame || entry.EndedFrame > frame.Value) ||
                    !entry.IsEnded && (entry.EndedFrame != 0 || entry.EndReason != 0) ||
                    !nodes.TryAdd(entry.ContextId, entry.ToNode()))
                    throw new InvalidOperationException($"Invalid or duplicate lifecycle execution node: id={entry.ContextId}, kind={entry.Kind}, created={entry.CreatedFrame}, ended={entry.EndedFrame}, checkpoint={frame.Value}.");
                if (_skills.ExecutionContextRegistry.TryGet(entry.ContextId, out var live) &&
                    !MobaExecutionContextRegistry.HasSameIdentity(live, entry.ToNode()))
                    throw new InvalidOperationException("Lifecycle checkpoint changed a surviving execution identity.");
            }
            foreach (var node in nodes.Values)
            {
                if (node.ParentContextId == node.ContextId ||
                    MobaExecutionContextRegistry.IsExecutionContextId(node.ParentContextId) &&
                    (node.ParentContextId >= node.ContextId || !nodes.ContainsKey(node.ParentContextId)) ||
                    MobaExecutionContextRegistry.IsExecutionContextId(node.RootContextId) && !nodes.ContainsKey(node.RootContextId) ||
                    MobaExecutionContextRegistry.IsExecutionContextId(node.OwnerContextId) && !nodes.ContainsKey(node.OwnerContextId))
                    throw new InvalidOperationException("Lifecycle execution topology has a missing or cyclic reference.");
            }
            return nodes;
        }

        private static void ValidateSkills(in MobaSkillRuntimeRollbackPayload state,
            IReadOnlyDictionary<long, MobaExecutionContextNode> nodes, ISet<int> actors)
        {
            if (state.Version != 3 || state.NextRuntimeId <= 0 || state.NextRetainId <= 0 || state.NextGeneration <= 0 ||
                state.Runtimes == null || state.Retains == null)
                throw new InvalidOperationException("Invalid lifecycle skill runtime state.");
            var runtimes = new Dictionary<long, MobaSkillRuntimeRollbackEntry>();
            var roots = new HashSet<long>();
            var childCount = 0;
            foreach (var runtime in state.Runtimes)
            {
                if (runtime.RuntimeId <= 0 || runtime.RuntimeId >= state.NextRuntimeId || runtime.Generation <= 0 ||
                    runtime.Generation >= state.NextGeneration || runtime.SkillId <= 0 || runtime.SkillLevel <= 0 ||
                    runtime.IsEnded || runtime.IsEnding || !Enum.IsDefined(typeof(SkillCastStage), runtime.Stage) ||
                    !Enum.IsDefined(typeof(MobaSkillRuntimeEndReason), runtime.EndReason) ||
                    !actors.Contains(runtime.CasterActorId) || runtime.TargetActorId > 0 && !actors.Contains(runtime.TargetActorId) ||
                    !runtimes.TryAdd(runtime.RuntimeId, runtime) || !roots.Add(runtime.RootContextId) || runtime.Children == null ||
                    !nodes.TryGetValue(runtime.RootContextId, out var root) || root.Kind != MobaExecutionKind.SkillCast || root.ConfigId != runtime.SkillId)
                    throw new InvalidOperationException("Invalid lifecycle skill identity, actor, or root context.");
                var children = new HashSet<(int, long)>();
                foreach (var child in runtime.Children)
                {
                    if (child.Kind == 0 || !Enum.IsDefined(typeof(MobaSkillRuntimeChildKind), child.Kind) || child.ChildId == 0 ||
                        !children.Add((child.Kind, child.ChildId)) || child.ContextId != 0 &&
                        (!nodes.TryGetValue(child.ContextId, out var node) || node.RootContextId != runtime.RootContextId ||
                         !MobaSkillRuntimeRollbackProvider.MatchesChild(node, child)))
                        throw new InvalidOperationException("Invalid lifecycle skill child context.");
                    childCount++;
                }
            }
            var retainIds = new HashSet<long>();
            var retainedChildren = new HashSet<(long, int, long)>();
            foreach (var retain in state.Retains)
            {
                if (retain.RetainId <= 0 || retain.RetainId >= state.NextRetainId || !retainIds.Add(retain.RetainId) ||
                    !runtimes.TryGetValue(retain.RuntimeId, out var runtime) || runtime.Generation != retain.Generation ||
                    runtime.RootContextId != retain.RootContextId || !retainedChildren.Add((retain.RuntimeId, retain.ChildKind, retain.ChildId)))
                    throw new InvalidOperationException("Invalid lifecycle parent runtime or retain identity.");
                var found = false;
                foreach (var child in runtime.Children)
                    if (child.Kind == retain.ChildKind && child.ChildId == retain.ChildId &&
                        child.ContextId == retain.ChildContextId && child.ConfigId == retain.ChildConfigId) found = true;
                if (!found) throw new InvalidOperationException("Lifecycle retain has no matching target child.");
            }
            if (childCount != state.Retains.Length) throw new InvalidOperationException("Lifecycle children and retains are not one to one.");
        }

        private static void ValidateLocalExecutionScope(MobaSkillExecutionContextRollbackEntry[] entries,
            in MobaSkillRuntimeRollbackPayload skills, IReadOnlyDictionary<long, MobaExecutionContextNode> nodes)
        {
            var expected = new HashSet<long>();
            var roots = new HashSet<long>();
            foreach (var runtime in skills.Runtimes)
            {
                roots.Add(runtime.RootContextId);
                expected.Add(runtime.RootContextId);
                foreach (var child in runtime.Children)
                    if (child.ContextId != 0) expected.Add(child.ContextId);
            }
            foreach (var node in nodes.Values)
                if (node.Kind == MobaExecutionKind.SkillPhase && roots.Contains(node.RootContextId))
                    expected.Add(node.ContextId);
            foreach (var entry in entries)
                if (!expected.Remove(entry.ContextId))
                    throw new InvalidOperationException("Duplicate or unrelated local skill execution context.");
            if (expected.Count != 0)
                throw new InvalidOperationException("Local skill section omits target execution contexts.");
        }

        private static void ValidateRunners(byte[] payload, in MobaSkillRuntimeRollbackPayload skills,
            IReadOnlyDictionary<long, MobaExecutionContextNode> nodes)
        {
            var runners = SkillRunnerRollbackCodec.Deserialize(payload);
            foreach (var runner in runners.Entries)
            {
                var found = false;
                foreach (var runtime in skills.Runtimes)
                    if (runtime.RuntimeId == runner.RuntimeId && runtime.Generation == runner.RuntimeGeneration &&
                        runtime.RootContextId == runner.RootContextId && runtime.CasterActorId == runner.ActorId &&
                        runtime.SkillId == runner.SkillId && runtime.Sequence == runner.Sequence) found = true;
                if (!found || runner.PhaseContextId != 0 &&
                    (!nodes.TryGetValue(runner.PhaseContextId, out var phase) || phase.IsEnded ||
                     phase.Kind != MobaExecutionKind.SkillPhase || phase.RootContextId != runner.RootContextId))
                    throw new InvalidOperationException("Lifecycle runner parent runtime or phase context is missing.");
            }
        }

        private static MobaSkillLifecycleSection Section(string name, byte[] payload) =>
            new MobaSkillLifecycleSection { Name = name, Payload = payload };
    }
}
