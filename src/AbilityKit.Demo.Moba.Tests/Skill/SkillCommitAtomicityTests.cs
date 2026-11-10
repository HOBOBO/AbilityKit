using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Pipeline;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Triggering.Runtime.Config.Plans;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class SkillCommitAtomicityTests
{
    private const int CommitTriggerId = 900101012;
    private const int CasterActorId = 17;
    private const int SkillId = 1001;
    private const int SkillSlot = 1;

    [Fact]
    public void Owner_bound_rule_plan_cannot_enter_direct_skill_pipeline_execution()
    {
        const int triggerId = 900101013;
        var plans = new TriggerPlanJsonDatabase();
        var actions = new ActionRegistry();
        var executed = 0;
        var actionId = new ActionId(920003);
        actions.Register<NamedAction0<object, object, IWorldResolver>>(
            actionId,
            (_, _, _) => executed++,
            isDeterministic: true);
        var plan = new TriggerPlan<object>(
            phase: 0,
            priority: 0,
            triggerId: triggerId,
            actions: new[] { new ActionCallPlan(actionId) });
        plans.AddRecord(new TriggerPlanJsonDatabase.Record(
            triggerId,
            eventName: string.Empty,
            eventId: 0,
            scope: TriggerPlanScope.OwnerBound,
            in plan,
            TriggerPlanExecutableDsl.Action(actionId)));

        var executor = new MobaTriggerPlanExecutor(
            services: null,
            planDb: plans,
            eventBus: new EventBus(),
            functions: new FunctionRegistry(),
            actions: actions,
            payloads: new PayloadAccessorRegistry());

        Assert.False(executor.ExecuteRulePlan(triggerId, new object()));
        Assert.Equal(0, executed);
    }

    [Fact]
    public void Effect_service_rejects_owner_bound_rule_plan_before_direct_execution()
    {
        const int triggerId = 900101014;
        var plans = new TriggerPlanJsonDatabase();
        var plan = new TriggerPlan<object>(phase: 0, priority: 0, triggerId: triggerId);
        plans.AddRecord(new TriggerPlanJsonDatabase.Record(
            triggerId,
            eventName: string.Empty,
            eventId: 0,
            scope: TriggerPlanScope.OwnerBound,
            in plan));

        var effects = new MobaEffectExecutionService();
        typeof(MobaEffectExecutionService)
            .GetField("_planDb", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(effects, plans);

        Assert.False(effects.ExecuteRulePlan(triggerId, new object()));
    }

    [Fact]
    public void Timeline_effect_entry_rejects_owner_bound_plan_with_explicit_scope_error()
    {
        const int effectId = 900101015;
        var plans = new TriggerPlanJsonDatabase();
        var plan = new TriggerPlan<object>(phase: 0, priority: 0, triggerId: effectId);
        plans.AddRecord(new TriggerPlanJsonDatabase.Record(
            effectId,
            eventName: string.Empty,
            eventId: 0,
            scope: TriggerPlanScope.OwnerBound,
            in plan));

        var frameTime = new FixedFrameTime();
        var request = new SkillCastRequest(
            skillId: 1,
            skillSlot: 1,
            casterActorId: 101,
            targetActorId: 202,
            Vec3.Zero,
            Vec3.Forward,
            worldServices: null,
            eventBus: new EventBus(),
            casterUnit: null,
            targetUnit: null);
        var cast = new SkillCastContext();
        cast.Initialize(in request, skillLevel: 1);
        var context = new SkillPipelineContext();
        context.Initialize(new object(), in request, cast);
        context.SetSourceContextId(1);

        var effects = new MobaEffectExecutionService();
        Inject(effects, "_planDb", plans);
        Inject(effects, "_frameTime", frameTime);

        var error = Assert.Throws<InvalidOperationException>(() => effects.ExecuteWithResult(effectId, context));
        Assert.Contains("Invalid trigger plan scope", error.Message);
        Assert.Contains(nameof(TriggerPlanScope.Global), error.Message);
    }

    [Fact]
    public void Owner_bound_execution_entry_rejects_global_plan_before_trigger_execution()
    {
        const int triggerId = 900101016;
        var plans = new TriggerPlanJsonDatabase();
        var plan = new TriggerPlan<object>(phase: 0, priority: 0, triggerId: triggerId);
        plans.AddRecord(new TriggerPlanJsonDatabase.Record(
            triggerId,
            eventName: string.Empty,
            eventId: 0,
            scope: TriggerPlanScope.Global,
            in plan));

        var effects = new MobaEffectExecutionService();
        Inject(effects, "_planDb", plans);

        var executed = false;
        var trigger = new DelegateTrigger<object, IWorldResolver>(
            predicate: (_, _) => true,
            actions: (_, _) => executed = true);
        var source = new MobaOwnerBoundTriggerExecutionSource(
            sourceActorId: CasterActorId,
            targetActorId: 0,
            sourceContextId: 1,
            rootContextId: 1,
            ownerContextId: 1,
            sourceConfigId: 0);
        var ctx = default(ExecCtx<IWorldResolver>);

        var result = effects.ExecuteOwnerBoundTriggerActions(
            triggerId,
            new object(),
            in ctx,
            in source,
            trigger);

        Assert.False(result);
        Assert.False(executed);
    }

    [Fact]
    public void Direct_event_entry_rejects_owner_bound_plan_before_trigger_execution()
    {
        const int triggerId = 900101017;
        var plans = new TriggerPlanJsonDatabase();
        var actions = new ActionRegistry();
        var executed = 0;
        var actionId = new ActionId(920004);
        actions.Register<NamedAction0<object, object, IWorldResolver>>(
            actionId,
            (_, _, _) => executed++,
            isDeterministic: true);
        var plan = new TriggerPlan<object>(
            phase: 0,
            priority: 0,
            triggerId: triggerId,
            actions: new[] { new ActionCallPlan(actionId) });
        plans.AddRecord(new TriggerPlanJsonDatabase.Record(
            triggerId,
            eventName: "damage.apply.after",
            eventId: 1,
            scope: TriggerPlanScope.OwnerBound,
            in plan,
            TriggerPlanExecutableDsl.Action(actionId)));

        var effects = new MobaEffectExecutionService();
        Inject(effects, "_planDb", plans);

        var request = MobaTriggerExecutionRequest<object>.Create(
            triggerId,
            new object(),
            "direct-event-test");

        Assert.False(effects.ExecuteTrigger(in request));
        Assert.Equal(0, executed);
    }

    [Fact]
    public void Failed_cooldown_commit_rolls_back_resource_and_existing_cooldown()
    {
        var contexts = new Contexts();
        var actor = contexts.actor.CreateEntity();
        var resource = new ResourceState { Current = 75, LastMax = 100 };
        var skill = new ActiveSkillRuntime
        {
            SkillId = SkillId,
            Level = 1,
            CooldownDurationMs = 321,
            CooldownEndTimeMs = 654L,
        };
        actor.AddActorId(CasterActorId);
        actor.AddResourceContainer(
            new ResourceContainer
            {
                Map = new Dictionary<ResourceType, ResourceState>
                {
                    [ResourceType.Mana] = resource,
                },
            },
            true);
        actor.AddSkillLoadout(0, new[] { skill },
            Array.Empty<PassiveSkillRuntime>());

        using var actorIndex = new ActorIdIndex(contexts);
        var eventBus = new EventBus();
        var registry = new MobaActorRegistry();
        registry.Register(CasterActorId, actor);
        var entities = new MobaEntityManager(eventBus);
        var actors = new MobaActorLookupService(
            actorIndex,
            registry,
            entities,
            contexts);
        var services = new TestWorldResolver(actors);

        var consumeActionId = new ActionId(920001);
        var rejectCooldownActionId = new ActionId(920002);
        var actions = new ActionRegistry();
        actions.Register<NamedAction0<object, object, IWorldResolver>>(
            consumeActionId,
            (triggerArgs, _, _) =>
            {
                var context = Assert.IsType<SkillPipelineContext>(triggerArgs);
                resource.Current -= context.ResolvedConfiguration.ResourceCost;
            },
            isDeterministic: true);
        actions.Register<NamedAction0<object, object, IWorldResolver>>(
            rejectCooldownActionId,
            (_, _, ctx) => ctx.Control.RejectAction("cooldown runtime unavailable"),
            isDeterministic: true);

        var plan = new TriggerPlan<object>(
            phase: 0,
            priority: 0,
            triggerId: CommitTriggerId,
            actions: new[]
            {
                new ActionCallPlan(consumeActionId),
                new ActionCallPlan(rejectCooldownActionId),
            });
        var root = TriggerPlanExecutableDsl.Sequence(
            TriggerPlanExecutableDsl.Action(consumeActionId),
            TriggerPlanExecutableDsl.Action(rejectCooldownActionId));
        var planDb = new TriggerPlanJsonDatabase();
        planDb.AddRecord(new TriggerPlanJsonDatabase.Record(
            CommitTriggerId,
            eventName: string.Empty,
            eventId: 0,
            scope: default,
            in plan,
            root));
        var executor = new MobaTriggerPlanExecutor(
            services,
            planDb,
            eventBus,
            new FunctionRegistry(),
            actions,
            new PayloadAccessorRegistry());
        var phase = new SkillRulePlanPhase(
            new AbilityPipelinePhaseId("commit"),
            new SkillRulePlanPhaseDTO
            {
                TriggerIds = new[] { CommitTriggerId },
                AbortOnFailure = true,
            },
            executor);
        var context = CreatePipelineContext(services, eventBus);

        phase.Execute(context);

        Assert.True(context.IsAborted);
        Assert.Equal($"Skill rule plan failed: {CommitTriggerId}", context.FailReason);
        Assert.Equal(AbilityKit.Deterministic.Fixed64.FromInt32(75), resource.Current);
        Assert.Equal(321, skill.CooldownDurationMs);
        Assert.Equal(654L, skill.CooldownEndTimeMs);
    }

    private static SkillPipelineContext CreatePipelineContext(
        IWorldResolver services,
        IEventBus eventBus)
    {
        var request = new SkillCastRequest(
            SkillId,
            SkillSlot,
            CasterActorId,
            targetActorId: 0,
            Vec3.Zero,
            Vec3.Forward,
            services,
            eventBus,
            casterUnit: null,
            targetUnit: null);
        var castContext = new SkillCastContext();
        castContext.Initialize(in request, skillLevel: 1);
        castContext.ResolvedConfiguration = new ResolvedSkillCastConfiguration(
            SkillId,
            skillLevel: 1,
            ResourceType.Mana,
            resourceCost: 20,
            cooldownMs: 1000,
            hasLevelConfiguration: true);
        var context = new SkillPipelineContext();
        context.Initialize(new object(), in request, castContext);
        return context;
    }

    private static void Inject(object target, string name, object value)
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var field = target.GetType().GetField(name, flags);
        Assert.NotNull(field);
        field.SetValue(target, value);
    }

    private sealed class FixedFrameTime : IFrameTime
    {
        public FrameIndex Frame => new(1);
        public float DeltaTime => 0f;
        public float Time => 0f;
        public float FrameToTime(FrameIndex frame) => frame.Value;
        public FrameIndex TimeToFrame(float time) => new((int)time);
    }

    private sealed class TestWorldResolver : IWorldResolver
    {
        private readonly Dictionary<Type, object> _services = new();

        public TestWorldResolver(MobaActorLookupService actors)
        {
            _services[typeof(MobaActorLookupService)] = actors;
        }

        public object Resolve(Type serviceType)
        {
            return _services[serviceType];
        }

        public T Resolve<T>()
        {
            return (T)Resolve(typeof(T));
        }

        public bool TryResolve(Type serviceType, out object instance)
        {
            return _services.TryGetValue(serviceType, out instance);
        }

        public bool TryResolve<T>(out T instance)
        {
            if (_services.TryGetValue(typeof(T), out var value))
            {
                instance = (T)value;
                return true;
            }

            instance = default;
            return false;
        }
    }
}
