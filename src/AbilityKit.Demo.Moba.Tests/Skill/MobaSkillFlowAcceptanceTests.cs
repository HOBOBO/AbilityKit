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
using AbilityKit.Deterministic;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class MobaSkillFlowAcceptanceTests
{
    private const int ActorId = 8401;
    private const int Instant = 8411;
    private const int Charge = 8412;
    private const int Volley = 8413;

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

        public Scope()
        {
            var actor = _contexts.actor.CreateEntity();
            actor.AddActorId(ActorId);
            actor.AddTeam(Team.Team1);
            actor.AddTransform(new Transform3(Vec3.Zero, Quat.Identity, Vec3.One));
            SkillOne = NewSkill(Instant);
            actor.AddSkillLoadout(new[] { SkillOne, NewSkill(Charge), NewSkill(Volley) }, Array.Empty<PassiveSkillRuntime>());
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
            trigger.RuntimeHandle = runtime.Handle;
            trigger.RuntimeId = runtime.RuntimeId;
            return Runner.TryStart(pre, prePhases, cast, phases, new object(), in request, trigger, in policy);
        }

        public SkillPipelineContext NewContext(int id, int slot)
        {
            var aim = Vec3.Zero;
            var direction = Vec3.Forward;
            var root = _executionContexts.Create(new MobaExecutionContextCreateRequest(
                MobaExecutionKind.SkillCast, id, ActorId, 0, frame: 10));
            var runtime = Runtimes.Create(new MobaSkillCastRuntimeCreateRequest(
                id, slot, 1, ++_sequence, ActorId, 0, in aim, in direction, root.ContextId));
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
