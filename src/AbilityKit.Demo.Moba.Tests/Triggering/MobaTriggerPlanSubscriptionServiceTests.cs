using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Eventing;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Events.Unit;
using AbilityKit.Demo.Moba.Gameplay.Triggering;
using AbilityKit.Demo.Moba.Predicates;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config.Plans;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Triggering.Testing;
using AbilityKit.Triggering.Variables.Numeric;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Triggering;

public sealed class MobaTriggerPlanSubscriptionServiceTests
{
    [Fact]
    public void OnInit_releases_previous_owner_registrations_and_evaluations()
    {
        const long ownerKey = 9201;
        const int triggerId = 9202;

        var service = new MobaTriggerPlanSubscriptionService();
        var ownerBlackboards = new OwnerBlackboardStore();
        ownerBlackboards.GetOrCreate(ownerKey);
        SetPrivate(service, "_ownerBlackboards", ownerBlackboards);

        var registration = new TrackingDisposable();
        var registrations = (IDictionary)GetPrivate(service, "_regsByOwnerKey");
        registrations[ownerKey] = new Dictionary<int, IDisposable>
        {
            [triggerId] = registration,
        };

        var evaluations = (IDictionary)GetPrivate(service, "_evaluationsByOwnerKey");
        evaluations[ownerKey] = new Dictionary<int, MobaTriggerPlanSubscriptionService.TriggerEvaluationSnapshot>
        {
            [triggerId] = new MobaTriggerPlanSubscriptionService.TriggerEvaluationSnapshot(1, true, true, true),
        };

        service.OnInit(null);

        Assert.True(registration.Disposed);
        Assert.Empty(registrations);
        Assert.Empty(evaluations);
        Assert.False(ownerBlackboards.TryGet(ownerKey, out _));
    }

    [Fact]
    public void RemoveStaleEvaluations_removes_trigger_ids_outside_current_binding()
    {
        const long ownerKey = 9203;
        const int staleTriggerId = 9204;

        var service = new MobaTriggerPlanSubscriptionService();
        InvokePrivate(service, "RecordEvaluation", ownerKey, staleTriggerId, true, true, true);

        Assert.True(service.TryGetLastEvaluation(ownerKey, staleTriggerId, out _));

        InvokePrivate(service, "RemoveStaleEvaluations", ownerKey, new[] { 9205 });

        Assert.False(service.TryGetLastEvaluation(ownerKey, staleTriggerId, out _));
    }

    [Fact]
    public void OnInit_accepts_value_type_owner_event_args()
    {
        const int validTriggerId = 9101;
        const int invalidTriggerId = 9102;
        const string validEventName = "damage.apply.after";
        const string invalidEventName = "test.owner.value_args";

        var registry = new MobaEventSubscriptionRegistry();
        registry.RegisterExact(invalidEventName, typeof(int));

        var service = new MobaTriggerPlanSubscriptionService();
        SetPrivate(service, "_eventRegistry", registry);
        SetPrivate(service, "_db", CreatePlans(validTriggerId, validEventName));
        service.OnInit(null);

        var records = (IDictionary)GetPrivate(service, "_byTriggerId");
        Assert.True(records.Contains(validTriggerId));

        SetPrivate(service, "_db", CreatePlans(invalidTriggerId, invalidEventName));

        service.OnInit(null);

        Assert.True(records.Contains(invalidTriggerId));
        var argsTypes = (IDictionary)GetPrivate(service, "_argsTypeByTriggerId");
        Assert.Equal(typeof(int), argsTypes[invalidTriggerId]);
    }

    [Fact]
    public void Owner_bound_value_type_event_runs_only_for_matching_owner_and_tracks_effect_contexts()
    {
        const int triggerId = 9301;
        const int actionId = 9302;
        const long firstOwnerKey = 9303;
        const long secondOwnerKey = 9304;
        const int firstOwnerActorId = 9305;
        const int secondOwnerActorId = 9306;
        const string eventName = MobaUnitTriggering.Events.Die;

        using var executionContexts = new MobaExecutionContextRegistry();
        var firstSourceContext = executionContexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SkillCast, 1, firstOwnerActorId, firstOwnerActorId, frame: 1));
        var secondSourceContext = executionContexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SkillCast, 1, secondOwnerActorId, secondOwnerActorId, frame: 1));

        var functions = new FunctionRegistry();
        MobaPlanPredicateFunctions.Register(functions);
        var actions = new ActionRegistry();
        var payloads = new PayloadAccessorRegistry();
        var plans = new TriggerPlanJsonDatabase();
        var predicate = new PredicateExprPlan(new[]
        {
            BoolExprNode.Function(
                MobaPlanPredicateFunctions.OwnerMatchesPayloadSourceFunctionId,
                arity: 2,
                NumericValueRef.Const(0),
                NumericValueRef.Const(0)),
        });
        var plan = new TriggerPlan<object>(
            phase: 0,
            priority: 0,
            triggerId: triggerId,
            predicateExpr: predicate,
            actions: new[] { new ActionCallPlan(new ActionId(actionId)) });
        var eventId = TriggeringIdUtil.GetEventEid(eventName);
        plans.AddRecord(new TriggerPlanJsonDatabase.Record(
            triggerId,
            eventName,
            eventId,
            TriggerPlanScope.OwnerBound,
            in plan));

        var gates = new MobaOwnerBoundTriggerGateService();
        var ownerGate = new TestOwnerExecutionGate(triggerId);
        ownerGate.Add(firstOwnerKey, new MobaOwnerBoundTriggerExecutionSource(
            firstOwnerActorId,
            firstOwnerActorId,
            firstSourceContext.ContextId,
            firstSourceContext.RootContextId,
            firstSourceContext.OwnerContextId,
            triggerId));
        ownerGate.Add(secondOwnerKey, new MobaOwnerBoundTriggerExecutionSource(
            secondOwnerActorId,
            secondOwnerActorId,
            secondSourceContext.ContextId,
            secondSourceContext.RootContextId,
            secondSourceContext.OwnerContextId,
            triggerId));
        gates.RegisterGate(ownerGate);

        var ownerBlackboards = new OwnerBlackboardStore();
        var frameTime = new FixedFrameTime();
        var effects = new MobaEffectExecutionService();
        var services = new TestWorldResolver(gates, effects, executionContexts, frameTime, ownerBlackboards);
        SetPrivate(effects, "_services", services);
        SetPrivate(effects, "_planDb", plans);
        SetPrivate(effects, "_planEventBus", null);
        SetPrivate(effects, "_planFunctions", functions);
        SetPrivate(effects, "_planActions", actions);
        SetPrivate(effects, "_planPayloads", payloads);
        SetPrivate(effects, "_frameTime", frameTime);
        SetPrivate(effects, "_ownerBlackboards", ownerBlackboards);
        typeof(MobaEffectExecutionService)
            .GetProperty("ExecutionContexts", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(effects, executionContexts);

        var observations = new List<(UnitDieEventPayload Payload, long EffectContextId, long ActionContextId)>();
        actions.Register<NamedAction0<UnitDieEventPayload, object, IWorldResolver>>(
            new ActionId(actionId),
            (payload, _, _) => observations.Add((payload, effects.CurrentEffectContextId, effects.CurrentActionChain.Single())),
            isDeterministic: true);

        var eventRegistry = new MobaEventSubscriptionRegistry();
        using var harness = new TriggeringTestHarness<IWorldResolver>(
            services,
            functions: functions,
            actions: actions);
        var subscriptions = new MobaTriggerPlanSubscriptionService();
        SetPrivate(subscriptions, "_db", plans);
        SetPrivate(subscriptions, "_runner", harness.Runner);
        SetPrivate(subscriptions, "_planEventBus", harness.EventBus);
        SetPrivate(subscriptions, "_eventRegistry", eventRegistry);
        SetPrivate(subscriptions, "_ownerBoundGates", gates);
        SetPrivate(subscriptions, "_effects", effects);
        SetPrivate(subscriptions, "_ownerBlackboards", ownerBlackboards);
        subscriptions.OnInit(services);
        subscriptions.ApplyTriggers(new[] { triggerId }, firstOwnerKey);
        subscriptions.ApplyTriggers(new[] { triggerId }, secondOwnerKey);

        var firstKill = new UnitDieEventPayload(9401, firstOwnerActorId, 1, 1, 1, 100f);
        var secondKill = new UnitDieEventPayload(9402, secondOwnerActorId, 1, 1, 1, 100f);
        harness.Publish(new EventKey<UnitDieEventPayload>(eventId), in firstKill);
        harness.Publish(new EventKey<UnitDieEventPayload>(eventId), in secondKill);

        Assert.Equal(2, observations.Count);
        Assert.Equal(firstOwnerActorId, observations[0].Payload.KillerActorId);
        Assert.Equal(secondOwnerActorId, observations[1].Payload.KillerActorId);
        Assert.Equal(2, ownerGate.CompletedCount);

        foreach (var observation in observations)
        {
            Assert.True(executionContexts.TryGet(observation.EffectContextId, out var effectContext));
            Assert.True(executionContexts.TryGet(observation.ActionContextId, out var actionContext));
            Assert.Equal(MobaExecutionKind.EffectExecution, effectContext.Kind);
            Assert.Equal(triggerId, effectContext.TriggerId);
            Assert.Equal(MobaExecutionKind.EffectAction, actionContext.Kind);
            Assert.Equal(effectContext.ContextId, actionContext.ParentContextId);
            Assert.True(effectContext.IsEnded);
            Assert.True(actionContext.IsEnded);
            Assert.Equal((int)MobaExecutionEndReason.Completed, effectContext.EndReason);
            Assert.Equal((int)MobaExecutionEndReason.Completed, actionContext.EndReason);
        }

        subscriptions.Dispose();
        gates.Dispose();
    }

    private static TriggerPlanJsonDatabase CreatePlans(int triggerId, string eventName)
    {
        var plans = new TriggerPlanJsonDatabase();
        var plan = new TriggerPlan<object>(phase: 0, priority: 0, triggerId: triggerId);
        plans.AddRecord(new TriggerPlanJsonDatabase.Record(
            triggerId,
            eventName,
            eventId: 1,
            scope: TriggerPlanScope.OwnerBound,
            in plan));
        return plans;
    }

    private static object GetPrivate(object instance, string name)
    {
        return instance.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance)!;
    }

    private static void SetPrivate(object instance, string name, object value)
    {
        instance.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(instance, value);
    }

    private static void InvokePrivate(object instance, string name, params object[] arguments)
    {
        instance.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(instance, arguments);
    }

    private sealed class TrackingDisposable : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    private sealed class TestOwnerExecutionGate :
        IMobaOwnerBoundTriggerGate,
        IMobaOwnerBoundTriggerExecutionSourceProvider
    {
        private readonly int _triggerId;
        private readonly Dictionary<long, MobaOwnerBoundTriggerExecutionSource> _sources = new();

        public TestOwnerExecutionGate(int triggerId) => _triggerId = triggerId;

        public int CompletedCount { get; private set; }

        public void Add(long ownerKey, MobaOwnerBoundTriggerExecutionSource source) => _sources.Add(ownerKey, source);

        public bool IsMatch(long ownerKey, int triggerId) => triggerId == _triggerId && _sources.ContainsKey(ownerKey);

        public bool CanExecute(long ownerKey, int triggerId) => IsMatch(ownerKey, triggerId);

        public void Complete(long ownerKey, int triggerId)
        {
            if (IsMatch(ownerKey, triggerId)) CompletedCount++;
        }

        public bool TryGetExecutionSource(
            long ownerKey,
            int triggerId,
            out MobaOwnerBoundTriggerExecutionSource source)
        {
            source = default;
            return IsMatch(ownerKey, triggerId) && _sources.TryGetValue(ownerKey, out source);
        }
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
        private readonly object[] _services;

        public TestWorldResolver(params object[] services) => _services = services;

        public object Resolve(Type serviceType) => Array.Find(_services, serviceType.IsInstanceOfType);

        public T Resolve<T>() => TryResolve<T>(out var value) ? value : default!;

        public bool TryResolve(Type serviceType, out object instance)
        {
            instance = Resolve(serviceType);
            return instance != null;
        }

        public bool TryResolve<T>(out T instance)
        {
            var value = Resolve(typeof(T));
            instance = value is T typed ? typed : default!;
            return value is T;
        }
    }
}
