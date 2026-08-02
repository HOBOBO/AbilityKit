using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Pipeline;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class SkillCommitAtomicityTests
{
    private const int CommitTriggerId = 900101012;
    private const int CasterActorId = 17;
    private const int SkillId = 1001;
    private const int SkillSlot = 1;

    [Fact]
    public void Failed_cooldown_commit_rolls_back_resource_and_existing_cooldown()
    {
        var contexts = new Contexts();
        var actor = contexts.actor.CreateEntity();
        var resource = new ResourceState { Current = 75f, LastMax = 100f };
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
        actor.AddSkillLoadout(
            new[] { skill },
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
        Assert.Equal(75f, resource.Current);
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
