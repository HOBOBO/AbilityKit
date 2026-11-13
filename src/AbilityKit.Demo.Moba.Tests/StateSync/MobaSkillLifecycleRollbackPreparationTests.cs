using System.Reflection;
using System.Text;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Continuous;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;
using AbilityKit.Demo.Moba.Services.Buffs.Tagging;
using AbilityKit.GameplayTags;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Modifiers;
using MemoryPack;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

[CollectionDefinition("Moba lifecycle preparation", DisableParallelization = true)]
public sealed class MobaLifecyclePreparationCollection { }

// Console worlds share registration catalogs and object pools with other real-world
// suites; initialize and dispose them outside concurrent test collections.
[Collection("Moba lifecycle preparation")]
public sealed class MobaSkillLifecycleRollbackPreparationTests : IDisposable
{
    private readonly ConsoleBattleBootstrapper _bootstrapper = new(BattleStartConfig.CreateDefault());
    private readonly IWorld _world;
    private readonly MobaSkillLifecycleRollbackPreparation _preparation;
    private readonly FrameIndex _frame;

    public MobaSkillLifecycleRollbackPreparationTests()
    {
        _bootstrapper.Initialize();
        _bootstrapper.Start();
        for (var i = 0; i < 8 && _bootstrapper.Context.EcsWorld == null; i++) _bootstrapper.Tick();
        _bootstrapper.SetupBattle();
        for (var i = 0; i < 10; i++) _bootstrapper.Tick();
        _world = (IWorld)typeof(ConsoleBattleBootstrapper)
            .GetField("_runtimeWorld", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_bootstrapper)!;
        var registry = MobaRollbackRegistryBuilder.Create(_world);
        Assert.True(registry.TryGet(MobaSkillLifecycleRollbackProvider.DefaultKey, out var group));
        var lifecycle = Assert.IsType<MobaSkillLifecycleRollbackProvider>(group);
        foreach (var key in new[] { MobaOwnerBlackboardRollbackProvider.DefaultKey, MobaSkillRuntimeRollbackProvider.DefaultKey,
            MobaBuffLifecycleRollbackProvider.DefaultKey, MobaBuffTimerRollbackProvider.DefaultKey,
            MobaContinuousModifierCaptureRollbackProvider.DefaultKey, MobaTriggerModifierContinuousRollbackProvider.DefaultKey,
            MobaSkillPipelineRollbackProvider.DefaultKey }) Assert.False(registry.TryGet(key, out _));
        _preparation = new MobaSkillLifecycleRollbackPreparation(_world, lifecycle.OwnerBlackboards);
        _frame = new FrameIndex(_bootstrapper.Context.LastFrame);
    }

    [Theory]
    [InlineData(1, 2, false)]
    [InlineData(3, 4, true)]
    public void Target_buff_parent_and_context_can_be_absent_live_without_preflight_mutation(int target, int source, bool paused)
    {
        var beforeBuff = _preparation.Capture(_frame);
        var buff = AddRetainedBuff(target, source);
        buff.Continuous.AdvanceElapsedRaw(123456789);
        buff.Continuous.IntervalRemainingRaw = 987654;
        buff.Continuous.SyncManagedState();
        if (paused) Assert.True(Get<MobaContinuousManager>().TryPause(buff.Continuous));
        var snapshot = _preparation.Capture(_frame);
        var metadata = ReadMetadata(snapshot).Single(e => e.SourceContextId == buff.SourceContextId);
        Assert.Equal(123456789, metadata.ElapsedRaw);
        Assert.Equal(987654, metadata.IntervalRemainingRaw);
        Assert.Equal((int)(paused ? ContinuousState.Paused : ContinuousState.Active), metadata.State);
        Assert.Equal(buff.ContextSource, metadata.ContextSource.ToView());
        Assert.Equal(buff.SkillRuntimeRetainHandle.RetainId, metadata.RetainId);

        // A new Buff relative to the target is a planned removal, not ordinary membership drift.
        var current = _preparation.Capture(_frame);
        _preparation.Prepare(_frame, beforeBuff);
        Assert.Equal(current, _preparation.Capture(_frame));

        var handle = buff.SkillRuntimeHandle;
        var contextId = buff.RuntimeContextId;
        RemoveBuff(target, buff);
        Assert.False(Get<MobaSkillCastRuntimeService>().TryGet(in handle, out _));
        Assert.False(Get<MobaRuntimeContextService>().Registry.Exists(contextId));
        current = _preparation.Capture(_frame);
        var manager = Get<MobaContinuousManager>();
        var objects = manager.GetAllContinuous().ToArray();
        var lifecycleEvents = new List<string>();
        manager.OnRegistered += _ => lifecycleEvents.Add("registered");
        manager.OnActivated += _ => lifecycleEvents.Add("activated");
        manager.OnUnregistered += (_, _) => lifecycleEvents.Add("unregistered");
        using var observation = Get<MobaExecutionContextRegistry>().Observe(
            (in AbilityKit.Context.ContextLifecycleEvent<MobaExecutionContextNode> _) => lifecycleEvents.Add("context"),
            replayExisting: false);

        var plan = _preparation.Prepare(_frame, snapshot);

        Assert.Equal(current, _preparation.Capture(_frame));
        Assert.Equal(objects, manager.GetAllContinuous());
        Assert.Empty(lifecycleEvents);
        Assert.Equal(snapshot, plan.CopyCheckpoint());
        Assert.True(plan.BuffContinuousCount > 0);
        var saved = plan.CopyCheckpoint();
        Array.Fill(snapshot, (byte)0);
        Array.Fill(saved, (byte)0);
        Assert.NotEqual(saved, plan.CopyCheckpoint());
        Assert.Equal(_frame.Value, plan.Frame);
    }

    [Theory]
    [InlineData(1, 2, false)]
    [InlineData(3, 4, true)]
    public void Commit_rebuilds_ended_buff_with_exact_retains_timing_and_source_then_removes_new_buff(int target, int source, bool paused)
    {
        var baseline = _preparation.Capture(_frame);
        var buff = AddRetainedBuff(target, source);
        buff.Continuous.AdvanceElapsedRaw(123456789);
        buff.Continuous.IntervalRemainingRaw = 987654;
        buff.Continuous.SyncManagedState();
        if (paused) Assert.True(Get<MobaContinuousManager>().TryPause(buff.Continuous));
        var checkpoint = _preparation.Capture(_frame);
        var sourceId = buff.Continuous.ModifierSourceId;
        var retainId = buff.SkillRuntimeRetainHandle.RetainId;
        var sourceContext = buff.SourceContextId;
        RemoveBuff(target, buff);
        var manager = Get<MobaContinuousManager>();
        var events = new List<string>();
        manager.OnRegistered += _ => events.Add("registered");
        manager.OnActivated += _ => events.Add("activated");
        manager.OnUnregistered += (_, _) => events.Add("unregistered");

        _preparation.Commit(_preparation.Prepare(_frame, checkpoint));
        Assert.Equal(checkpoint, _preparation.Capture(_frame));
        var actor = Get<MobaActorRegistry>();
        Assert.True(actor.TryGet(target, out var entity));
        var rebuilt = entity.buffs.Active.Single(b => b.SourceContextId == sourceContext);
        Assert.Equal(sourceId, rebuilt.Continuous.ModifierSourceId);
        Assert.Equal(retainId, rebuilt.SkillRuntimeRetainHandle.RetainId);
        Assert.Equal(paused, rebuilt.Continuous.IsPaused);
        Assert.True(Get<MobaRuntimeContextService>().TryGetBuffReference(rebuilt, out _));

        _preparation.Commit(_preparation.Prepare(_frame, checkpoint));
        Assert.Equal(checkpoint, _preparation.Capture(_frame));
        _preparation.Commit(_preparation.Prepare(_frame, baseline));
        Assert.Equal(baseline, _preparation.Capture(_frame));
        Assert.Empty(events);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Commit_stage_failure_compensates_exact_local_state_and_live_bindings(int stage)
    {
        var target = _preparation.Capture(_frame);
        var buff = AddRetainedBuff(3, 4);
        buff.Continuous.AdvanceElapsedRaw(987654321);
        buff.Continuous.SyncManagedState();
        Assert.True(Get<MobaContinuousManager>().TryPause(buff.Continuous));
        var current = _preparation.Capture(_frame);
        var instances = Get<MobaContinuousManager>().GetAllContinuous().ToArray();
        Assert.True(Get<MobaRuntimeContextService>().TryGetBuffReference(buff, out var reference));
        _preparation.RestoreStageCompleted = (value, compensating) =>
        {
            if (!compensating && (int)value == stage) throw new InvalidOperationException("injected commit failure");
        };
        var error = Assert.Throws<InvalidOperationException>(() => _preparation.Commit(_preparation.Prepare(_frame, target)));
        Assert.Contains("previous state was restored", error.Message);
        Assert.Equal(current, _preparation.Capture(_frame));
        Assert.Equal(instances, Get<MobaContinuousManager>().GetAllContinuous());
        Assert.True(Get<MobaRuntimeContextService>().TryGetBuffReference(buff, out var restoredReference));
        Assert.Equal(reference, restoredReference);
        _preparation.RestoreStageCompleted = null;
        _preparation.Commit(_preparation.Prepare(_frame, target));
        Assert.Equal(target, _preparation.Capture(_frame));
    }

    [Fact]
    public void Legacy_context_only_payload_is_rejected_by_the_joint_provider()
    {
        var current = _preparation.Capture(_frame);
        var legacy = new MobaContextEntityRollbackProvider(Get<MobaRuntimeContextService>()).Export(_frame);
        Assert.ThrowsAny<Exception>(() => _preparation.Prepare(_frame, legacy));
        Assert.Equal(current, _preparation.Capture(_frame));
    }

    [Fact]
    public void Surviving_owner_bound_subscription_keeps_blackboard_and_rebinds_gate_to_rebuilt_buff()
    {
        const int buffId = 10030101;
        const int triggerId = 10030111;
        var buff = AddRetainedBuff(1, 2, buffId);
        var keys = new List<long>();
        Get<MobaContinuousManager>().CopyActiveOwnerKeys(keys);
        var gate = Get<MobaOwnerBoundTriggerGateService>();
        var key = keys.Single(k => gate.HasGate(k, triggerId));
        var store = Get<AbilityKit.Triggering.Blackboard.IOwnerBlackboardStore>();
        var board = store.GetOrCreate(key);
        var checkpoint = _preparation.Capture(_frame);
        Assert.True(Get<MobaContinuousManager>().TryPause(buff.Continuous));

        _preparation.Commit(_preparation.Prepare(_frame, checkpoint));

        Assert.Equal(checkpoint, _preparation.Capture(_frame));
        Assert.Same(board, store.GetOrCreate(key));
        Assert.True(gate.CanExecute(key, triggerId));
        Get<MobaActorRegistry>().TryGet(1, out var actor);
        var restored = actor.buffs.Active.Single(b => b.BuffId == buffId);
        Assert.NotSame(buff, restored);
        Assert.True(Get<MobaContinuousManager>().TryPause(restored.Continuous));
        Assert.False(gate.CanExecute(key, triggerId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Active_runner_restores_silently_and_compensates_with_running_pipeline(bool injectFailure)
    {
        var casts = Get<SkillCastCoordinator>();
        var caster = Get<MobaActorRegistry>().Entries.First(p => p.Value.hasModelId && p.Value.modelId.Value == 1001).Key;
        var cast = casts.TryCastSkill(caster, 10010301, 3);
        Assert.True(cast.Success, cast.FailReason);
        var checkpoint = _preparation.Capture(_frame);
        var plan = _preparation.Prepare(_frame, checkpoint);
        if (injectFailure)
        {
            _preparation.RestoreStageCompleted = (stage, compensating) =>
            {
                if (!compensating && stage == MobaLifecycleRestoreStage.Bound) throw new InvalidOperationException("injected runner failure");
            };
            Assert.Throws<InvalidOperationException>(() => _preparation.Commit(plan));
            Assert.Equal(checkpoint, _preparation.Capture(_frame));
            _preparation.RestoreStageCompleted = null;
        }
        _preparation.Commit(plan);
        Assert.Equal(checkpoint, _preparation.Capture(_frame));
        Assert.True(casts.TryGetRunningByInstanceId(caster, cast.RuntimeHandle.RootContextId, out _));
    }

    [Fact]
    public void Interval_lifecycle_rebuilds_source_timing_pause_and_registration_order()
    {
        var baseline = _preparation.Capture(_frame);
        const int processId = 30020010;
        var root = Get<MobaExecutionContextRegistry>().Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.PassiveActivation, processId, 1, 1, frame: _frame.Value));
        var source = MobaContextSourceView.FromOrigin(new MobaGameplayOrigin(1, 1, MobaExecutionKind.PassiveActivation,
            processId, root.ContextId, root.ContextId, root.ContextId, root.ContextId),
            MobaContextSourceResolveKind.Origin, MobaContextSourceBoundary.Snapshot, false, "TriggerIntervalContinuous", processId);
        var intervals = Get<MobaTriggerIntervalContinuousService>();
        Assert.True(intervals.UpsertProcess(processId, 1, 1, root.ContextId, source));
        var interval = Get<MobaContinuousManager>().GetAllContinuous().OfType<MobaTriggerIntervalContinuousRuntime>()
            .Single(r => r.SourceContextId == root.ContextId);
        interval.SetElapsedRaw(123456789);
        interval.IntervalRemainingRaw = 987654;
        Assert.True(Get<MobaContinuousManager>().TryPause(interval));
        var checkpoint = _preparation.Capture(_frame);
        intervals.EndOwnerProcesses(root.ContextId);
        _preparation.Commit(_preparation.Prepare(_frame, checkpoint));
        Assert.Equal(checkpoint, _preparation.Capture(_frame));
        var rebuilt = Get<MobaContinuousManager>().GetAllContinuous().OfType<MobaTriggerIntervalContinuousRuntime>()
            .Single(r => r.SourceContextId == root.ContextId);
        Assert.True(intervals.OwnsRuntime(rebuilt));
        Assert.True(rebuilt.IsPaused);
        Assert.Equal(interval.ModifierSourceId, rebuilt.ModifierSourceId);
        Assert.Equal(source, rebuilt.SourceSnapshot);
        _preparation.Commit(_preparation.Prepare(_frame, baseline));
        Assert.Equal(baseline, _preparation.Capture(_frame));
    }

    [Fact]
    public void Failed_rebuild_restores_removed_buff_snapshot_history_and_context_identity()
    {
        var buff = AddRetainedBuff(3, 4);
        var contextId = buff.RuntimeContextId;
        var checkpoint = _preparation.Capture(_frame);
        RemoveBuff(3, buff);
        var contexts = Get<MobaRuntimeContextService>();
        var snapshot = contexts.Snapshots.Get(contextId);
        Assert.NotNull(snapshot);
        var historyCount = contexts.Snapshots.HistoryCount;
        var current = _preparation.Capture(_frame);
        _preparation.RestoreStageCompleted = (stage, compensating) =>
        {
            if (!compensating && stage == MobaLifecycleRestoreStage.Reconciled) throw new InvalidOperationException("injected after context rebuild");
        };
        Assert.Throws<InvalidOperationException>(() => _preparation.Commit(_preparation.Prepare(_frame, checkpoint)));
        Assert.Equal(current, _preparation.Capture(_frame));
        Assert.Same(snapshot, contexts.Snapshots.Get(contextId));
        Assert.Equal(historyCount, contexts.Snapshots.HistoryCount);
        Assert.False(contexts.Registry.Exists(contextId));
        _preparation.RestoreStageCompleted = null;
    }

    [Fact]
    public void Trigger_modifier_lifecycle_restores_parameter_projection_and_negative_allocator()
    {
        var baseline = _preparation.Capture(_frame);
        var modifiers = Get<MobaTriggerModifierContinuousService>();
        Assert.True(modifiers.Add(1, 987, MobaSkillParamModifierKeys.Projectile.Ids.Speed, ModifierOp.Add, 5, 0));
        var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);
        Assert.Equal(15, Get<MobaSkillParamModifierService>().Projectile.ResolveSpeed(1, 10, record));
        var checkpoint = _preparation.Capture(_frame);
        Assert.True(modifiers.Remove(1, 987));
        Assert.Equal(10, Get<MobaSkillParamModifierService>().Projectile.ResolveSpeed(1, 10, record));
        _preparation.Commit(_preparation.Prepare(_frame, checkpoint));
        Assert.Equal(checkpoint, _preparation.Capture(_frame));
        Assert.Equal(15, Get<MobaSkillParamModifierService>().Projectile.ResolveSpeed(1, 10, record));
        _preparation.Commit(_preparation.Prepare(_frame, baseline));
        Assert.Equal(baseline, _preparation.Capture(_frame));
        Assert.Equal(10, Get<MobaSkillParamModifierService>().Projectile.ResolveSpeed(1, 10, record));
    }

    [Fact]
    public void Compensation_failure_explicitly_reports_unusable_restore()
    {
        var target = _preparation.Capture(_frame);
        AddRetainedBuff(1, 2);
        _preparation.RestoreStageCompleted = (_, _) => throw new InvalidOperationException("injected failure");
        var error = Assert.Throws<AggregateException>(() => _preparation.Commit(_preparation.Prepare(_frame, target)));
        Assert.Contains("stop replay", error.Message);
        Assert.Equal(2, error.InnerExceptions.Count);
        _preparation.RestoreStageCompleted = null;
    }

    [Theory]
    [InlineData("version")]
    [InlineData("frame")]
    [InlineData("missing-section")]
    [InlineData("duplicate-section")]
    [InlineData("unknown-section")]
    [InlineData("empty-section")]
    [InlineData("roster")]
    [InlineData("missing-context")]
    [InlineData("execution-nodes")]
    [InlineData("execution-identity")]
    [InlineData("skill-context-scope")]
    [InlineData("metadata-members")]
    [InlineData("metadata-duplicate")]
    [InlineData("retain")]
    [InlineData("parent")]
    [InlineData("source-id")]
    [InlineData("elapsed")]
    [InlineData("interval")]
    [InlineData("state")]
    [InlineData("configuration")]
    [InlineData("context-source")]
    public void Corrupt_joint_dependencies_are_rejected_without_touching_live_world(string change)
    {
        AddRetainedBuff(1, 2);
        var snapshot = _preparation.Capture(_frame);
        _preparation.Prepare(_frame, snapshot);
        var envelope = Decode(snapshot);
        var metadata = ReadMetadata(snapshot);
        var localBuff = metadata.Single(e => e.RetainId != 0);
        switch (change)
        {
            case "version": envelope.Version++; break;
            case "frame": envelope.Frame++; break;
            case "missing-section": envelope.Sections = envelope.Sections.Skip(1).ToArray(); break;
            case "duplicate-section": envelope.Sections[0] = envelope.Sections[1]; break;
            case "unknown-section": envelope.Sections[0].Name = "unknown"; break;
            case "empty-section": envelope.Sections[0].Payload = Array.Empty<byte>(); break;
            case "roster": envelope.ActorIds[0] = 9999; break;
            case "missing-context":
                var section = Find(envelope, "context-entities");
                var contexts = MemoryPackSerializer.Deserialize<MobaContextEntityRollbackPayload>(section.Payload);
                section.Payload = MemoryPackSerializer.Serialize(new MobaContextEntityRollbackPayload(1,
                    contexts.NextEntityId + 1, contexts.EntityIds.Append(contexts.NextEntityId).ToArray()));
                break;
            case "execution-nodes":
                var executionSection = Find(envelope, "execution-contexts");
                var execution = MobaSkillLifecycleCheckpointCodec.Decode<MobaExecutionRegistryCheckpoint>(executionSection.Payload);
                execution.Nodes = MemoryPackSerializer.Serialize(Array.Empty<MobaSkillExecutionContextRollbackEntry>());
                executionSection.Payload = MobaSkillLifecycleCheckpointCodec.Encode(execution);
                break;
            case "execution-identity":
                var identitySection = Find(envelope, "execution-contexts");
                var identityState = MobaSkillLifecycleCheckpointCodec.Decode<MobaExecutionRegistryCheckpoint>(identitySection.Payload);
                var nodes = MemoryPackSerializer.Deserialize<MobaSkillExecutionContextRollbackEntry[]>(identityState.Nodes);
                var first = nodes[0];
                nodes[0] = new MobaSkillExecutionContextRollbackEntry(first.ContextId, first.ParentContextId,
                    first.RootContextId, first.OwnerContextId, first.Kind, first.ConfigId + 1, first.SourceActorId,
                    first.TargetActorId, first.CreatedFrame, first.EndedFrame, first.EndReason, first.IsEnded,
                    first.TriggerId, first.OriginKind, first.OriginConfigId, first.CastFlowId, first.CombatFlags);
                identityState.Nodes = MemoryPackSerializer.Serialize(nodes);
                identitySection.Payload = MobaSkillLifecycleCheckpointCodec.Encode(identityState);
                break;
            case "metadata-members": metadata = Array.Empty<MobaBuffLocalRollbackEntry>(); break;
            case "metadata-duplicate": metadata = metadata.Append(metadata[0]).ToArray(); break;
            case "retain": localBuff.RetainId++; break;
            case "parent":
                var skillSection = Find(envelope, "skill-runtime");
                var local = MemoryPackSerializer.Deserialize<MobaSkillRuntimeLocalRollbackPayload>(skillSection.Payload);
                var skills = MemoryPackSerializer.Deserialize<MobaSkillRuntimeRollbackPayload>(local.RuntimeState);
                var orphan = skills.Retains.Single(r => r.RetainId == localBuff.RetainId);
                var retains = skills.Retains.Select(r => r.RetainId == orphan.RetainId
                    ? new MobaSkillRuntimeRetainRollbackEntry(r.RetainId, long.MaxValue, r.Generation,
                        r.RootContextId, r.ChildKind, r.ChildId, r.ChildContextId, r.ChildConfigId) : r).ToArray();
                var corrupt = new MobaSkillRuntimeRollbackPayload(skills.Version, skills.NextRuntimeId,
                    skills.NextRetainId, skills.NextGeneration, skills.Runtimes, retains);
                skillSection.Payload = MemoryPackSerializer.Serialize(new MobaSkillRuntimeLocalRollbackPayload(
                    local.Version, MemoryPackSerializer.Serialize(corrupt), local.ExecutionContextNodes, local.ExecutionContextNextId));
                break;
            case "skill-context-scope":
                var scopedSection = Find(envelope, "skill-runtime");
                var scoped = MemoryPackSerializer.Deserialize<MobaSkillRuntimeLocalRollbackPayload>(scopedSection.Payload);
                scopedSection.Payload = MemoryPackSerializer.Serialize(new MobaSkillRuntimeLocalRollbackPayload(scoped.Version,
                    scoped.RuntimeState, Array.Empty<MobaSkillExecutionContextRollbackEntry>(), scoped.ExecutionContextNextId));
                break;
            case "source-id": localBuff.ModifierSourceId = int.MaxValue; break;
            case "elapsed": localBuff.ElapsedRaw = -1; break;
            case "interval": localBuff.IntervalRemainingRaw++; break;
            case "state": localBuff.State = (int)ContinuousState.Expired; break;
            case "configuration": localBuff.Configuration += "changed"; break;
            case "context-source": localBuff.ContextSource.SourceActorId++; break;
        }
        Find(envelope, "buff-local").Payload = MobaSkillLifecycleCheckpointCodec.Encode(metadata);
        var before = _preparation.Capture(_frame);
        Assert.ThrowsAny<Exception>(() => _preparation.Prepare(_frame, MobaSkillLifecycleCheckpointCodec.Encode(envelope)));
        Assert.Equal(before, _preparation.Capture(_frame));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ordinary_continuous_membership_stays_strict(bool remove)
    {
        var manager = Get<MobaContinuousManager>();
        Get<MobaConfigDatabase>().TryGetBuff(10010000, out var buff);
        var ordinary = new OrdinaryRuntime(buff.Modifiers);
        if (remove) Assert.True(manager.TryActivate(ordinary));
        var snapshot = _preparation.Capture(_frame);
        if (remove) manager.TryEnd(ordinary);
        else Assert.True(manager.TryActivate(ordinary));
        var current = _preparation.Capture(_frame);

        Assert.Throws<InvalidOperationException>(() => _preparation.Prepare(_frame, snapshot));

        Assert.Equal(current, _preparation.Capture(_frame));
    }

    [Fact]
    public void Unowned_buff_continuous_is_not_silently_adopted_by_the_group()
    {
        var snapshot = _preparation.Capture(_frame);
        Get<MobaConfigDatabase>().TryGetBuff(10010000, out var buff);
        var orphan = new BuffContinuousRuntime(buff, 2, 1, 10, null);
        var manager = Get<MobaContinuousManager>();
        Assert.True(manager.TryActivate(orphan));
        var registered = manager.GetAllContinuous().ToArray();
        var nextId = manager.NextModifierSourceId;

        Assert.Contains("repository owner", Assert.Throws<InvalidOperationException>(
            () => _preparation.Prepare(_frame, snapshot)).Message);

        Assert.Equal(registered, manager.GetAllContinuous());
        Assert.Equal(nextId, manager.NextModifierSourceId);
        manager.TryEnd(orphan);
    }

    [Theory]
    [InlineData("missing-field")]
    [InlineData("duplicate-property")]
    [InlineData("trailing")]
    [InlineData("null")]
    public void Invalid_json_is_rejected_before_any_service_mutation(string change)
    {
        var snapshot = _preparation.Capture(_frame);
        var json = Encoding.UTF8.GetString(snapshot);
        if (change == "missing-field")
        {
            var obj = JObject.Parse(json);
            obj.Remove("Frame");
            json = obj.ToString();
        }
        else if (change == "duplicate-property") json = json.Insert(1, "\"Version\":1,");
        else if (change == "trailing") json += "{}";
        else json = "null";
        Assert.ThrowsAny<Exception>(() => _preparation.Prepare(_frame, Encoding.UTF8.GetBytes(json)));
        Assert.Equal(snapshot, _preparation.Capture(_frame));
    }

    private BuffRuntime AddRetainedBuff(int targetId, int sourceId, int buffId = 10010000)
    {
        const int skillId = 10010101;
        var executions = Get<MobaExecutionContextRegistry>();
        var root = executions.Create(new MobaExecutionContextCreateRequest(MobaExecutionKind.SkillCast,
            skillId, sourceId, targetId, frame: _frame.Value));
        var skills = Get<MobaSkillCastRuntimeService>();
        var runtime = skills.Create(new MobaSkillCastRuntimeCreateRequest(skillId, 1, 1, 1,
            sourceId, targetId, Vec3.Zero, Vec3.Zero, root.ContextId));
        var buff = BuffRepository.RentRuntime();
        buff.BuffId = buffId;
        buff.SourceId = sourceId;
        buff.StackCount = 1;
        buff.Remaining = 10;
        buff.SkillRuntimeHandle = runtime.Handle;
        var origin = new BuffOriginContext(root.ContextId, sourceId, targetId, MobaExecutionKind.SkillCast,
            skillId, root.ContextId, runtime.Handle);
        new BuffContextRegistry(executions, Get<MobaRuntimeContextService>(), Get<IFrameTime>())
            .EnsureBuffContext(buff, buffId, sourceId, targetId, origin);
        var child = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Buff,
            buff.SourceContextId, buff.SourceContextId, buffId);
        var handle = runtime.Handle;
        Assert.True(skills.RetainChild(in handle, in child, out var retain));
        buff.SkillRuntimeRetainHandle = retain;
        Assert.True(skills.MarkPipelineEnded(runtime.RuntimeId, MobaSkillRuntimeEndReason.PipelineCompleted));
        Assert.True(Get<MobaActorRegistry>().TryGet(targetId, out var actor));
        var list = new BuffRepository().GetOrCreateList(actor);
        list.Add(buff);
        BuffRepository.RegisterRuntime(list, buff);
        Assert.True(Get<MobaConfigDatabase>().TryGetBuff(buffId, out var config));
        var requirements = BuffTagLifecycle.ResolveRequirements(config, Get<IMobaContinuousTagTemplateRegistry>());
        Assert.True(new BuffContinuousBindingService(Get<MobaContinuousManager>(), Get<IMobaEffectiveTagQueryService>())
            .EnsureActive(buff, config, sourceId, targetId, 10, requirements));
        return buff;
    }

    private void RemoveBuff(int targetId, BuffRuntime buff)
    {
        Get<MobaContinuousManager>().TryEnd(buff.Continuous, ContinuousEndReason.Completed);
        Get<MobaRuntimeContextService>().SnapshotAndDestroyBuffContext(buff, MobaRuntimeContextLifecycleState.Ended, _frame.Value);
        Get<MobaActorRegistry>().TryGet(targetId, out var actor);
        Assert.True(actor.buffs.Active.Remove(buff));
        BuffRepository.MarkDirty(actor.buffs.Active);
        var retain = buff.SkillRuntimeRetainHandle;
        Assert.True(Get<MobaSkillCastRuntimeService>().ReleaseChild(in retain));
        BuffRepository.ReleaseRuntime(buff);
    }

    private T Get<T>() => _world.Services.Resolve<T>();
    private static MobaSkillLifecycleCheckpoint Decode(byte[] payload) =>
        MobaSkillLifecycleCheckpointCodec.Decode<MobaSkillLifecycleCheckpoint>(payload);
    private static MobaSkillLifecycleSection Find(MobaSkillLifecycleCheckpoint checkpoint, string name) =>
        checkpoint.Sections.Single(s => s.Name == name);
    private static MobaBuffLocalRollbackEntry[] ReadMetadata(byte[] payload) =>
        MobaSkillLifecycleCheckpointCodec.Decode<MobaBuffLocalRollbackEntry[]>(Find(Decode(payload), "buff-local").Payload);

    private sealed class OrdinaryRuntime : MobaContinuousRuntimeBase
    {
        private readonly OrdinaryConfig _config;
        public OrdinaryRuntime(IReadOnlyList<IMobaContinuousModifierSpec> modifiers) => _config = new OrdinaryConfig(this, modifiers);
        public override IContinuousConfig Config => _config;

        private sealed class OrdinaryConfig : MobaContinuousConfigBase
        {
            private readonly OrdinaryRuntime _runtime;
            public OrdinaryConfig(OrdinaryRuntime runtime, IReadOnlyList<IMobaContinuousModifierSpec> modifiers)
                : base(10, null, modifiers) => _runtime = runtime;
            public override string Id => "test.ordinary";
            public override long OwnerId => 1;
            public override int OwnerActorId => 1;
            public override int ModifierSourceId => _runtime.AllocatedModifierSourceId;
            public override GameplayTagSource TagSource => GameplayTagSource.System;
        }
    }

    public void Dispose()
    {
        _bootstrapper.Stop();
        _bootstrapper.Dispose();
    }
}
