using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class TriggerRunnerDispatchMutationTests
{
    private static readonly EventKey<bool> Event = new(9183);

    [Fact]
    public void Self_disposal_does_not_skip_the_next_rule()
    {
        var bus = new EventBus();
        var runner = CreateRunner(bus);
        var calls = new List<string>();
        IDisposable? self = null;
        using var registration = runner.Register(Event, Trigger(_ =>
        {
            calls.Add("self");
            self!.Dispose();
        }), priority: 10);
        self = registration;
        using var next = runner.Register(Event, Trigger(_ => calls.Add("next")));

        bus.Publish(Event, false);
        bus.Publish(Event, false);

        Assert.Equal(new[] { "self", "next", "next" }, calls);
    }

    [Fact]
    public void Disposing_a_pending_registration_skips_it_without_skipping_the_tail()
    {
        var bus = new EventBus();
        var runner = CreateRunner(bus);
        var calls = new List<string>();
        var removedEvaluations = 0;
        using var removed = runner.Register(Event, new DelegateTrigger<bool, object>((_, _) =>
        {
            removedEvaluations++;
            return true;
        }, (_, _) => calls.Add("removed")), priority: 5);
        using var first = runner.Register(Event, Trigger(_ =>
        {
            calls.Add("first");
            removed.Dispose();
        }), priority: 10);
        using var tail = runner.Register(Event, Trigger(_ => calls.Add("tail")));

        bus.Publish(Event, false);

        Assert.Equal(0, removedEvaluations);
        Assert.Equal(new[] { "first", "tail" }, calls);
    }

    [Theory]
    [InlineData(20, "added,first,tail")]
    [InlineData(-1, "first,tail,added")]
    public void Adding_a_rule_during_dispatch_only_affects_the_next_dispatch(int addedPriority, string nextOrder)
    {
        var bus = new EventBus();
        var runner = CreateRunner(bus);
        var calls = new List<string>();
        IDisposable? added = null;
        using var first = runner.Register(Event, Trigger(_ =>
        {
            calls.Add("first");
            added ??= runner.Register(Event, Trigger(_ => calls.Add("added")), priority: addedPriority);
        }), priority: 10);
        using var tail = runner.Register(Event, Trigger(_ => calls.Add("tail")));

        bus.Publish(Event, false);
        using var addedRegistration = added;
        Assert.Equal(new[] { "first", "tail" }, calls);

        calls.Clear();
        bus.Publish(Event, false);
        Assert.Equal(nextOrder.Split(','), calls);
    }

    [Fact]
    public void Nested_dispatch_sees_updated_registrations_without_changing_the_outer_order()
    {
        var bus = new EventBus();
        var runner = CreateRunner(bus);
        var calls = new List<string>();
        using var removed = runner.Register(Event, Trigger(_ => calls.Add("removed")), priority: 5);
        IDisposable? added = null;
        using var first = runner.Register(Event, Trigger(nested =>
        {
            calls.Add(nested ? "inner:first" : "outer:first");
            if (nested) return;
            removed.Dispose();
            added ??= runner.Register(Event, Trigger(inner => calls.Add(inner ? "inner:new" : "outer:new")), priority: 20);
            bus.Publish(Event, true);
        }), priority: 10);
        using var tail = runner.Register(Event, Trigger(nested => calls.Add(nested ? "inner:tail" : "outer:tail")));

        bus.Publish(Event, false);
        using var addedRegistration = added;

        Assert.Equal(new[] { "outer:first", "inner:new", "inner:first", "inner:tail", "outer:tail" }, calls);
    }

    [Fact]
    public void Entered_rule_finishes_when_its_condition_disposes_its_own_registration()
    {
        var bus = new EventBus();
        var runner = CreateRunner(bus);
        var calls = new List<string>();
        IDisposable? self = null;
        var trigger = new DelegateTrigger<bool, object>((_, _) =>
        {
            calls.Add("evaluate");
            self!.Dispose();
            return true;
        }, (_, _) => calls.Add("execute"));
        using var registration = runner.Register(Event, trigger, priority: 10);
        self = registration;
        using var other = runner.Register(Event, trigger);

        bus.Publish(Event, false);
        bus.Publish(Event, false);

        Assert.Equal(new[] { "evaluate", "execute", "evaluate", "execute", "evaluate", "execute" }, calls);
    }

    [Fact]
    public void Replacing_the_last_rule_during_dispatch_updates_only_later_dispatches()
    {
        var bus = new EventBus();
        var runner = CreateRunner(bus);
        var calls = new List<string>();
        IDisposable? self = null;
        IDisposable? replacement = null;
        using var registration = runner.Register(Event, Trigger(_ =>
        {
            calls.Add("outer");
            self!.Dispose();
            Assert.False(bus.HasSubscribers(Event));
            replacement = runner.Register(Event, Trigger(nested => calls.Add(nested ? "nested" : "next")));
            bus.Publish(Event, true);
        }));
        self = registration;

        bus.Publish(Event, false);
        using var active = Assert.IsAssignableFrom<IDisposable>(replacement);
        Assert.Equal(new[] { "outer", "nested" }, calls);

        registration.Dispose();
        bus.Publish(Event, false);
        Assert.Equal(new[] { "outer", "nested", "next" }, calls);

        active.Dispose();
        Assert.False(bus.HasSubscribers(Event));
    }

    [Fact]
    public void Earlier_bus_callback_can_replace_the_last_rule_before_runner_dispatch_starts()
    {
        var bus = new EventBus();
        var runner = CreateRunner(bus);
        var calls = new List<string>();
        IDisposable? original = null;
        IDisposable? replacement = null;
        using var earlier = bus.Subscribe(Event, _ =>
        {
            if (replacement != null) return;
            original!.Dispose();
            replacement = runner.Register(Event, Trigger(_ => calls.Add("replacement")));
        });
        using var registration = runner.Register(Event, Trigger(_ => calls.Add("original")));
        original = registration;

        bus.Publish(Event, false);
        using var active = Assert.IsAssignableFrom<IDisposable>(replacement);
        Assert.Equal(new[] { "replacement" }, calls);

        bus.Publish(Event, false);
        Assert.Equal(new[] { "replacement", "replacement" }, calls);
    }

    [Fact]
    public void Releasing_one_event_slot_does_not_affect_other_keys_or_payload_types()
    {
        var bus = new EventBus();
        var runner = CreateRunner(bus);
        var otherKey = new EventKey<bool>(9184);
        var otherType = new EventKey<int>(9185);
        var calls = new List<string>();
        using var first = runner.Register(Event, Trigger(_ => calls.Add("first")));
        using var second = runner.Register(Event, Trigger(_ => calls.Add("second")));
        using var other = runner.Register(otherKey, Trigger(_ => calls.Add("other")));
        using var typed = runner.Register(otherType,
            new DelegateTrigger<int, object>((_, _) => true, (value, _) => calls.Add($"int:{value}")));

        bus.Publish(Event, false);
        Assert.Equal(new[] { "first", "second" }, calls);
        first.Dispose();
        Assert.True(bus.HasSubscribers(Event));
        second.Dispose();
        Assert.False(bus.HasSubscribers(Event));

        bus.Publish(otherKey, true);
        bus.Publish(otherType, 7);
        Assert.Equal(new[] { "first", "second", "other", "int:7" }, calls);
    }

    private static TriggerRunner<object> CreateRunner(EventBus bus) => new(bus, new FunctionRegistry(), new ActionRegistry());

    private static ITrigger<bool, object> Trigger(Action<bool> execute) => new DelegateTrigger<bool, object>((_, _) => true, (args, _) => execute(args));
}
