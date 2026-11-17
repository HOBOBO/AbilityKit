using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.Search;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Continuous;
using AbilityKit.Deterministic;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Pipeline;
using AbilityKit.Modifiers;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class MobaSkillFlowAcceptanceTests
{
    private const int ActorId = 8401;
    private const int Instant = 8411;
    private const int Charge = 8412;
    private const int Volley = 8413;

    [Fact]
    public void Recovery_rejects_early_and_failed_input_then_completes_normally_preserving_children()
    {
        using var scope = new Scope(recoveryStartMs: 200);
        Assert.True(scope.Start(Volley, 3, SkillCastPolicy.Default).Success);
        Assert.False(scope.Start(Instant, 1, SkillCastPolicy.Default).Success);
        Assert.False(scope.Runner.FinishRecoveryForMovement());
        scope.Runner.Step(.21f);
        Assert.True(scope.Runner.TryGetLatestRunningBySlot(3, out var old));
        Assert.True(scope.Runtimes.TryGetByContext(old.InstanceId, out var original));
        var handle = original.Handle;
        Assert.True(scope.Runtimes.RetainChild(handle,
            new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Projectile, 71, 72, 73), out _));
        scope.Mana.Current = Fixed64.Zero;
        Assert.False(scope.Start(Instant, 1, SkillCastPolicy.Default).Success);
        Assert.True(scope.Runner.TryGetLatestRunningBySlot(3, out _));
        Assert.False(original.PipelineEnded);
        scope.Mana.Current = Fixed64.FromInt32(100);
        Assert.True(scope.Start(Instant, 1, SkillCastPolicy.Default).Success);
        Assert.Equal(Fixed64.FromInt32(90), scope.Mana.Current);
        Assert.False(scope.Runner.TryGetLatestRunningBySlot(3, out _));
        Assert.True(scope.Runtimes.TryGet(handle, out var retained));
        Assert.True(retained.PipelineEnded);
        Assert.False(retained.IsEnded);
        Assert.Equal(MobaSkillRuntimeEndReason.PipelineCompleted, retained.EndReason);
        Assert.Equal(new[] { 8441, 8442, 8443 }, scope.ExecutedEffects);
    }

    [Fact]
    public void Recovery_restores_exact_eligibility_and_no_input_keeps_full_timeline()
    {
        using var scope = new Scope(recoveryStartMs: 200);
        Assert.True(scope.Start(Volley, 3, SkillCastPolicy.Default).Success);
        scope.Runner.Step(.21f);
        var entries = new List<SkillRunnerRollbackEntry>();
        scope.Runner.CaptureRollbackEntries(entries);
        var runtimeState = scope.Runtimes.CaptureRollbackSnapshot();
        var contexts = scope.ExecutionContexts.CaptureLifecycleSnapshot();
        scope.Runner.Step(.05f);
        Assert.True(scope.Runner.HasRunning); // Recovery alone must not shorten 300ms.
        scope.Runner.Step(.05f);
        Assert.False(scope.Runner.HasRunning);
        scope.Runner.DetachForRollback();
        scope.Runtimes.RestoreRollbackSnapshot(in runtimeState);
        scope.ExecutionContexts.RestoreLifecycle(contexts);
        scope.Restore(entries.Single());
        scope.ExecutedEffects.Clear();
        Assert.True(scope.Runner.FinishRecoveryForMovement());
        Assert.False(scope.Runner.HasRunning);
        Assert.Empty(scope.ExecutedEffects); // Restore/handoff cannot replay any submitted effect.
    }

    [Fact]
    public void Recovery_configuration_cannot_skip_pending_or_nested_gameplay()
    {
        var timeline = new SkillPhaseDTO { Type = (int)SkillPhaseType.Timeline,
            Timeline = new SkillTimelinePhaseDTO { DurationMs = 500, RecoveryStartMs = 200,
                Events = new[] { new SkillTimelineEventDTO { AtMs = 250, EffectId = 8441 } } } };
        var flow = new SkillFlowDTO { Phases = new[] { timeline } };
        Assert.Throws<ArgumentException>(() => SkillRecoveryContract.Validate(flow));
        timeline.Timeline.Events = Array.Empty<SkillTimelineEventDTO>();
        flow.Phases = new[] { timeline, new SkillPhaseDTO { Type = (int)SkillPhaseType.RulePlan } };
        Assert.Throws<ArgumentException>(() => SkillRecoveryContract.Validate(flow));
        flow.Phases = new[] { new SkillPhaseDTO { Type = (int)SkillPhaseType.Parallel, Children = new[] { timeline } } };
        Assert.Throws<ArgumentException>(() => SkillRecoveryContract.Validate(flow));
        flow.Phases = new[] { timeline };
        SkillRecoveryContract.Validate(flow);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Diagnostic_ids_are_reserved_zero_and_legacy_skill_runner_payloads_are_rejected(int version)
    {
        using var scope = new Scope();
        Assert.True(scope.Start(Volley, 3, SkillCastPolicy.Default).Success);
        var entries = new List<SkillRunnerRollbackEntry>();
        scope.Runner.CaptureRollbackEntries(entries);
        var snapshot = new SkillRunnerRollbackSnapshot { Entries = entries.ToArray() };
        var canonical = SkillRunnerRollbackCodec.Serialize(snapshot);
        entries[0].DiagnosticCommandId = 987654321;
        Assert.Equal(canonical, SkillRunnerRollbackCodec.Serialize(snapshot));

        BitConverter.GetBytes(version).CopyTo(canonical, 0);
        Assert.Throws<System.IO.InvalidDataException>(() => SkillRunnerRollbackCodec.Deserialize(canonical));
        entries[0].DiagnosticCommandId = 0L;
        var fresh = SkillRunnerRollbackCodec.Serialize(snapshot);
        entries[0].Sequence++;
        Assert.NotEqual(fresh, SkillRunnerRollbackCodec.Serialize(snapshot));
    }

    [Fact]
    public void Exact_deployed_recast_commits_without_spending_or_overwriting_original_cooldown()
    {
        using var scope = new Scope();
        var original = scope.NewContext(Instant, 1);
        Assert.True(scope.Runtimes.TryBindChildChannel(original.RuntimeHandle, 41));
        var configs = new MobaConfigDatabase();
        Assert.True(configs.ReloadFromDtoArrays(new Dictionary<Type, Array>
        {
            [typeof(SkillDTO)] = new[] { new SkillDTO { Id = Instant, CastFlowId = 8491 } },
            [typeof(SkillFlowDTO)] = new[] { new SkillFlowDTO { Id = 8491, RecastChannelId = 41 } },
        }, strict: false).Succeeded);
        scope.Services.Add(configs);
        var loadout = new MobaSkillLoadoutService(scope.Services.Resolve<MobaActorLookupService>());
        loadout.OnInit(scope.Services);
        Assert.False(loadout.IsIndependentRecastReady(ActorId, 1, Instant));
        var child = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Area, 701, 702, 703);
        Assert.True(scope.Runtimes.RetainChild(original.RuntimeHandle, child, out _));
        Assert.True(loadout.IsIndependentRecastReady(ActorId, 1, Instant));
        Assert.False(loadout.IsIndependentRecastReady(ActorId, 1, Charge));
        scope.Runtimes.MarkPipelineEnded(original.RuntimeHandle, MobaSkillRuntimeEndReason.PipelineCompleted);
        scope.SkillOne.CooldownEndTimeMs = 10000;
        scope.SkillOne.CooldownDurationMs = 10000;
        scope.SkillOne.CooldownGroupId = 19;
        scope.SkillOne.IgnoreGlobalCooldown = true;
        var specification = new SkillEconomyPhaseDTO
        {
            ResourceType = (int)ResourceType.Mana, UseResolvedResourceCost = false, ResourceAmount = 0,
            StartSkillCooldown = false, RequireExplicitCommit = true,
        };
        var unbound = scope.NewContext(8499, 1, Instant);
        Assert.False(scope.Economy.TryReserve(unbound, specification, out _));
        var recast = scope.NewContext(8499, 1, Instant);
        Assert.True(scope.Runtimes.TrySelectRecastArea(ActorId, 41, out var binding, out _, 1, Instant));
        Assert.False(scope.Runtimes.TrySelectRecastArea(ActorId, 41, out _, out _, 2, Instant));
        Assert.True(scope.Runtimes.BindRecastArea(recast.RuntimeHandle, binding));
        Assert.True(scope.Economy.TryReserve(recast, specification, out var failure), failure);
        Assert.True(scope.Economy.Commit(recast.RuntimeHandle));
        Assert.Equal(Fixed64.FromInt32(100), scope.Mana.Current);
        Assert.Equal(10000, scope.SkillOne.CooldownEndTimeMs);
        Assert.Equal(10000, scope.SkillOne.CooldownDurationMs);
        Assert.Equal(19, scope.SkillOne.CooldownGroupId);
        Assert.True(scope.SkillOne.IgnoreGlobalCooldown);
        Assert.True(scope.Runtimes.TryClaimChildTerminal(binding));
        Assert.False(loadout.IsIndependentRecastReady(ActorId, 1, Instant));
        var late = scope.NewContext(8499, 1, Instant);
        Assert.False(scope.Runtimes.TryGetRecastArea(recast.RuntimeHandle, out _));
        Assert.False(scope.Runtimes.BindRecastArea(late.RuntimeHandle, binding));
        Assert.False(scope.Economy.TryReserve(late, specification, out _));
    }

    [Fact]
    public void Channel_window_rollback_preserves_tick_boundary()
    {
        using var scope = new Scope();
        var windows = new MobaSkillWindowRuntimeService(scope.Runtimes);
        scope.Services.Add(windows);
        var context = scope.NewContext(Charge, 2);
        Assert.True(scope.Library.TryGet(Charge, out _, out _, out var config, out _));
        var pipeline = new SkillCastPipeline();
        pipeline.AddPhase(new SkillWindowPhase(new AbilityPipelinePhaseId("channel"),
            new SkillWindowPhaseDTO
            {
                WindowId = "rollback-channel", Kind = (int)SkillWindowKind.Channel,
                DurationMs = 400, ChannelIntervalMs = 100
            }, null));
        var run = pipeline.Start(config, context);
        var handle = context.RuntimeHandle;
        run.Tick(0f);
        run.Tick(0.15f);
        Assert.True(windows.TryGetSnapshot(in handle, out var first));
        Assert.Equal(1, first.ChannelTick);
        var runtimeState = scope.Runtimes.CaptureRollbackSnapshot();
        var contextState = context.CaptureRollbackState();
        var runBytes = PipelineRollbackStateCodec.Serialize(SkillPipelineRollback.Capture(run, config));

        run.Tick(0.1f);
        Assert.True(windows.TryGetSnapshot(in handle, out var later));
        Assert.Equal(2, later.ChannelTick);
        SkillPipelineRollback.Detach(run);
        scope.Runtimes.RestoreRollbackSnapshot(in runtimeState);

        var aimPos = contextState.AimPos;
        var aimDir = contextState.AimDir;
        var request = new SkillCastRequest(Charge, 2, ActorId, 0, in aimPos, in aimDir,
            scope.Services, null, null, null);
        var trigger = new SkillCastContext();
        trigger.Initialize(in request, 1);
        trigger.RuntimeHandle = context.RuntimeHandle;
        trigger.RuntimeId = context.RuntimeId;
        trigger.SourceContextId = context.SourceContextId;
        var restoredContext = new SkillPipelineContext();
        restoredContext.Initialize(null, in request, trigger);
        restoredContext.RestoreRollbackState(in contextState);
        var restored = SkillPipelineRollback.Restore(pipeline, config, restoredContext,
            PipelineRollbackStateCodec.Deserialize(runBytes));
        Assert.True(windows.TryGetSnapshot(in handle, out var rewind));
        Assert.Equal(1, rewind.ChannelTick);
        restored.Tick(0.1f);
        Assert.True(windows.TryGetSnapshot(in handle, out var replay));
        Assert.Equal(2, replay.ChannelTick);
        Assert.Equal(EAbilityPipelineState.Executing, restored.State);
        restored.Interrupt();
    }

    [Fact]
    public void Timeline_rollback_restores_mid_cast_without_replaying_prior_effects()
    {
        using var scope = new Scope();
        Assert.True(scope.Start(Volley, 3, SkillCastPolicy.Default).Success);
        scope.Runner.Step(0.15f);
        Assert.Equal(new[] { 8441, 8442 }, scope.ExecutedEffects);

        var entries = new List<SkillRunnerRollbackEntry>();
        scope.Runner.CaptureRollbackEntries(entries);
        var bytes = SkillRunnerRollbackCodec.Serialize(new SkillRunnerRollbackSnapshot
        {
            Entries = entries.ToArray()
        });
        var runtimeState = scope.Runtimes.CaptureRollbackSnapshot();
        var contextNodes = scope.ExecutionContexts.CaptureLifecycleSnapshot();

        scope.Runner.Step(0.2f);
        Assert.Equal(new[] { 8441, 8442, 8443 }, scope.ExecutedEffects);

        scope.Runner.DetachForRollback();
        scope.Runtimes.RestoreRollbackSnapshot(in runtimeState);
        scope.ExecutionContexts.RestoreLifecycle(contextNodes);
        scope.Restore(SkillRunnerRollbackCodec.Deserialize(bytes).Entries[0]);
        var restoredEntries = new List<SkillRunnerRollbackEntry>();
        scope.Runner.CaptureRollbackEntries(restoredEntries);
        Assert.Equal(bytes, SkillRunnerRollbackCodec.Serialize(new SkillRunnerRollbackSnapshot
        {
            Entries = restoredEntries.ToArray()
        }));
        scope.ExecutedEffects.Clear();
        scope.Runner.Step(0.2f);

        Assert.Equal(new[] { 8443 }, scope.ExecutedEffects);
        Assert.False(scope.Runner.HasRunning);
    }

    [Fact]
    public void Three_configured_skills_prewarm_and_instant_cost_commits_at_the_explicit_point()
    {
        using var scope = new Scope();
        Assert.True(scope.Library.PrewarmAll().Succeeded);
        Assert.Equal(3, scope.Library.CachedSkillCount);

        var result = scope.Start(Instant, 1, SkillCastPolicy.Default);

        Assert.True(result.Success, result.FailReason);
        Assert.Equal(Fixed64.FromInt32(90), scope.Mana.Current);
        Assert.Equal(0, scope.Economy.PendingTransactionCount);
        Assert.True(scope.SkillOne.CooldownEndTimeMs > 0);
    }

    [Fact]
    public void Explicit_reservation_without_commit_refunds_even_when_pipeline_completes()
    {
        using var scope = new Scope();
        var context = scope.NewContext(Instant, 1);
        var specification = new SkillEconomyPhaseDTO
        {
            Operation = (int)SkillEconomyOperation.ReserveCast,
            ResourceType = (int)ResourceType.Mana,
            ResourceAmount = 10,
            UseResolvedResourceCost = false,
            RequireExplicitCommit = true,
            RefundBeforeCommit = false,
        };

        Assert.True(scope.Economy.TryReserve(context, specification, out var failure), failure);
        Assert.Equal(Fixed64.FromInt32(90), scope.Mana.Current);
        Assert.True(scope.Runtimes.MarkPipelineEnded(context.RuntimeHandle,
            MobaSkillRuntimeEndReason.PipelineCompleted));
        Assert.Equal(Fixed64.FromInt32(100), scope.Mana.Current);
        Assert.Equal(0, scope.Economy.PendingTransactionCount);
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(.6f, 4800)]
    public void Commit_factor_is_generic_and_freezes_once_across_transaction_restore(float factor, int expected)
    {
        using var scope = new Scope();
        using var parameters = new MobaSkillParamModifierService();
        scope.Services.Add(parameters);
        scope.Economy.OnInit(scope.Services);
        var context = scope.NewContext(Instant, 1);
        var handle = context.RuntimeHandle;
        scope.Runtimes.PrepareCooldownForCommit(in handle, 8000);
        var specification = new SkillEconomyPhaseDTO { ChargeCost = 0,
            ResourceType = (int)ResourceType.Mana, ResourceAmount = 10,
            UseResolvedResourceCost = false, UseResolvedSkillCooldown = true,
            StartSkillCooldown = true, RequireExplicitCommit = true };
        Assert.True(scope.Economy.TryReserve(context, specification, out var failure), failure);
        var reserved = scope.Economy.CaptureRollbackSnapshot();
        var prepared = scope.Runtimes.CaptureRollbackSnapshot();
        parameters.AddModifier(MobaModifierOwnerRef.Actor(ActorId), new ModifierData {
            Key = MobaSkillParamModifierKeys.Skill.CooldownCommitFactor,
            Op = ModifierOp.Mul, Magnitude = MagnitudeSource.Fixed(factor), SourceId = 718,
        }, new MobaParameterSelector(effectiveSkillId: Instant));
        Assert.True(scope.Economy.Commit(in handle));
        Assert.Equal(expected, scope.SkillOne.CooldownDurationMs);
        var end = scope.SkillOne.CooldownEndTimeMs;
        Assert.True(scope.Runtimes.TryResolveCooldownRefund(in handle, ActorId, Instant, 1, .5, out var refund));
        Assert.Equal(expected / 2, refund);
        parameters.ClearSource(MobaModifierOwnerRef.Actor(ActorId), 718);
        Assert.True(scope.Economy.Commit(in handle));
        Assert.Equal(end, scope.SkillOne.CooldownEndTimeMs);
        Assert.Equal(expected, scope.SkillOne.CooldownDurationMs);
        // Restore before commit: the final total must be sampled again from the
        // current restored sources, rather than reusing the discarded future.
        scope.Runtimes.RestoreRollbackSnapshot(in prepared);
        scope.Economy.RestoreRollbackSnapshot(in reserved);
        Assert.True(scope.Economy.Commit(in handle));
        Assert.Equal(8000, scope.SkillOne.CooldownDurationMs);
        Assert.Equal(Fixed64.FromInt32(90), scope.Mana.Current);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Commit_factor_flow_requires_a_single_ordered_explicit_economy_pair(int invalidShape)
    {
        var reserve = new SkillPhaseDTO { Type = (int)SkillPhaseType.Economy,
            Economy = new SkillEconomyPhaseDTO { RequireExplicitCommit = true } };
        var commit = new SkillPhaseDTO { Type = (int)SkillPhaseType.CommitPoint,
            CommitPoint = new SkillCommitPointPhaseDTO { RequireEconomyReservation = true } };
        var flow = new SkillFlowDTO { Id = 999, UseCommitCooldownFactor = true,
            Phases = new[] { reserve, commit } };
        SkillCommitCooldownContract.Validate(flow);
        switch (invalidShape)
        {
            case 0: flow.Phases = new[] { commit, reserve }; break;
            case 1: reserve.Economy.RequireExplicitCommit = false; break;
            case 2: commit.CommitPoint.RequireEconomyReservation = false; break;
            case 3: flow.Phases = new[] { reserve, commit, commit }; break;
            case 4: flow.Phases = new[] { new SkillPhaseDTO { Type = (int)SkillPhaseType.Parallel,
                Children = new[] { reserve, commit } } }; break;
        }
        Assert.Throws<ArgumentException>(() => SkillCommitCooldownContract.Validate(flow));
        flow.UseCommitCooldownFactor = false;
        SkillCommitCooldownContract.Validate(flow);
    }

    [Theory]
    [InlineData(MobaSkillRuntimeEndReason.PipelineCompleted)]
    [InlineData(MobaSkillRuntimeEndReason.RollbackCleanup)]
    public void Uncommitted_factor_cast_refunds_without_starting_cooldown(MobaSkillRuntimeEndReason reason)
    {
        using var scope = new Scope();
        var context = scope.NewContext(Instant, 1);
        var handle = context.RuntimeHandle;
        scope.Runtimes.PrepareCooldownForCommit(in handle, 8000);
        var specification = new SkillEconomyPhaseDTO { ChargeCost = 0,
            ResourceType = (int)ResourceType.Mana, ResourceAmount = 10,
            UseResolvedResourceCost = false, RequireExplicitCommit = true };
        Assert.True(scope.Economy.TryReserve(context, specification, out var failure), failure);
        Assert.Equal(Fixed64.FromInt32(90), scope.Mana.Current);
        Assert.True(scope.Runtimes.MarkPipelineEnded(in handle, reason));
        Assert.Equal(Fixed64.FromInt32(100), scope.Mana.Current);
        Assert.Equal(0, scope.SkillOne.CooldownEndTimeMs);
        Assert.Equal(0, scope.Economy.PendingTransactionCount);
        Assert.False(scope.Economy.Commit(in handle));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Effect_failure_obeys_timeline_abort_policy(bool abortOnFailure)
    {
        using var scope = new Scope();
        scope.FailedEffectId = 8441;
        scope.VolleyFlow.Phases[0].Timeline.Events[0].AbortOnFailure = abortOnFailure;

        var result = scope.Start(Volley, 3, SkillCastPolicy.Default);

        Assert.Equal(!abortOnFailure, result.Success);
        Assert.Equal(new[] { 8441 }, scope.ExecutedEffects);
        Assert.Equal(!abortOnFailure, scope.Runner.TryGetLatestRunningBySlot(3, out _));
    }

    [Fact]
    public void Charge_can_be_interrupted_by_higher_priority_but_not_lower_priority()
    {
        using var scope = new Scope();
        var charge = new SkillCastPolicy(false, false, conflictGroup: 7, interruptPriority: 2);
        var weak = new SkillCastPolicy(false, true, conflictGroup: 7, interruptPriority: 1);
        var strong = new SkillCastPolicy(false, true, conflictGroup: 7, interruptPriority: 3);

        Assert.True(scope.Start(Charge, 2, charge).Success);
        Assert.True(scope.Runner.TryGetLatestRunningBySlot(2, out _));
        Assert.False(scope.Start(Charge, 3, weak).Success);
        Assert.True(scope.Runner.TryGetLatestRunningBySlot(2, out _));
        Assert.True(scope.Start(Charge, 3, strong).Success);
        Assert.False(scope.Runner.TryGetLatestRunningBySlot(2, out _));
        Assert.True(scope.Runner.TryGetLatestRunningBySlot(3, out _));
    }

    [Fact]
    public void Conflict_preflight_never_partially_interrupts_existing_casts()
    {
        using var scope = new Scope();
        var first = new SkillCastPolicy(true, false, conflictGroup: 7);
        var protectedCast = new SkillCastPolicy(true, false, uninterruptible: true);
        var incoming = new SkillCastPolicy(false, true, conflictGroup: 7, interruptPriority: 10);
        Assert.True(scope.Start(Charge, 2, first).Success);
        Assert.True(scope.Start(Charge, 3, protectedCast).Success);

        Assert.False(scope.Start(Charge, 1, incoming).Success);

        Assert.True(scope.Runner.TryGetLatestRunningBySlot(2, out _));
        Assert.True(scope.Runner.TryGetLatestRunningBySlot(3, out _));
    }

    [Fact]
    public void Volley_executes_three_ordered_effects_and_target_updates_are_explicit()
    {
        using var scope = new Scope();
        var flow = Assert.IsType<SkillFlowDTO>(scope.VolleyFlow);
        var timeline = Assert.Single(flow.Phases);
        var events = timeline.Timeline.Events;
        Assert.Equal(new[] { 0, 100, 200 }, Array.ConvertAll(events, e => e.AtMs));
        Assert.All(events, e => Assert.True(e.AbortOnFailure));
        var result = scope.Start(Volley, 3, SkillCastPolicy.Default);
        Assert.True(result.Success, result.FailReason);
        Assert.Equal(new[] { 8441 }, scope.ExecutedEffects);
        scope.Runner.Step(0.1f);
        Assert.Equal(new[] { 8441, 8442 }, scope.ExecutedEffects);
        scope.Runner.Step(0.1f);
        Assert.Equal(new[] { 8441, 8442, 8443 }, scope.ExecutedEffects);

        var aim = Vec3.Zero;
        var direction = Vec3.Forward;
        var request = new SkillCastRequest(Volley, 3, ActorId, 500, in aim, in direction,
            scope.Services, null, null, null);
        var context = new SkillPipelineContext();
        context.Initialize(new object(), in request);
        context.UpdateInput(in aim, in direction, 0, true, false, true);
        Assert.Equal(Vec3.Zero, context.AimPos);
        Assert.Equal(0, context.TargetActorId);
    }

    [Theory]
    [InlineData(SkillTargetLostPolicy.Cancel, false)]
    [InlineData(SkillTargetLostPolicy.KeepAim, true)]
    public void Lost_target_uses_configured_policy(SkillTargetLostPolicy lostPolicy, bool staysRunning)
    {
        using var scope = new Scope();
        var policy = new SkillCastPolicy(false, false, targetLostPolicy: lostPolicy);
        Assert.True(scope.Start(Charge, 2, policy).Success);
        var aim = Vec3.Zero;
        var direction = Vec3.Forward;
        Assert.True(scope.Runner.UpdateInputBySlot(2, in aim, in direction, 500,
            false, false, true));

        scope.Runner.Step(0.1f);

        Assert.Equal(staysRunning, scope.Runner.TryGetLatestRunningBySlot(2, out var snapshot));
        if (staysRunning)
        {
            Assert.Equal(0, snapshot.TargetActorId);
            Assert.Equal(Vec3.Zero, snapshot.AimPos);
            Assert.True(scope.Runtimes.TryGetByContext(snapshot.InstanceId, out var runtime));
            Assert.Equal(0, runtime.TargetActorId);
        }
    }

    [Fact]
    public void Instant_effect_does_not_execute_before_the_target_loss_check()
    {
        using var scope = new Scope();
        var cancel = new SkillCastPolicy(false, false, targetLostPolicy: SkillTargetLostPolicy.Cancel);
        var keepAim = new SkillCastPolicy(false, false, targetLostPolicy: SkillTargetLostPolicy.KeepAim);

        Assert.False(scope.Start(Volley, 3, cancel, targetActorId: 500).Success);
        Assert.Empty(scope.ExecutedEffects);
        Assert.True(scope.Start(Volley, 3, keepAim, targetActorId: 500).Success);
        Assert.Equal(new[] { 8441 }, scope.ExecutedEffects);
        Assert.True(scope.Runner.TryGetLatestRunningBySlot(3, out var snapshot));
        Assert.Equal(0, snapshot.TargetActorId);
    }

    [Fact]
    public void Reacquire_resolves_a_new_alive_enemy_before_the_first_phase()
    {
        using var scope = new Scope();
        scope.AddEnemy(8402);
        var policy = new SkillCastPolicy(false, false, targetLostPolicy: SkillTargetLostPolicy.Reacquire);
        Assert.Equal(8402, scope.FindEnemy());
        Assert.True(scope.Services.Resolve<MobaCombatRulesService>().IsAlive(8402));
        Assert.True(scope.Services.Resolve<MobaActorLookupService>().TryGetActorEntity(ActorId, out var caster));
        Assert.True(caster.hasTransform);
        Assert.True(scope.Services.Resolve<MobaConfigDatabase>().TryGetSkill(Charge, out var skill));
        Assert.Equal(5, skill.Range);

        var result = scope.Start(Charge, 2, policy, targetActorId: 500);

        Assert.True(result.Success, result.FailReason);
        Assert.True(scope.Runner.TryGetLatestRunningBySlot(2, out var snapshot));
        Assert.Equal(8402, snapshot.TargetActorId);
        Assert.True(scope.Runtimes.TryGetByContext(snapshot.InstanceId, out var runtime));
        Assert.Equal(8402, runtime.TargetActorId);
    }

    private sealed class Scope : IDisposable
    {
        private readonly Contexts _contexts = new();
        private readonly ActorIdIndex _index;
        private readonly MobaActorRegistry _registry = new();
        private readonly MobaEntityManager _entities = new(null);
        private readonly MobaExecutionContextRegistry _executionContexts = new();
        private int _sequence;

        public Scope(int recoveryStartMs = -1)
        {
            var actor = _contexts.actor.CreateEntity();
            actor.AddActorId(ActorId);
            actor.AddTeam(Team.Team1);
            actor.AddTransform(new Transform3(Vec3.Zero, Quat.Identity, Vec3.One));
            SkillOne = NewSkill(Instant);
            actor.AddSkillLoadout(0, new[] { SkillOne, NewSkill(Charge), NewSkill(Volley) }, Array.Empty<PassiveSkillRuntime>());
            Mana = new ResourceState { Current = Fixed64.FromInt32(100), LastMax = Fixed64.FromInt32(100) };
            actor.AddResourceContainer(new ResourceContainer
            {
                Map = new Dictionary<ResourceType, ResourceState> { [ResourceType.Mana] = Mana },
            }, true);
            _registry.Register(ActorId, actor);
            _index = new ActorIdIndex(_contexts);
            var lookup = new MobaActorLookupService(_index, _registry, _entities, _contexts);
            Runtimes = new MobaSkillCastRuntimeService();
            var frame = new FixedFrameTime();
            Economy = new MobaSkillEconomyService(Runtimes, lookup, frame);
            Services = new Resolver(frame, Economy, Runtimes);
            Services.Add<IContinuousManager>(new DefaultContinuousManager());
            Services.Add(lookup);
            var combatRules = new MobaCombatRulesService(lookup);
            Services.Add(combatRules);
            var plans = new TriggerPlanJsonDatabase();
            var actions = new ActionRegistry();
            foreach (var effectId in new[] { 8441, 8442, 8443 })
            {
                var actionId = new ActionId(effectId + 100);
                var capturedId = effectId;
                actions.Register<NamedAction0<object, object, IWorldResolver>>(actionId,
                    (_, _, actionContext) =>
                    {
                        ExecutedEffects.Add(capturedId);
                        if (FailedEffectId == capturedId)
                            actionContext.Control.RejectAction("acceptance failure");
                    }, isDeterministic: true);
                var plan = new TriggerPlan<object>(0, 0, effectId,
                    actions: new[] { new ActionCallPlan(actionId) });
                plans.AddRecord(new TriggerPlanJsonDatabase.Record(effectId, string.Empty, 0,
                    default, in plan, TriggerPlanExecutableDsl.Action(actionId)));
            }
            var effects = new MobaEffectExecutionService();
            Inject(effects, "_services", Services);
            Inject(effects, "_planDb", plans);
            Inject(effects, "_planEventBus", new EventBus());
            Inject(effects, "_planFunctions", new FunctionRegistry());
            Inject(effects, "_planActions", actions);
            Inject(effects, "_planPayloads", new PayloadAccessorRegistry());
            Inject(effects, "_frameTime", frame);
            Inject(effects, "ExecutionContexts", _executionContexts);
            var invoker = new MobaEffectInvokerService();
            Inject(invoker, "_effects", effects);
            Services.Add(invoker);
            var configs = new MobaConfigDatabase();
            VolleyFlow = new SkillFlowDTO
            {
                Id = 8433,
                Phases = new[] { new SkillPhaseDTO
                {
                    Type = (int)SkillPhaseType.Timeline,
                    PhaseId = "volley",
                    Timeline = new SkillTimelinePhaseDTO
                    {
                        DurationMs = 300,
                        RecoveryStartMs = recoveryStartMs,
                        Events = new[]
                        {
                            Shot(0, 8441), Shot(100, 8442), Shot(200, 8443),
                        },
                    },
                } },
            };
            var load = configs.ReloadFromDtoArrays(new Dictionary<Type, Array>
            {
                [typeof(SkillDTO)] = new[]
                {
                    Skill(Instant, 8431), Skill(Charge, 8432), Skill(Volley, 8433),
                },
                [typeof(SkillFlowDTO)] = new[]
                {
                    new SkillFlowDTO { Id = 8431, Phases = new[]
                    {
                        new SkillPhaseDTO { Type = (int)SkillPhaseType.Economy, PhaseId = "reserve",
                            Economy = new SkillEconomyPhaseDTO { Operation = (int)SkillEconomyOperation.ReserveCast,
                                ResourceType = (int)ResourceType.Mana, ResourceAmount = 10,
                                UseResolvedResourceCost = false, RequireExplicitCommit = true,
                                StartSkillCooldown = true, SkillCooldownMs = 500, UseResolvedSkillCooldown = false } },
                        new SkillPhaseDTO { Type = (int)SkillPhaseType.CommitPoint, PhaseId = "hit",
                            CommitPoint = new SkillCommitPointPhaseDTO { RequireEconomyReservation = true } },
                    } },
                    new SkillFlowDTO { Id = 8432, Phases = new[]
                    {
                        new SkillPhaseDTO { Type = (int)SkillPhaseType.Delay, PhaseId = "charge",
                            Delay = new SkillDelayPhaseDTO { DelayMs = 1000 } },
                    } },
                    VolleyFlow,
                },
            }, strict: false);
            Assert.True(load.Succeeded, load.Error);
            Services.Add(configs);
            Services.Add(new SearchTargetService(_registry, configs, combatRules));
            Library = new TableDrivenMobaSkillPipelineLibrary(configs, invoker);
            Runner = new SkillPipelineRunner(ActorId);
        }

        public SkillFlowDTO VolleyFlow { get; }
        public List<int> ExecutedEffects { get; } = new();
        public int FailedEffectId { get; set; }
        public ActiveSkillRuntime SkillOne { get; }
        public ResourceState Mana { get; }
        public MobaSkillCastRuntimeService Runtimes { get; }
        public MobaExecutionContextRegistry ExecutionContexts => _executionContexts;
        public MobaSkillEconomyService Economy { get; }
        public Resolver Services { get; }
        public TableDrivenMobaSkillPipelineLibrary Library { get; }
        public SkillPipelineRunner Runner { get; }

        public void AddEnemy(int actorId)
        {
            var actor = _contexts.actor.CreateEntity();
            actor.AddActorId(actorId);
            actor.AddTeam(Team.Team2);
            actor.AddTransform(new Transform3(new Vec3(2f, 0f, 0f), Quat.Identity, Vec3.One));
            _registry.Register(actorId, actor);
        }

        public int FindEnemy()
        {
            var search = Services.Resolve<SearchTargetService>();
            var results = new List<int>();
            var position = Vec3.Zero;
            search.TrySearchActorIds(NormalAttackTargetQuery.Create(5f), ActorId,
                in position, 0, results);
            return results.Count > 0 ? results[0] : 0;
        }

        public SkillPipelineRunner.SkillPipelineStartResult Start(int id, int slot, SkillCastPolicy policy, int targetActorId = 0)
        {
            Assert.True(Library.TryGet(id, out var pre, out var prePhases, out var cast, out var phases));
            var aim = Vec3.Zero;
            var direction = Vec3.Forward;
            var root = _executionContexts.Create(new MobaExecutionContextCreateRequest(
                MobaExecutionKind.SkillCast, id, ActorId, targetActorId, frame: 10));
            var runtime = Runtimes.Create(new MobaSkillCastRuntimeCreateRequest(
                id, slot, 1, ++_sequence, ActorId, targetActorId, in aim, in direction, root.ContextId));
            var request = new SkillCastRequest(id, slot, ActorId, targetActorId, in aim, in direction,
                Services, null, null, null);
            var trigger = new SkillCastContext();
            trigger.Initialize(in request, skillLevel: 1);
            trigger.SourceContextId = root.ContextId;
            trigger.Sequence = _sequence;
            trigger.ResolvedConfiguration = new ResolvedSkillCastConfiguration(id, 1,
                ResourceType.Mana, 0, 0, false);
            trigger.RuntimeHandle = runtime.Handle;
            trigger.RuntimeId = runtime.RuntimeId;
            return Runner.TryStart(pre, prePhases, cast, phases, new object(), in request, trigger, in policy);
        }

        public void Restore(SkillRunnerRollbackEntry state)
        {
            Assert.True(Library.TryGet(state.SkillId, out var pre, out var prePhases,
                out var cast, out var castPhases));
            var aimPos = state.Context.AimPos;
            var aimDir = state.Context.AimDir;
            var request = new SkillCastRequest(state.SkillId, state.SkillSlot, ActorId,
                state.Context.TargetActorId, in aimPos, in aimDir, Services, null, null, null);
            var trigger = new SkillCastContext();
            trigger.Initialize(in request, state.SkillLevel, state.Sequence);
            trigger.SourceContextId = state.SourceContextId;
            trigger.RuntimeHandle = new MobaSkillCastRuntimeHandle(state.RuntimeId,
                state.RuntimeGeneration, state.RootContextId);
            trigger.RuntimeId = state.RuntimeId;
            trigger.ResolvedConfiguration = state.Context.ResolvedConfiguration;
            Runner.RestoreRollbackEntry(state, pre, prePhases, cast, castPhases,
                new object(), in request, trigger);
        }

        public SkillPipelineContext NewContext(int id, int slot, int baseSkillId = 0)
        {
            var aim = Vec3.Zero;
            var direction = Vec3.Forward;
            var root = _executionContexts.Create(new MobaExecutionContextCreateRequest(
                MobaExecutionKind.SkillCast, id, ActorId, 0, frame: 10));
            var runtime = Runtimes.Create(new MobaSkillCastRuntimeCreateRequest(
                id, slot, 1, ++_sequence, ActorId, 0, in aim, in direction, root.ContextId,
                baseSkillId: baseSkillId > 0 ? baseSkillId : id));
            var request = new SkillCastRequest(id, slot, ActorId, 0, in aim, in direction,
                Services, null, null, null);
            var trigger = new SkillCastContext();
            trigger.Initialize(in request, skillLevel: 1);
            trigger.SourceContextId = root.ContextId;
            trigger.RuntimeHandle = runtime.Handle;
            trigger.RuntimeId = runtime.RuntimeId;
            var context = new SkillPipelineContext();
            context.Initialize(new object(), in request, trigger);
            return context;
        }

        public void Dispose()
        {
            Runner.CancelAll();
            Economy.Dispose();
            Runtimes.Dispose();
            _executionContexts.Dispose();
            _index.Dispose();
            _contexts.actor.DestroyAllEntities();
            _registry.Dispose();
            _entities.Dispose();
        }

        private static ActiveSkillRuntime NewSkill(int id) => new() { SkillId = id, Level = 1, MaxCharges = 1, CurrentCharges = 1 };
        private static SkillDTO Skill(int id, int flowId) => new() { Id = id, Name = "acceptance_" + id,
            SkillType = (int)SkillType.Active, CastFlowId = flowId, Range = 5, Tags = Array.Empty<int>() };
        private static SkillTimelineEventDTO Shot(int atMs, int effectId) => new() { AtMs = atMs,
            EffectId = effectId, ExecuteMode = 0, AbortOnFailure = true };

        private static void Inject(object target, string name, object value)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var property = target.GetType().GetProperty(name, flags);
            if (property != null) { property.SetValue(target, value); return; }
            var field = target.GetType().GetField(name, flags);
            Assert.NotNull(field);
            field.SetValue(target, value);
        }
    }

    private sealed class FixedFrameTime : IFrameTime
    {
        public FrameIndex Frame => new(10);
        public float DeltaTime => 0f;
        public float Time => 1f;
        public float FrameToTime(FrameIndex frame) => 1f;
        public FrameIndex TimeToFrame(float time) => new(10);
    }

    private sealed class Resolver : IWorldResolver
    {
        private readonly Dictionary<Type, object> _items;
        public Resolver(IFrameTime frame, MobaSkillEconomyService economy, MobaSkillCastRuntimeService runtimes)
        {
            _items = new() { [typeof(IFrameTime)] = frame,
                [typeof(MobaSkillEconomyService)] = economy,
                [typeof(MobaSkillCastRuntimeService)] = runtimes };
        }
        public void Add<T>(T value) => _items[typeof(T)] = value;
        public object Resolve(Type type) => _items[type];
        public T Resolve<T>() => (T)Resolve(typeof(T));
        public bool TryResolve(Type type, out object instance) => _items.TryGetValue(type, out instance);
        public bool TryResolve<T>(out T instance)
        {
            var found = TryResolve(typeof(T), out var value);
            instance = found ? (T)value : default;
            return found;
        }
    }
}
