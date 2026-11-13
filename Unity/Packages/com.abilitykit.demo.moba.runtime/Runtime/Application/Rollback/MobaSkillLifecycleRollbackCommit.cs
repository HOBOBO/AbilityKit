using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Continuous;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Rollback
{
    internal enum MobaLifecycleRestoreStage { Detached, Built, Registered, Bound, Projected, Reconciled }

    public sealed partial class MobaSkillLifecycleRollbackPreparation
    {
        // Tests inject after actual stages; production never installs a callback.
        internal Action<MobaLifecycleRestoreStage, bool> RestoreStageCompleted;

        private sealed class TargetState
        {
            internal readonly List<(BuffRuntime Runtime, int TargetActorId)> Buffs = new List<(BuffRuntime, int)>();
            internal readonly List<IContinuous> Instances = new List<IContinuous>();
            internal List<MobaTriggerIntervalContinuousRuntime> Intervals;
            internal List<TriggerModifierContinuous> Modifiers;
            internal int NextSourceId, NextProjectionId;
            internal MobaContinuousLocalCheckpoint[] Order;
        }

        private byte[] CaptureContinuousLocal()
        {
            var entries = new List<MobaContinuousLocalCheckpoint>();
            foreach (var instance in _continuous.GetAllContinuous())
            {
                if (!(instance is MobaContinuousRuntimeBase runtime))
                    throw new InvalidOperationException("Local lifecycle recovery requires a continuous state accessor.");
                var entry = MobaContinuousLocalCheckpoint.Capture(runtime);
                entry.PausedByTags = _continuous.IsLocallyPausedByTags(runtime);
                entries.Add(entry);
            }
            return MobaSkillLifecycleCheckpointCodec.Encode(entries.ToArray());
        }

        private TargetState PrepareTarget(FrameIndex frame, Dictionary<string, byte[]> sections,
            IReadOnlyList<BuffContinuousRuntime> stagedBuffs)
        {
            var result = new TargetState
            {
                Order = MobaSkillLifecycleCheckpointCodec.Decode<MobaContinuousLocalCheckpoint[]>(sections["continuous-local"]),
            };
            var metadata = MobaSkillLifecycleCheckpointCodec.Decode<MobaBuffLocalRollbackEntry[]>(sections["buff-local"]);
            var states = MemoryPackSerializer.Deserialize<MobaBuffStateRecoveryPayload>(sections["buffs"]);
            foreach (var entry in states.Entries)
            {
                BuffRuntime runtime = null;
                foreach (var continuous in stagedBuffs)
                    if (continuous.TargetActorId == entry.TargetActorId && continuous.BuffId == entry.BuffId &&
                        continuous.SourceActorId == entry.SourceActorId && continuous.SourceContextId == entry.SourceContextId)
                        runtime = continuous.Runtime;
                if (runtime == null)
                {
                    runtime = new BuffRuntime();
                    entry.ApplyTo(runtime);
                    foreach (var local in metadata)
                        if (local.TargetActorId == entry.TargetActorId && local.BuffId == entry.BuffId &&
                            local.SourceActorId == entry.SourceActorId && local.SourceContextId == entry.SourceContextId)
                            runtime.ContextSource = local.ContextSource.ToView();
                }
                result.Buffs.Add((runtime, entry.TargetActorId));
            }
            foreach (var buff in stagedBuffs) result.Instances.Add(buff);
            var intervalEntries = _captures.PrepareLifecycleIntervals(sections["continuous"], stagedBuffs, out result.NextSourceId);
            result.Intervals = _intervals.PrepareLocalRollback(intervalEntries);
            var execution = MobaSkillLifecycleCheckpointCodec.Decode<MobaExecutionRegistryCheckpoint>(sections["execution-contexts"]);
            var executionIds = new HashSet<long>();
            foreach (var entry in MemoryPackSerializer.Deserialize<MobaSkillExecutionContextRollbackEntry[]>(execution.Nodes)) executionIds.Add(entry.ContextId);
            foreach (var interval in result.Intervals)
            {
                if (!_actors.TryGet(interval.SourceActorId, out _) || !_actors.TryGet(interval.TargetActorId, out _))
                    throw new InvalidOperationException("Local interval references an actor outside the target roster.");
                var source = interval.SourceSnapshot;
                foreach (var id in new[] { interval.SourceContextId, source.SourceContextId, source.ParentContextId, source.RootContextId, source.OwnerContextId })
                    if (MobaExecutionContextRegistry.IsExecutionContextId(id) && !executionIds.Contains(id))
                        throw new InvalidOperationException("Local interval execution source is missing from the target registry.");
                if (source.IsValid && (source.SourceActorId != interval.SourceActorId || source.TargetActorId != interval.TargetActorId))
                    throw new InvalidOperationException("Local interval execution source refers to different actors.");
                result.Instances.Add(interval);
            }
            result.Modifiers = MobaTriggerModifierContinuousRollbackProvider.ReadAndValidate(
                sections["trigger-modifiers"], out result.NextProjectionId);
            foreach (var modifier in result.Modifiers)
            {
                if (!_actors.TryGet(modifier.OwnerActorId, out _))
                    throw new InvalidOperationException("Local trigger modifier references an actor outside the target roster.");
                modifier.RestoreLocalLifecycle(modifier.WasPaused ? ContinuousState.Paused : ContinuousState.Active, 0, 0);
                result.Instances.Add(modifier);
            }
            foreach (var live in _continuous.GetAllContinuous())
                if (!(live is BuffContinuousRuntime) && !(live is TriggerModifierContinuous) &&
                    !(live is SkillPipelineContinuousRuntime) &&
                    !(live is MobaTriggerIntervalContinuousRuntime interval && _intervals.OwnsRuntime(interval)))
                    result.Instances.Add(live);

            var byIdentity = Index(result.Instances);
            var expectedPipelines = new HashSet<(string, long, string)>();
            foreach (var runner in SkillRunnerRollbackCodec.Deserialize(sections["pipelines"]).Entries)
            {
                var run = PipelineRollbackStateCodec.Deserialize(runner.Run);
                expectedPipelines.Add((typeof(SkillPipelineContinuousRuntime).FullName, runner.ActorId,
                    $"skill_pipeline:{runner.ActorId}:{runner.SkillId}:{run.ConfigId}:{runner.Context.SourceContextId}"));
            }
            var seen = new HashSet<(string, long, string)>();
            foreach (var entry in result.Order)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Type) || string.IsNullOrEmpty(entry.Id) ||
                    entry.ElapsedRaw < 0 || entry.IntervalRaw < 0 ||
                    entry.State != (int)ContinuousState.Active && entry.State != (int)ContinuousState.Paused && entry.State != (int)ContinuousState.Inactive ||
                    entry.PausedByTags && entry.State != (int)ContinuousState.Paused || !seen.Add(entry.Key))
                    throw new InvalidOperationException("Invalid local Continuous order or state.");
                if (expectedPipelines.Remove(entry.Key))
                {
                    if (entry.Configuration != null) throw new InvalidOperationException("Pipeline configuration belongs to its runner checkpoint.");
                    continue;
                }
                if (!byIdentity.TryGetValue(entry.Key, out var instance))
                    throw new InvalidOperationException("Local Continuous membership is outside declared recovery ownership.");
                if (!string.Equals(entry.Configuration, MobaContinuousLocalCheckpoint.Capture(instance).Configuration, StringComparison.Ordinal))
                    throw new InvalidOperationException("Local continuous configuration changed since the checkpoint.");
                if (instance is BuffContinuousRuntime || instance is MobaTriggerIntervalContinuousRuntime interval && result.Intervals.Contains(interval))
                    if ((int)instance.State != entry.State || instance.ElapsedRaw != entry.ElapsedRaw || instance.IntervalRemainingRaw != entry.IntervalRaw)
                        throw new InvalidOperationException("Local continuous sections disagree about timing or state.");
                byIdentity.Remove(entry.Key);
            }
            if (expectedPipelines.Count != 0 || byIdentity.Count != 0)
                throw new InvalidOperationException("Local Continuous membership is incomplete.");
            var sources = new HashSet<(int, int)>();
            foreach (var instance in result.Instances)
                if (instance.Config is IMobaContinuousProjectionConfig projection && projection.ModifierSourceId != 0 &&
                    !sources.Add((projection.OwnerActorId, projection.ModifierSourceId)))
                    throw new InvalidOperationException("Local continuous projection sources collide.");
            _continuous.ValidateLocalSubscriptions(result.Instances);
            return result;
        }

        private static Dictionary<(string, long, string), MobaContinuousRuntimeBase> Index(IReadOnlyList<IContinuous> instances)
        {
            var index = new Dictionary<(string, long, string), MobaContinuousRuntimeBase>();
            foreach (var instance in instances)
                if (!(instance is MobaContinuousRuntimeBase runtime) ||
                    !index.TryAdd(MobaContinuousLocalCheckpoint.Capture(runtime).Key, runtime))
                    throw new InvalidOperationException("Local continuous identities are unsupported or duplicate.");
            return index;
        }

        private List<IContinuous> RestoreOrder(MobaContinuousLocalCheckpoint[] order, IReadOnlyList<IContinuous> instances,
            out List<IContinuous> tagPaused)
        {
            var index = Index(instances);
            var result = new List<IContinuous>();
            tagPaused = new List<IContinuous>();
            foreach (var entry in order)
            {
                if (!index.TryGetValue(entry.Key, out var runtime))
                    throw new InvalidOperationException("Rebuilt continuous is missing from the target order.");
                index.Remove(entry.Key);
                runtime.RestoreLocalLifecycle((ContinuousState)entry.State, entry.ElapsedRaw, entry.IntervalRaw);
                if (entry.PausedByTags) tagPaused.Add(runtime);
                result.Add(runtime);
            }
            if (index.Count != 0) throw new InvalidOperationException("Unexpected rebuilt continuous registration.");
            _continuous.RestoreLocalRegistration(result);
            return result;
        }

        /// <summary>Rechecks the detached plan immediately before committing it. A failure compensates
        /// only this group's state; the outer coordinator still owns partial World restore reporting.</summary>
        public void Commit(MobaSkillLifecycleRestorePlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var frame = new FrameIndex(plan.Frame);
            var bytes = plan.CopyCheckpoint();
            Prepare(frame, bytes);
            var checkpoint = MobaSkillLifecycleCheckpointCodec.Decode<MobaSkillLifecycleCheckpoint>(bytes);
            var sections = MobaSkillLifecycleCheckpointCodec.ReadSections(checkpoint);
            var execution = MobaSkillLifecycleCheckpointCodec.Decode<MobaExecutionRegistryCheckpoint>(sections["execution-contexts"]);
            var localSkills = MemoryPackSerializer.Deserialize<MobaSkillRuntimeLocalRollbackPayload>(sections["skill-runtime"]);
            var skills = MemoryPackSerializer.Deserialize<MobaSkillRuntimeRollbackPayload>(localSkills.RuntimeState);
            var contexts = MemoryPackSerializer.Deserialize<MobaContextEntityRollbackPayload>(sections["context-entities"]);
            var metadata = MobaSkillLifecycleCheckpointCodec.Decode<MobaBuffLocalRollbackEntry[]>(sections["buff-local"]);
            var nodes = ValidateExecutionContexts(execution, frame);
            var target = PrepareTarget(frame, sections, _buffs.PrepareLocalRollback(sections["buffs"], metadata,
                skills, nodes, new HashSet<long>(contexts.EntityIds)));

            // Backups retain live object identities until all fallible stages have completed.
            var oldInstances = _continuous.GetAllContinuous();
            var oldOrder = MobaSkillLifecycleCheckpointCodec.Decode<MobaContinuousLocalCheckpoint[]>(CaptureContinuousLocal());
            var oldCaptures = new Dictionary<MobaContinuousRuntimeBase, Dictionary<MobaContinuousModifierCaptureKey, MobaModifierCapturedValue>>();
            var oldIntervals = new List<MobaTriggerIntervalContinuousRuntime>();
            foreach (var instance in oldInstances)
            {
                var runtime = (MobaContinuousRuntimeBase)instance;
                oldCaptures.Add(runtime, new Dictionary<MobaContinuousModifierCaptureKey, MobaModifierCapturedValue>(runtime.StableModifierCaptures));
                if (runtime is MobaTriggerIntervalContinuousRuntime interval && _intervals.OwnsRuntime(interval)) oldIntervals.Add(interval);
            }
            var oldModifiers = _triggerModifiers.GetActive();
            var oldProjectionId = _triggerModifiers.NextProjectionId;
            var oldSourceId = _continuous.NextModifierSourceId;
            var restoreSubscriptions = _continuous.PrepareLocalSubscriptionRebind(oldInstances);
            var restoreSkills = _skills.CaptureLocalCompensation();
            var restoreContexts = _contexts.CaptureLocalLifecycleCompensation();
            var oldNodes = _skills.ExecutionContextRegistry.CaptureLifecycleSnapshot();
            var oldNextContextId = _skills.ExecutionContextRegistry.NextContextId;
            var oldRunners = _pipelines.ExportRunnerRollbackState();
            var oldBlackboards = _owners.Export(frame);
            var oldRepositories = CaptureRepositories();
            try
            {
                _pipelines.DetachForLocalRollback();
                _continuous.ClearLocalProjections();
                _continuous.RestoreLocalRegistration(Array.Empty<IContinuous>());
                RestoreStageCompleted?.Invoke(MobaLifecycleRestoreStage.Detached, false);

                _skills.ExecutionContextRegistry.RestoreLocalRegistry(new List<MobaExecutionContextNode>(nodes.Values), execution.NextContextId);
                _skillProvider.ImportState(frame, localSkills.RuntimeState);
                BindRetains(target.Buffs, metadata);
                ApplyRepositories(target.Buffs);
                _contexts.RestoreLocalBuffBindings(contexts.NextEntityId, contexts.EntityIds, target.Buffs, frame.Value);
                _intervals.RestoreLocalIndex(target.Intervals);
                _triggerModifiers.RestoreLocalIndex(target.Modifiers, target.NextProjectionId);
                RestoreStageCompleted?.Invoke(MobaLifecycleRestoreStage.Built, false);

                _continuous.RestoreLocalRegistration(target.Instances);
                RestoreStageCompleted?.Invoke(MobaLifecycleRestoreStage.Registered, false);
                _pipelines.ImportRunnerRollbackState(sections["pipelines"], localRollback: true);
                var ordered = RestoreOrder(target.Order, _continuous.GetAllContinuous(), out var tagPaused);
                _continuous.PrepareLocalSubscriptionRebind(ordered)();
                _owners.Import(frame, sections["owner-blackboards"]);
                RestoreStageCompleted?.Invoke(MobaLifecycleRestoreStage.Bound, false);
                _continuous.RestoreLocalProjectionOwners(ordered, target.NextSourceId);
                _captures.RestoreLocalCaptures(sections["continuous"]);
                _continuous.ProjectLocalOwners(ordered, () => RestoreStageCompleted?.Invoke(MobaLifecycleRestoreStage.Projected, false));
                _continuous.RestoreLocalTagState(tagPaused, checkpoint.ActorIds);
                _buffs.ValidateLocalLifecycleOwnership();
                RestoreStageCompleted?.Invoke(MobaLifecycleRestoreStage.Reconciled, false);
            }
            catch (Exception failure)
            {
                try
                {
                    _pipelines.DetachForLocalRollback();
                    _continuous.ClearLocalProjections();
                    restoreSkills();
                    _skills.ExecutionContextRegistry.RestoreLocalRegistry(oldNodes, oldNextContextId);
                    RestoreRepositories(oldRepositories);
                    restoreContexts();
                    foreach (var pair in oldCaptures) pair.Key.RestoreStableModifierCaptures(pair.Value);
                    var baseInstances = new List<IContinuous>();
                    foreach (var instance in oldInstances) if (!(instance is SkillPipelineContinuousRuntime)) baseInstances.Add(instance);
                    _continuous.RestoreLocalRegistration(baseInstances);
                    _intervals.RestoreLocalIndex(oldIntervals);
                    _triggerModifiers.RestoreLocalIndex(oldModifiers, oldProjectionId);
                    _pipelines.ImportRunnerRollbackState(oldRunners, localRollback: true);
                    var ordered = RestoreOrder(oldOrder, _continuous.GetAllContinuous(), out var paused);
                    restoreSubscriptions();
                    _owners.Import(frame, oldBlackboards);
                    _continuous.RestoreLocalProjectionOwners(ordered, oldSourceId);
                    _continuous.ProjectLocalOwners(ordered);
                    _continuous.RestoreLocalTagState(paused, checkpoint.ActorIds);
                    _buffs.ValidateLocalLifecycleOwnership();
                    RestoreStageCompleted?.Invoke(MobaLifecycleRestoreStage.Reconciled, true);
                }
                catch (Exception compensation)
                {
                    throw new AggregateException("Local skill lifecycle restore and compensation both failed; stop replay.", failure, compensation);
                }
                throw new InvalidOperationException("Local skill lifecycle restore failed; the group's previous state was restored.", failure);
            }
            // No normal End/Removed effects or retain releases: the complete target table owns them now.
            foreach (var repository in oldRepositories)
                foreach (var runtime in repository.Value.Items) BuffRepository.ReleaseRuntime(runtime);
            _skills.ExecutionContextRegistry.NotifyLocalRegistryReconciled();
        }

        private sealed class RepositoryBackup
        {
            internal bool HadComponent;
            internal List<BuffRuntime> List;
            internal BuffRuntime[] Items;
        }

        private Dictionary<global::ActorEntity, RepositoryBackup> CaptureRepositories()
        {
            var result = new Dictionary<global::ActorEntity, RepositoryBackup>();
            foreach (var actor in _actors.Entries)
                result.Add(actor.Value, new RepositoryBackup
                {
                    HadComponent = actor.Value.hasBuffs,
                    List = actor.Value.hasBuffs ? actor.Value.buffs.Active : null,
                    Items = actor.Value.hasBuffs && actor.Value.buffs.Active != null ? actor.Value.buffs.Active.ToArray() : Array.Empty<BuffRuntime>(),
                });
            return result;
        }

        private void ApplyRepositories(IReadOnlyList<(BuffRuntime Runtime, int TargetActorId)> buffs)
        {
            foreach (var actor in _actors.Entries)
                if (actor.Value.hasBuffs && actor.Value.buffs.Active != null)
                {
                    actor.Value.buffs.Active.Clear(); BuffRepository.MarkDirty(actor.Value.buffs.Active);
                }
            foreach (var buff in buffs)
            {
                _actors.TryGet(buff.TargetActorId, out var actor);
                var list = new BuffRepository().GetOrCreateList(actor);
                list.Add(buff.Runtime); BuffRepository.MarkDirty(list);
            }
        }

        private static void RestoreRepositories(Dictionary<global::ActorEntity, RepositoryBackup> repositories)
        {
            foreach (var pair in repositories)
            {
                var backup = pair.Value;
                if (pair.Key.hasBuffs && pair.Key.buffs.Active != backup.List) BuffRepository.ReleaseList(pair.Key);
                if (!backup.HadComponent)
                {
                    if (pair.Key.hasBuffs) pair.Key.RemoveBuffs();
                    continue;
                }
                pair.Key.ReplaceBuffs(backup.List);
                if (backup.List == null) continue;
                backup.List.Clear(); backup.List.AddRange(backup.Items); BuffRepository.MarkDirty(backup.List);
            }
        }

        private void BindRetains(IReadOnlyList<(BuffRuntime Runtime, int TargetActorId)> buffs, MobaBuffLocalRollbackEntry[] metadata)
        {
            foreach (var buff in buffs)
            {
                var runtime = buff.Runtime;
                foreach (var local in metadata)
                {
                    if (local.TargetActorId != buff.TargetActorId || local.BuffId != runtime.BuffId ||
                        local.SourceActorId != runtime.SourceId || local.SourceContextId != runtime.SourceContextId || local.RetainId == 0) continue;
                    var child = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Buff,
                        runtime.SourceContextId, runtime.SourceContextId, runtime.BuffId);
                    var parent = runtime.SkillRuntimeHandle;
                    if (!_skills.TryFindRetain(parent, child, out var retain) || retain.RetainId != local.RetainId ||
                        retain.Child.ContextId != child.ContextId || retain.Child.ConfigId != child.ConfigId)
                        throw new InvalidOperationException("Restored Buff retain does not match its exact target identity.");
                    runtime.SkillRuntimeRetainHandle = retain;
                }
            }
        }
    }
}
