using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Continuous;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Passive;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Trace;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Passive;

public sealed class MobaPassiveSkillLifecycleServiceTests
{
    [Fact]
    public void Sync_actor_passives_keeps_multiple_passives_independent_and_dedupes_duplicate_ids()
    {
        using var trace = new MobaTraceRegistry();
        var service = new MobaPassiveSkillLifecycleService(CreateConfigDatabase(), trace);
        var actor = CreateActor(
            new PassiveSkillRuntime { PassiveSkillId = 101, Level = 1 },
            new PassiveSkillRuntime { PassiveSkillId = 102, Level = 1 },
            new PassiveSkillRuntime { PassiveSkillId = 101, Level = 2 });

        service.SyncActorPassives(actor, frame: 1);

        Assert.True(actor.hasPassiveSkillTriggerListeners);
        Assert.Equal(new[] { 101, 102 }, actor.passiveSkillTriggerListeners.Active.Select(x => x.PassiveSkillId).OrderBy(x => x));
        Assert.True(actor.hasOngoingTriggerPlans);
        Assert.Equal(2, actor.ongoingTriggerPlans.Active.Count);
        Assert.All(actor.ongoingTriggerPlans.Active, entry => Assert.True(service.IsPassiveOwnerKey(entry.OwnerKey)));
        Assert.Contains(actor.ongoingTriggerPlans.Active, entry => entry.TriggerIds.SequenceEqual(new[] { 1001 }));
        Assert.Contains(actor.ongoingTriggerPlans.Active, entry => entry.TriggerIds.SequenceEqual(new[] { 1002 }));
    }

    [Fact]
    public void Repeated_sync_keeps_plan_list_entries_trigger_arrays_and_revision_stable()
    {
        using var trace = new MobaTraceRegistry();
        var service = new MobaPassiveSkillLifecycleService(CreateConfigDatabase(), trace);
        var actor = CreateActor(
            new PassiveSkillRuntime { PassiveSkillId = 101, Level = 1 },
            new PassiveSkillRuntime { PassiveSkillId = 102, Level = 1 });

        service.SyncActorPassives(actor, frame: 1);
        var plans = actor.ongoingTriggerPlans.Active;
        var revision = actor.ongoingTriggerPlans.Revision;
        var entries = plans.ToArray();
        var triggerArrays = entries.Select(entry => entry.TriggerIds).ToArray();
        var ownerKeys = entries.Select(entry => entry.OwnerKey).ToArray();

        service.SyncActorPassives(actor, frame: 2);

        Assert.Same(plans, actor.ongoingTriggerPlans.Active);
        Assert.Equal(revision, actor.ongoingTriggerPlans.Revision);
        Assert.Equal(ownerKeys, actor.ongoingTriggerPlans.Active.Select(entry => entry.OwnerKey));
        for (int i = 0; i < entries.Length; i++)
        {
            Assert.Same(entries[i], actor.ongoingTriggerPlans.Active[i]);
            Assert.Same(triggerArrays[i], actor.ongoingTriggerPlans.Active[i].TriggerIds);
        }
    }

    [Fact]
    public void Complete_owner_bound_trigger_starts_cooldown_and_blocks_until_time_passes()
    {
        var time = new TestFrameTime { TimeSeconds = 1.5f };
        using var trace = new MobaTraceRegistry();
        var service = new MobaPassiveSkillLifecycleService(CreateConfigDatabase(), trace, frameTime: time);
        var runtime = new PassiveSkillRuntime { PassiveSkillId = 101, Level = 1 };
        var actor = CreateActor(runtime);

        service.SyncActorPassives(actor, frame: 1);
        var entry = Assert.Single(actor.ongoingTriggerPlans.Active);

        Assert.True(service.CanExecuteOwnerBoundTrigger(entry.OwnerKey, 1001));

        service.CompleteOwnerBoundTrigger(entry.OwnerKey, 1001);

        Assert.Equal(2500L, runtime.CooldownEndTimeMs);
        Assert.False(service.CanExecuteOwnerBoundTrigger(entry.OwnerKey, 1001));

        time.TimeSeconds = 2.5f;

        Assert.True(service.CanExecuteOwnerBoundTrigger(entry.OwnerKey, 1001));
    }

    [Fact]
    public void Removing_one_passive_keeps_other_passive_owner_binding_active()
    {
        using var trace = new MobaTraceRegistry();
        var service = new MobaPassiveSkillLifecycleService(CreateConfigDatabase(), trace);
        var first = new PassiveSkillRuntime { PassiveSkillId = 101, Level = 1 };
        var second = new PassiveSkillRuntime { PassiveSkillId = 102, Level = 1 };
        var actor = CreateActor(first, second);

        service.SyncActorPassives(actor, frame: 1);
        var plans = actor.ongoingTriggerPlans.Active;
        var revision = actor.ongoingTriggerPlans.Revision;
        var firstOwnerKey = plans.Single(entry => entry.TriggerIds.Contains(1001)).OwnerKey;
        var secondEntry = plans.Single(entry => entry.TriggerIds.Contains(1002));
        var secondOwnerKey = secondEntry.OwnerKey;
        var secondTriggerIds = secondEntry.TriggerIds;

        actor.ReplaceSkillLoadout(Array.Empty<ActiveSkillRuntime>(), new[] { second });
        service.SyncActorPassives(actor, frame: 2);

        Assert.False(service.IsPassiveOwnerKey(firstOwnerKey));
        Assert.True(service.IsPassiveOwnerKey(secondOwnerKey));
        Assert.Same(plans, actor.ongoingTriggerPlans.Active);
        Assert.Equal(revision + 1, actor.ongoingTriggerPlans.Revision);
        var remaining = Assert.Single(actor.ongoingTriggerPlans.Active);
        Assert.Same(secondEntry, remaining);
        Assert.Same(secondTriggerIds, remaining.TriggerIds);
        Assert.Equal(secondOwnerKey, remaining.OwnerKey);
        Assert.Equal(new[] { 1002 }, remaining.TriggerIds);
    }

    [Fact]
    public void Unregister_actor_releases_listener_plan_owner_binding_and_trace_root()
    {
        using var trace = new MobaTraceRegistry();
        var service = new MobaPassiveSkillLifecycleService(CreateConfigDatabase(), trace);
        var actor = CreateActor(new PassiveSkillRuntime { PassiveSkillId = 101, Level = 1 });

        service.SyncActorPassives(actor, frame: 1);
        var ownerKey = Assert.Single(actor.ongoingTriggerPlans.Active).OwnerKey;
        Assert.True(trace.TryGetNodeSnapshot(ownerKey, out var activeTrace));
        Assert.False(activeTrace.IsEnded);

        service.UnregisterActor(actor, frame: 2);

        Assert.False(actor.hasOngoingTriggerPlans);
        Assert.Empty(actor.passiveSkillTriggerListeners.Active);
        Assert.False(service.IsPassiveOwnerKey(ownerKey));
        Assert.True(trace.TryGetNodeSnapshot(ownerKey, out var endedTrace));
        Assert.True(endedTrace.IsEnded);
        Assert.Equal((int)TraceLifecycleReason.Cancelled, endedTrace.EndReason);
    }

    [Fact]
    public void Sync_actor_passives_activates_configured_continuous_process()
    {
        var configs = CreateConfigDatabase();
        var continuous = new DefaultContinuousManager();
        var processService = CreateProcessService(configs, continuous, new TestWorldClock());
        using var trace = new MobaTraceRegistry();
        var service = new MobaPassiveSkillLifecycleService(configs, trace, continuousProcesses: processService);
        var actor = CreateActor(new PassiveSkillRuntime { PassiveSkillId = 101, Level = 1 });

        service.SyncActorPassives(actor, frame: 10);

        var active = Assert.Single(continuous.GetOwnerActiveContinuous(1));
        var runtime = Assert.IsType<MobaTriggerIntervalContinuousRuntime>(active);
        Assert.Equal(9001, runtime.ProcessId);
        Assert.Equal(1, runtime.SourceActorId);
        Assert.Equal(1, runtime.TargetActorId);
        Assert.True(service.IsPassiveOwnerKey(runtime.SourceContextId));
    }

    [Fact]
    public void Sync_actor_passives_interrupts_out_of_combat_process_after_combat_is_recorded()
    {
        var configs = CreateConfigDatabase();
        var clock = new TestWorldClock { TimeSeconds = 10f };
        var combat = new MobaCombatActivityService(clock);
        var continuous = new DefaultContinuousManager();
        var processService = CreateProcessService(configs, continuous, clock, combat);
        using var trace = new MobaTraceRegistry();
        var service = new MobaPassiveSkillLifecycleService(configs, trace, continuousProcesses: processService);
        var actor = CreateActor(new PassiveSkillRuntime { PassiveSkillId = 101, Level = 1 });

        service.SyncActorPassives(actor, frame: 10);
        Assert.Equal(1, continuous.ActiveCount);

        combat.RecordCombat(1);
        service.SyncActorPassives(actor, frame: 11);

        Assert.Equal(0, continuous.ActiveCount);
    }

    private static ActorEntity CreateActor(params PassiveSkillRuntime[] passiveSkills)
    {
        var context = new ActorContext();
        var actor = context.CreateEntity();
        actor.AddActorId(1);
        actor.AddSkillLoadout(Array.Empty<ActiveSkillRuntime>(), passiveSkills);
        return actor;
    }

    private static MobaConfigDatabase CreateConfigDatabase()
    {
        var configs = new MobaConfigDatabase();
        var result = configs.ReloadFromDtoArrays(
            new Dictionary<Type, Array>
            {
                [typeof(PassiveSkillDTO)] = new[]
                {
                    new PassiveSkillDTO { Id = 101, Name = "passive_a", CooldownMs = 1000, TriggerIds = new[] { 1001 }, ContinuousProcessIds = new[] { 9001 } },
                    new PassiveSkillDTO { Id = 102, Name = "passive_b", CooldownMs = 500, TriggerIds = new[] { 1002 } },
                },
                [typeof(ContinuousProcessDTO)] = new[]
                {
                    new ContinuousProcessDTO
                    {
                        Id = 9001,
                        Name = "passive_process",
                        DurationMs = 0,
                        IntervalMs = 1000,
                        IntervalTriggerIds = new[] { 1003 },
                        RequireOutOfCombat = true,
                        OutOfCombatSeconds = 5,
                    },
                },
            },
            strict: false);

        Assert.True(result.Succeeded, result.Error);
        return configs;
    }

    private static MobaTriggerIntervalContinuousService CreateProcessService(
        MobaConfigDatabase configs,
        IContinuousManager continuous,
        TestWorldClock clock,
        MobaCombatActivityService combat = null)
    {
        var service = new MobaTriggerIntervalContinuousService();
        service.OnInit(new TestWorldResolver(
            configs,
            continuous,
            combat ?? new MobaCombatActivityService(clock)));
        return service;
    }

    private sealed class TestFrameTime : IFrameTime
    {
        public FrameIndex Frame => new FrameIndex(0);
        public float DeltaTime => 0f;
        public float Time => TimeSeconds;
        public float TimeSeconds { get; set; }
        public float FrameToTime(FrameIndex frame) => 0f;
        public FrameIndex TimeToFrame(float time) => new FrameIndex(0);
    }

    private sealed class TestWorldClock : IWorldClock
    {
        public float DeltaTime { get; private set; }
        public float Time => TimeSeconds;
        public float TimeSeconds { get; set; }

        public void Tick(float deltaTime)
        {
            DeltaTime = deltaTime;
            TimeSeconds += deltaTime;
        }
    }

    private sealed class TestWorldResolver : IWorldResolver
    {
        private readonly Dictionary<Type, object> _services = new();

        public TestWorldResolver(MobaConfigDatabase configs, IContinuousManager continuous, MobaCombatActivityService combat)
        {
            _services[typeof(MobaConfigDatabase)] = configs;
            _services[typeof(IContinuousManager)] = continuous;
            _services[typeof(MobaCombatActivityService)] = combat;
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

