using System.Reflection;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Combat.Projectile;
using AbilityKit.Context;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Demo.Moba.Services.Buffs.Triggering;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Services.Triggering.PlanActions;
using AbilityKit.Trace;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Trace;

public sealed class MobaExecutionContextRegistryTests
{
    [Fact]
    public void Context_owns_normalized_lineage_and_observer_failures_do_not_change_state()
    {
        using var contexts = new MobaExecutionContextRegistry();
        var observerFailures = 0;
        contexts.ObserverException = (_, _) => observerFailures++;
        using var subscription = contexts.Observe(
            (in ContextLifecycleEvent<MobaExecutionContextNode> _) => throw new InvalidOperationException("observer"),
            replayExisting: false);

        var root = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SkillCast, 1001, 1, 2, frame: 10));
        var child = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.EffectExecution, 2001, 1, 2,
            parentContextId: root.ContextId,
            frame: 11));

        Assert.Equal(root.ContextId, root.RootContextId);
        Assert.Equal(root.ContextId, root.OwnerContextId);
        Assert.Equal(root.ContextId, child.ParentContextId);
        Assert.Equal(root.ContextId, child.RootContextId);
        Assert.Equal(root.ContextId, child.OwnerContextId);
        Assert.True(contexts.TryGet(child.ContextId, out var stored));
        Assert.False(stored.IsEnded);
        Assert.Equal(2, observerFailures);

        Assert.True(contexts.End(child.ContextId, (int)MobaExecutionEndReason.Completed, 12));
        Assert.True(contexts.TryGet(child.ContextId, out stored));
        Assert.True(stored.IsEnded);
        Assert.Equal(12, stored.EndedFrame);
        Assert.Equal(3, observerFailures);
    }

    [Fact]
    public void Context_and_observation_span_allocators_use_independent_id_partitions()
    {
        using var contexts = new MobaExecutionContextRegistry();
        using var trace = new MobaTraceRegistry();
        trace.AttachExecutionContexts(contexts);
        var root = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SkillCast, 1001, 1, 2));
        Assert.True(trace.Contains(root.ContextId));

        var actionSpan = trace.CreateObservationChild(
            root.ContextId, MobaExecutionKind.EffectAction, 3001, 1, 2);
        var effect = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.EffectExecution, 2001, 1, 2,
            parentContextId: root.ContextId));

        Assert.False(MobaExecutionContextRegistry.IsExecutionContextId(actionSpan));
        Assert.True(MobaExecutionContextRegistry.IsExecutionContextId(effect.ContextId));
        Assert.NotEqual(actionSpan, effect.ContextId);
        Assert.True(trace.Contains(root.ContextId));
        Assert.True(trace.Contains(actionSpan));
        Assert.True(trace.Contains(effect.ContextId));
        Assert.Equal((int)MobaExecutionKind.EffectExecution, trace.TryGetSnapshot(effect.ContextId).Kind);
        Assert.True(contexts.TryGet(effect.ContextId, out var stored));
        Assert.Equal(MobaExecutionKind.EffectExecution, stored.Kind);
    }

    [Fact]
    public void Prediction_retraction_removes_nodes_without_reusing_context_ids()
    {
        using var contexts = new MobaExecutionContextRegistry();
        contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SkillCast, 1001, 1, 2));
        var boundary = contexts.NextContextId;
        var predicted = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.EffectExecution, 2001, 1, 2));

        Assert.Equal(1, contexts.RetractPrediction(boundary));
        Assert.False(contexts.TryGet(predicted.ContextId, out _));
        var replay = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.EffectExecution, 2001, 1, 2));
        Assert.True(replay.ContextId > predicted.ContextId);
    }

    [Fact]
    public void Lifecycle_restore_is_atomic_and_publishes_one_reconciliation_event()
    {
        using var contexts = new MobaExecutionContextRegistry();
        var root = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SkillCast, 1001, 1, 2, frame: 10));
        var child = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.EffectExecution, 2001, 1, 2,
            parentContextId: root.ContextId,
            frame: 11));
        contexts.End(root.ContextId, (int)MobaExecutionEndReason.Completed, 12);
        contexts.End(child.ContextId, (int)MobaExecutionEndReason.Completed, 12);
        var observerEvents = new List<ContextLifecycleEventKind>();
        using var subscription = contexts.Observe(
            (in ContextLifecycleEvent<MobaExecutionContextNode> evt) => observerEvents.Add(evt.Kind),
            replayExisting: false);

        Assert.Throws<InvalidOperationException>(() =>
            contexts.RestoreLifecycle(new[] { root, root }));
        Assert.True(contexts.TryGet(root.ContextId, out var currentRoot));
        Assert.True(currentRoot.IsEnded);
        Assert.Empty(observerEvents);

        contexts.RestoreLifecycle(new[] { root, child });

        Assert.True(contexts.TryGet(root.ContextId, out currentRoot));
        Assert.True(contexts.TryGet(child.ContextId, out var currentChild));
        Assert.False(currentRoot.IsEnded);
        Assert.False(currentChild.IsEnded);
        Assert.Equal(new[] { ContextLifecycleEventKind.Reconciled }, observerEvents);
    }

    [Fact]
    public void Trace_replays_existing_parent_history_when_attached_late()
    {
        using var contexts = new MobaExecutionContextRegistry();
        using var trace = new MobaTraceRegistry();
        var root = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SkillCast, 1001, 1, 2));
        trace.OnInit(new ContextResolver(contexts));
        var effect = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.EffectExecution, 2001, 1, 2,
            parentContextId: root.ContextId));

        var recorded = trace.TryGetSnapshot(effect.ContextId);
        Assert.True(trace.Contains(root.ContextId));
        Assert.Equal(root.ContextId, recorded.RootId);
        Assert.Equal(root.ContextId, recorded.ParentId);
        Assert.Equal(root.ContextId, effect.RootContextId);
        trace.OnDeinit(null!);
    }

    [Fact]
    public void Buff_apply_tick_and_remove_contexts_exist_without_trace()
    {
        using var contexts = new MobaExecutionContextRegistry();
        var skill = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SkillCast, 1001, 1, 2, frame: 10));
        var origin = new BuffOriginContext(
            skill.ContextId,
            1,
            2,
            MobaExecutionKind.EffectAction,
            3001,
            skill.ContextId);
        var runtime = new BuffRuntime { BuffId = 4001, SourceId = 1, StackCount = 1 };
        var buffContexts = new BuffContextRegistry(contexts, null, null);

        buffContexts.EnsureBuffContext(runtime, 4001, 1, 2, in origin);

        var buffContextId = runtime.SourceContextId;
        Assert.True(contexts.TryGet(buffContextId, out var apply));
        Assert.Equal(MobaExecutionKind.BuffApply, apply.Kind);
        Assert.Equal(skill.ContextId, apply.ParentContextId);
        Assert.Equal(skill.ContextId, apply.RootContextId);
        Assert.Equal(MobaExecutionKind.EffectAction, apply.OriginKind);

        var stages = new BuffStageEffectExecutor(
            new MobaTriggerExecutionGateway(),
            contexts);
        var tickContextId = contexts.NextContextId;
        stages.Execute(
            new[] { 0 },
            4001,
            1,
            2,
            buffContextId,
            MobaBuffTriggering.Stages.Interval,
            runtime);

        Assert.True(contexts.TryGet(tickContextId, out var tick));
        Assert.Equal(MobaExecutionKind.BuffTick, tick.Kind);
        Assert.Equal(buffContextId, tick.ParentContextId);
        Assert.True(tick.IsEnded);
        Assert.Equal((int)MobaExecutionEndReason.Completed, tick.EndReason);

        buffContexts.EndByRuntimeNoClear(runtime, MobaExecutionEndReason.Expired);
        var removeContextId = contexts.NextContextId;
        stages.Execute(
            new[] { 0 },
            4001,
            1,
            2,
            buffContextId,
            MobaBuffTriggering.Stages.Remove,
            runtime,
            MobaExecutionEndReason.Expired);

        Assert.True(contexts.TryGet(buffContextId, out apply));
        Assert.True(apply.IsEnded);
        Assert.True(contexts.TryGet(removeContextId, out var remove));
        Assert.Equal(MobaExecutionKind.BuffRemove, remove.Kind);
        Assert.Equal(buffContextId, remove.ParentContextId);
        Assert.True(remove.IsEnded);
        Assert.Equal((int)MobaExecutionEndReason.Expired, remove.EndReason);
    }

    [Fact]
    public void Trace_observes_context_lifecycle_without_owning_it()
    {
        using var contexts = new MobaExecutionContextRegistry();
        using var trace = new MobaTraceRegistry();
        trace.OnInit(new ContextResolver(contexts));
        var observerFailures = 0;
        var successfulObserverCalls = 0;
        trace.ObserverException = (_, _) => observerFailures++;
        trace.RegistryEvent += evt =>
        {
            if (evt.Kind == TraceRegistryEventKind.RootCreated)
                throw new InvalidOperationException("trace observer");
        };
        trace.RegistryEvent += evt =>
        {
            if (evt.Kind == TraceRegistryEventKind.RootCreated)
                successfulObserverCalls++;
        };

        var root = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SkillCast, 1001, 11, 22, frame: 7));

        Assert.True(contexts.TryGet(root.ContextId, out var live));
        Assert.False(live.IsEnded);
        Assert.True(trace.Contains(root.ContextId));
        var recorded = trace.TryGetSnapshot(root.ContextId);
        Assert.Equal((int)MobaExecutionKind.SkillCast, recorded.Kind);
        Assert.Equal(7, recorded.CreatedFrame);
        Assert.Equal(1001, recorded.Metadata.ConfigId);
        Assert.Equal("Actor:11", recorded.Metadata.OriginSource);
        Assert.Equal(1, observerFailures);
        Assert.Equal(1, successfulObserverCalls);

        Assert.True(contexts.End(root.ContextId, (int)MobaExecutionEndReason.Completed, 8));
        var endedTrace = trace.TryGetSnapshot(root.ContextId);
        Assert.True(endedTrace.IsEnded);
        Assert.Equal(8, endedTrace.EndedFrame);

        trace.OnDeinit(null!);
        var unobserved = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.EffectExecution, 2001, 11, 22));
        Assert.False(trace.Contains(unobserved.ContextId));
    }

    [Fact]
    public void Optional_trace_does_not_change_context_identity_lineage_or_lifecycle()
    {
        var withoutTrace = RunContextScenario(enableTrace: false);
        var withTrace = RunContextScenario(enableTrace: true);

        Assert.Equal(withoutTrace.Root.ContextId, withTrace.Root.ContextId);
        Assert.Equal(withoutTrace.Child.ContextId, withTrace.Child.ContextId);
        Assert.Equal(withoutTrace.Child.ParentContextId, withTrace.Child.ParentContextId);
        Assert.Equal(withoutTrace.Child.RootContextId, withTrace.Child.RootContextId);
        Assert.Equal(withoutTrace.Child.OwnerContextId, withTrace.Child.OwnerContextId);
        Assert.Equal(withoutTrace.Child.IsEnded, withTrace.Child.IsEnded);
        Assert.Equal(withoutTrace.Child.EndReason, withTrace.Child.EndReason);
        Assert.Equal(withoutTrace.NextContextId, withTrace.NextContextId);
    }

    [Fact]
    public void Damage_facts_are_frozen_in_context_and_reflection_cannot_reflect_again()
    {
        var attack = new AttackInfo
        {
            AttackerActorId = 1,
            TargetActorId = 2,
            CombatFlags = MobaCombatExecutionFlags.ReflectedDamage
                          | MobaCombatExecutionFlags.LifestealSuppressed,
        };
        var lineage = new MobaEffectLineageInput(
            EffectContextKind.Trigger,
            MobaExecutionKind.DamageAttack,
            1,
            2,
            100,
            100,
            100,
            3001);
        var snapshot = new MobaTriggerExecutionSnapshot(
            EffectContextKind.Trigger,
            1,
            2,
            100,
            100,
            100,
            0,
            3001,
            10,
            default);

        var execution = MobaCombatExecutionContext.Create(
            attack,
            in lineage,
            in snapshot,
            10);
        var condition = MobaTriggerConditionContext.Create(
            in execution,
            null,
            10);
        var calc = new AttackCalcInfo(attack);

        attack.CombatFlags = MobaCombatExecutionFlags.None;

        Assert.True(execution.CombatFacts.IsReflectedDamage);
        Assert.True(execution.CombatFacts.IsSecondaryDamage);
        Assert.False(execution.CombatFacts.CanTriggerReflection);
        Assert.False(execution.CombatFacts.CanTriggerLifesteal);
        Assert.False(MobaDamageExecutionConditions.CanTriggerReflection(in execution));
        Assert.Equal(execution.CombatFacts, condition.CombatFacts);
        Assert.True(((object)condition).TryResolveCombatExecutionContext(out var conditionExecution));
        Assert.Equal(execution.CombatFacts, conditionExecution.CombatFacts);
        Assert.True(((IMobaCombatExecutionFactsProvider)calc)
            .TryGetCombatExecutionFacts(out var calcFacts));
        Assert.Equal(execution.CombatFacts, calcFacts);
    }

    [Fact]
    public void Execution_context_lifecycle_preserves_damage_facts_for_optional_observers()
    {
        using var contexts = new MobaExecutionContextRegistry();
        var facts = new MobaCombatExecutionFacts(
            MobaCombatExecutionFlags.PeriodicDamage |
            MobaCombatExecutionFlags.ReflectionSuppressed);
        MobaExecutionContextNode observed = default;
        using var subscription = contexts.Observe((in ContextLifecycleEvent<MobaExecutionContextNode> evt) =>
        {
            if (evt.Kind == ContextLifecycleEventKind.Created)
                observed = evt.Node;
        }, replayExisting: false);

        var damage = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.DamageAttack,
            3001,
            1,
            2,
            combatFlags: facts.Flags));

        Assert.Equal(facts, damage.CombatFacts);
        Assert.Equal(facts, observed.CombatFacts);
        Assert.False(observed.CombatFacts.CanTriggerReflection);
    }

    [Fact]
    public void Projectile_launch_context_exists_without_trace()
    {
        using var contexts = new MobaExecutionContextRegistry();
        var observerFailures = 0;
        contexts.ObserverException = (_, _) => observerFailures++;
        using var subscription = contexts.Observe(
            (in ContextLifecycleEvent<MobaExecutionContextNode> _) =>
                throw new InvalidOperationException("optional observer"),
            replayExisting: false);
        var service = new MobaProjectileService();
        SetField(service, "_executionContexts", contexts);
        SetField(service, "_frameTime", new TestFrameTime(17));
        var method = typeof(MobaProjectileService).GetMethod(
            "CreateLaunchSource",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        object[] args = { 11, 22, 3001, default(ProjectileSourceContext), 0L };

        var source = (ProjectileSourceContext)method.Invoke(service, args)!;
        var contextId = (long)args[4];

        Assert.True(source.IsValid);
        Assert.Equal(contextId, source.SourceContextId);
        Assert.True(contexts.TryGet(contextId, out var launch));
        Assert.Equal(MobaExecutionKind.ProjectileLaunch, launch.Kind);
        Assert.Equal(17, launch.CreatedFrame);
        Assert.False(launch.IsEnded);
        Assert.Equal(1, observerFailures);
    }

    [Fact]
    public void Summon_spawn_context_exists_without_trace_and_observer_failure_is_isolated()
    {
        using var contexts = new MobaExecutionContextRegistry();
        var observerFailures = 0;
        contexts.ObserverException = (_, _) => observerFailures++;
        using var subscription = contexts.Observe(
            (in ContextLifecycleEvent<MobaExecutionContextNode> _) =>
                throw new InvalidOperationException("optional observer"),
            replayExisting: false);
        var service = new MobaSummonService();
        SetField(service, "_executionContexts", contexts);
        SetField(service, "_frameTime", new TestFrameTime(23));
        var method = typeof(MobaSummonService).GetMethod(
            "CreateSpawnSourceContext",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        object[] args = { 11, 22, 5001, default(SummonSourceContext), 0L };

        var source = (SummonSourceContext)method.Invoke(service, args)!;
        var contextId = (long)args[4];

        Assert.True(source.IsValid);
        Assert.Equal(contextId, source.SourceContextId);
        Assert.True(contexts.TryGet(contextId, out var spawn));
        Assert.Equal(MobaExecutionKind.SummonSpawn, spawn.Kind);
        Assert.Equal(23, spawn.CreatedFrame);
        Assert.False(spawn.IsEnded);
        Assert.Equal(1, observerFailures);
    }

    [Fact]
    public void Area_runtime_ends_replaced_and_rolled_back_contexts_without_trace()
    {
        using var contexts = new MobaExecutionContextRegistry();
        var runtime = new MobaAreaRuntimeService();
        SetField(runtime, "_executionContexts", contexts);
        SetField(runtime, "_frameTime", new TestFrameTime(31));
        var resolver = new ContextResolver(contexts, runtime);

        Assert.True(SpawnAreaPlanActionModule.TryResolveRuntimeDependencies(
            resolver,
            out var resolvedRuntime,
            out var resolvedContexts,
            out var failure), failure);
        Assert.Same(runtime, resolvedRuntime);
        Assert.Same(contexts, resolvedContexts);

        var first = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.AreaSpawn, 4001, 11, 22, frame: 20));
        runtime.RegisterSpawn(
            new AreaId(7), 4001, 11, Vec3.Zero, 3f, 1, 4, 20, 0,
            first.ContextId, first.RootContextId, first.OwnerContextId);

        var replacement = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.AreaSpawn, 4001, 11, 22, frame: 21));
        runtime.RegisterSpawn(
            new AreaId(7), 4001, 11, Vec3.Zero, 3f, 1, 4, 21, 0,
            replacement.ContextId, replacement.RootContextId, replacement.OwnerContextId);

        Assert.True(contexts.TryGet(first.ContextId, out var replaced));
        Assert.True(replaced.IsEnded);
        Assert.Equal((int)MobaExecutionEndReason.Replaced, replaced.EndReason);
        Assert.True(runtime.RollbackSpawn(new AreaId(7), replacement.ContextId));
        Assert.True(contexts.TryGet(replacement.ContextId, out var rolledBack));
        Assert.True(rolledBack.IsEnded);
        Assert.Equal((int)MobaExecutionEndReason.Failed, rolledBack.EndReason);
    }

    [Fact]
    public void Late_trace_replays_existing_contexts_and_observes_future_events()
    {
        using var contexts = new MobaExecutionContextRegistry();
        var projectile = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.ProjectileLaunch, 3001, 11, 22, frame: 10));
        var area = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.AreaSpawn, 4001, 11, 22, frame: 10));
        var summon = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SummonSpawn, 5001, 11, 22, frame: 10));
        var passive = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.PassiveActivation, 6001, 11, 11, frame: 10));
        using var trace = new MobaTraceRegistry();
        trace.OnInit(new ContextResolver(contexts));

        var hit = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.ProjectileHit, 3001, 11, 22,
            parentContextId: projectile.ContextId,
            frame: 11));
        var enter = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.AreaEnter, 4001, 11, 22,
            parentContextId: area.ContextId,
            frame: 11));
        var phase = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SkillPhase, 1001, 11, 22,
            parentContextId: projectile.ContextId,
            frame: 11,
            castFlowId: 7001));
        contexts.End(hit.ContextId, (int)MobaExecutionEndReason.Completed, 11);
        contexts.End(enter.ContextId, (int)MobaExecutionEndReason.Completed, 11);
        contexts.End(phase.ContextId, (int)MobaExecutionEndReason.Completed, 11);

        Assert.Equal((int)MobaExecutionKind.ProjectileLaunch, trace.TryGetSnapshot(projectile.ContextId).Kind);
        Assert.Equal((int)MobaExecutionKind.AreaSpawn, trace.TryGetSnapshot(area.ContextId).Kind);
        Assert.Equal((int)MobaExecutionKind.SummonSpawn, trace.TryGetSnapshot(summon.ContextId).Kind);
        Assert.Equal((int)MobaExecutionKind.SkillEffect, trace.TryGetSnapshot(passive.ContextId).Kind);
        Assert.Equal((int)MobaExecutionKind.ProjectileHit, trace.TryGetSnapshot(hit.ContextId).Kind);
        Assert.Equal((int)MobaExecutionKind.AreaEnter, trace.TryGetSnapshot(enter.ContextId).Kind);
        Assert.Equal((int)MobaExecutionKind.SkillPhase, trace.TryGetSnapshot(phase.ContextId).Kind);
        Assert.True(trace.TryGetSnapshot(hit.ContextId).IsEnded);
        Assert.True(trace.TryGetSnapshot(enter.ContextId).IsEnded);
        Assert.True(trace.TryGetSnapshot(phase.ContextId).IsEnded);
        trace.OnDeinit(null!);
    }

    private static (MobaExecutionContextNode Root, MobaExecutionContextNode Child, long NextContextId)
        RunContextScenario(bool enableTrace)
    {
        using var contexts = new MobaExecutionContextRegistry();
        using var trace = enableTrace ? new MobaTraceRegistry() : null;
        if (trace != null) trace.OnInit(new ContextResolver(contexts));

        var root = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SkillCast, 1001, 1, 2, frame: 10));
        var child = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.EffectExecution, 2001, 1, 2,
            parentContextId: root.ContextId,
            frame: 11));
        contexts.End(child.ContextId, (int)MobaExecutionEndReason.Completed, 12);
        contexts.TryGet(child.ContextId, out child);

        trace?.OnDeinit(null!);
        return (root, child, contexts.NextContextId);
    }

    private sealed class ContextResolver : IWorldResolver
    {
        private readonly MobaExecutionContextRegistry _contexts;

        private readonly MobaAreaRuntimeService? _areaRuntime;

        public ContextResolver(
            MobaExecutionContextRegistry contexts,
            MobaAreaRuntimeService? areaRuntime = null)
        {
            _contexts = contexts;
            _areaRuntime = areaRuntime;
        }

        public object? Resolve(Type serviceType) =>
            serviceType == typeof(MobaExecutionContextRegistry) ? _contexts :
            serviceType == typeof(MobaAreaRuntimeService) ? _areaRuntime : null;
        public T? Resolve<T>() => TryResolve<T>(out var instance) ? instance : default;
        public bool TryResolve(Type serviceType, out object? instance)
        {
            instance = Resolve(serviceType);
            return instance != null;
        }
        public bool TryResolve<T>(out T? instance)
        {
            var resolved = Resolve(typeof(T));
            if (resolved is T typed)
            {
                instance = typed;
                return true;
            }

            instance = default;
            return false;
        }
    }

    private sealed class TestFrameTime : IFrameTime
    {
        public TestFrameTime(int frame) => Frame = new FrameIndex(frame);
        public FrameIndex Frame { get; }
        public float DeltaTime => 1f / 30f;
        public float Time => Frame.Value * DeltaTime;
        public float FrameToTime(FrameIndex frame) => frame.Value * DeltaTime;
        public FrameIndex TimeToFrame(float time) => new((int)MathF.Round(time / DeltaTime));
    }

    private static void SetField(object target, string fieldName, object value)
    {
        target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(target, value);
    }
}
