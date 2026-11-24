using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config.Plans;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class LegacyPlanRegistrationTests
{
    [Fact]
    public void Bulk_registration_filters_scope_and_zero_event_and_leaves_earlier_subscription_on_failure()
    {
        var database = new TriggerPlanJsonDatabase();
        database.LoadFromJson("""
            { "FormatVersion": 1, "Triggers": [
                { "TriggerId": 1, "EventId": 9181, "Actions": [{ "ActionId": 42 }] },
                { "TriggerId": 2, "EventId": 9182, "Scope": 1 },
                { "TriggerId": 3 },
                { "TriggerId": 4, "EventId": 9184, "Actions": [
                    { "ActionId": 42, "ScheduleMode": "Delayed", "ScheduleParam": 10 }
                ] },
                { "TriggerId": 5, "EventId": 9185 }
            ] }
            """);
        var bus = new EventBus();
        var actions = new ActionRegistry();
        var calls = 0;
        actions.Register<NamedAction0<object, object, object>>(new ActionId(42), (_, _, _) => calls++, isDeterministic: true);
        var runner = new TriggerRunner<object>(bus, new FunctionRegistry(), actions);

        var failure = Assert.Throws<InvalidOperationException>(() => database.RegisterAll(runner));
        Assert.Contains("TriggerId=4", failure.Message);
        Assert.False(bus.HasSubscribers(new EventKey<object>(0)));
        Assert.False(bus.HasSubscribers(new EventKey<object>(9182)));
        Assert.False(bus.HasSubscribers(new EventKey<object>(9184)));
        Assert.False(bus.HasSubscribers(new EventKey<object>(9185)));
        bus.Publish(new EventKey<object>(9181), new object());
        Assert.Equal(1, calls);

        database.RegisterAll(runner, TriggerPlanScope.OwnerBound);
        Assert.True(bus.HasSubscribers(new EventKey<object>(9182)));
    }

    [Fact]
    public void Bulk_registration_does_not_execute_the_stored_root()
    {
        var database = new TriggerPlanJsonDatabase();
        database.LoadFromJson("""
            { "FormatVersion": 1, "Triggers": [
                { "TriggerId": 1, "EventId": 9186,
                  "ExecutionRoot": { "Kind": "Action", "Action": { "ActionId": 43 } } }
            ] }
            """);
        Assert.True(database.TryGetExecutionRootByTriggerId(1, out _));
        var bus = new EventBus();
        var actions = new ActionRegistry();
        var calls = 0;
        actions.Register<NamedAction0<object, object, object>>(new ActionId(43), (_, _, _) => calls++, isDeterministic: true);
        var runner = new TriggerRunner<object>(bus, new FunctionRegistry(), actions);
        var key = new EventKey<object>(9186);

        database.RegisterAll(runner);
        Assert.True(bus.HasSubscribers(key));
        bus.Publish(key, new object());

        Assert.Equal(0, calls);
    }
}
