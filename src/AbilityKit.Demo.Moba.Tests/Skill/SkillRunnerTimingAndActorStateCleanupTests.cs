using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.Impl.Pipeline.Skill;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Modifiers;
using AbilityKit.Pipeline;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class SkillRunnerTimingAndActorStateCleanupTests
{
    private const int ActorId = 41;
    private const int OtherActorId = 42;

    [Fact]
    public void Runner_registry_steps_pipeline_with_authoritative_world_delta_time()
    {
        var clock = new TestWorldClock();
        var frameTime = new TestFrameTime();
        var services = new TestWorldResolver(frameTime);
        var registry = new SkillRunnerRegistry(clock, null, null, null);
        var runner = registry.GetOrCreate(ActorId);
        var config = new SkillPipelineConfig(1, "timing_test");
        var phases = new IAbilityPipelinePhase<SkillPipelineContext>[]
        {
            new AbilityDelayPhase<SkillPipelineContext>(new AbilityPipelinePhaseId("delay"), 1f),
        };
        var request = new SkillCastRequest(
            skillId: 1001,
            skillSlot: 1,
            casterActorId: ActorId,
            targetActorId: 0,
            Vec3.Zero,
            Vec3.Forward,
            services,
            eventBus: null,
            casterUnit: null,
            targetUnit: null);
        var castContext = new SkillCastContext();
        castContext.Initialize(in request, skillLevel: 1);
        castContext.SourceContextId = 7001L;

        Assert.True(runner.Start(
            preCastConfig: null,
            preCastPhases: null,
            castConfig: config,
            castPhases: phases,
            abilityInstance: new object(),
            in request,
            castContext,
            out var failReason), failReason);

        clock.Tick(0.125f);
        registry.Step(ActorId);

        Assert.True(registry.TryGetLatestRunningBySlot(ActorId, 1, out var snapshot));
        Assert.Equal(125, snapshot.ElapsedMs);
        registry.Dispose();
    }

    [Fact]
    public void Loadout_removal_is_actor_scoped_and_idempotent()
    {
        var service = new MobaSkillLoadoutService(actors: null);
        service.SetLoadout(ActorId, new[] { 1001 });
        service.SetLoadout(OtherActorId, new[] { 2001 });

        Assert.True(service.RemoveActor(ActorId));
        Assert.False(service.RemoveActor(ActorId));
        Assert.False(service.TryGetSkillId(ActorId, 1, out _));
        Assert.True(service.TryGetSkillId(OtherActorId, 1, out var skillId));
        Assert.Equal(2001, skillId);
    }

    [Fact]
    public void Combat_activity_removal_is_actor_scoped_and_idempotent()
    {
        var clock = new TestWorldClock();
        clock.Tick(2f);
        var service = new MobaCombatActivityService(clock);
        service.RecordCombat(ActorId);
        service.RecordCombat(OtherActorId);

        Assert.True(service.RemoveActor(ActorId));
        Assert.False(service.RemoveActor(ActorId));
        Assert.False(service.TryGetLastCombatTime(ActorId, out _));
        Assert.True(service.TryGetLastCombatTime(OtherActorId, out var time));
        Assert.Equal(2f, time);
    }

    [Fact]
    public void Shield_removal_is_actor_scoped_and_idempotent()
    {
        var service = new MobaShieldService();
        service.AddShield(ActorId, CreateShield(ActorId));
        service.AddShield(OtherActorId, CreateShield(OtherActorId));

        Assert.True(service.RemoveActor(ActorId));
        Assert.False(service.RemoveActor(ActorId));
        Assert.False(service.TryGetContainer(ActorId, out _));
        Assert.True(service.TryGetContainer(OtherActorId, out _));
    }

    [Fact]
    public void Skill_modifier_clear_is_actor_scoped_and_idempotent()
    {
        var service = new MobaSkillParamModifierService();
        var key = MobaSkillParamModifierKeys.Skill.SkillId;
        service.AddFixed(ActorId, key, ModifierOp.Add, 5f);
        service.AddFixed(OtherActorId, key, ModifierOp.Add, 7f);

        service.ClearActor(ActorId);
        service.ClearActor(ActorId);

        Assert.Equal(10, service.ResolveInt(ActorId, key, 10));
        Assert.Equal(17, service.ResolveInt(OtherActorId, key, 10));
    }

    private static ShieldLayer CreateShield(int actorId)
    {
        return new ShieldLayer
        {
            ShieldId = 1,
            SourceActorId = actorId,
            TargetActorId = actorId,
            CurrentValue = 50f,
            MaxValue = 50f,
            InitialValue = 50f,
            AbsorbRatio = 1f,
            StackingPolicy = ShieldStackingPolicy.Independent,
        };
    }

    private sealed class TestWorldClock : IWorldClock
    {
        public float DeltaTime { get; private set; }
        public float Time { get; private set; }

        public void Tick(float deltaTime)
        {
            DeltaTime = deltaTime;
            Time += deltaTime;
        }
    }

    private sealed class TestFrameTime : IFrameTime
    {
        public FrameIndex Frame => new(10);
        public float DeltaTime => 0f;
        public float Time => 0f;
        public float FrameToTime(FrameIndex frame) => 0f;
        public FrameIndex TimeToFrame(float time) => new(10);
    }

    private sealed class TestWorldResolver : IWorldResolver
    {
        private readonly Dictionary<Type, object> _services;

        public TestWorldResolver(IFrameTime frameTime)
        {
            _services = new Dictionary<Type, object>
            {
                [typeof(IFrameTime)] = frameTime,
            };
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
