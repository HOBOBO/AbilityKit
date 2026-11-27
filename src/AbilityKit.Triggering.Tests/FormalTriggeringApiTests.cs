using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Example;
using AbilityKit.Triggering.Runtime.Plan;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class FormalTriggeringApiTests
{
    [Fact]
    public void Package_sample_executes_through_the_formal_path() => FormalTriggeringMainlineExample.RunOnce();

    [Fact]
    public void Package_sample_shows_nested_dispatch_and_disposal() => FormalTriggeringMainlineExample.RunNestedDispatchOnce();

    [Fact]
    public void Queued_event_sample_runs_independently() => QueuedEventBusExample.RunOnce();

    [Theory]
    [InlineData(EEventDispatchMode.Immediate)]
    [InlineData(EEventDispatchMode.Queued)]
    public void Plans_dispatch_by_phase_priority_and_registration_order(EEventDispatchMode mode)
    {
        var bus = new EventBus(new EventBusOptions(mode, maxFlushPasses: 4));
        var actions = new ActionRegistry();
        var runner = new TriggerRunner<object>(bus, new FunctionRegistry(), actions);
        var key = new EventKey<int>(15);
        var order = new List<int>();

        using var latePhase = Register(1, phase: 1, priority: 100);
        using var lowPriority = Register(2, phase: 0, priority: 1);
        using var highPriority = Register(3, phase: 0, priority: 20);
        using (Register(4, phase: 0, priority: 20))
        {
            bus.Publish(key, 42);
            if (mode == EEventDispatchMode.Queued)
                Assert.Empty(order);
            bus.Flush();
            Assert.Equal(new[] { 3, 4, 2, 1 }, order);
        }

        order.Clear();
        bus.Publish(key, 42);
        bus.Flush();
        Assert.Equal(new[] { 3, 2, 1 }, order);

        IDisposable Register(int id, int phase, int priority)
        {
            var actionId = new ActionId(id);
            actions.Register<NamedAction0<int, object, object>>(actionId, (_, _, _) => order.Add(id), isDeterministic: true);
            var plan = TriggerPlanFactory.Create<int>(phase, priority, actions: new[] { new ActionCallPlan(actionId) });
            return runner.RegisterPlan(key, in plan);
        }
    }
}
