using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Gameplay.Triggering;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config.Plans;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
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
    public void OnInit_rejects_value_type_owner_event_args_and_clears_previous_indexes()
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

        var ex = Assert.Throws<InvalidOperationException>(() => service.OnInit(null));

        Assert.Contains("reference type", ex.Message);
        Assert.Empty((IDictionary)GetPrivate(service, "_byTriggerId"));
        Assert.Empty((IDictionary)GetPrivate(service, "_argsTypeByTriggerId"));
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
}
