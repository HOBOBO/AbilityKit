using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.ActionScheduler;
using AbilityKit.Triggering.Runtime.Plan;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class FlatActionSchedulingAssemblyTests
{
    private static readonly EventKey<object> Event = new(9121);
    private static readonly ActionId Immediate = new(9122);
    private static readonly ActionId Delayed = new(9123);
    private const int PlanId = 9124;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Plan_registration_without_manager_rejects_before_subscribing(bool dynamicRegistration)
    {
        var bus = new EventBus();
        var calls = new List<ActionId>();
        var runner = new TriggerRunner<object>(bus, new FunctionRegistry(), CreateActions(calls));
        var mixed = CreateMixedPlan();

        void RegisterMixed()
        {
            using var registration = dynamicRegistration
                ? runner.RegisterPlan(Event.IntId, typeof(object), in mixed)
                : runner.RegisterPlan(Event, in mixed);
        }

        AssertMissingManager(Assert.Throws<InvalidOperationException>(RegisterMixed));
        Assert.False(bus.HasSubscribers(Event));

        var immediate = new TriggerPlan<object>(0, 0, 1, actions: new[] { new ActionCallPlan(Immediate) });
        using var existing = runner.RegisterPlan(Event, in immediate);
        AssertMissingManager(Assert.Throws<InvalidOperationException>(RegisterMixed));
        bus.Publish(Event, new object());
        Assert.Equal(new[] { Immediate }, calls);
    }

    [Fact]
    public void Assembly_failure_keeps_the_context_of_already_scheduled_actions()
    {
        var bus = new EventBus();
        var calls = new List<ActionId>();
        var delayedContexts = new List<object>();
        var actions = CreateActions(calls, ctx => delayedContexts.Add(ctx));
        var manager = new ActionSchedulerManager();
        var trigger = new PlannedTrigger<object, object>(CreateMixedPlan());
        var configured = CreateContext(bus, actions, manager);
        var missing = CreateContext(bus, actions, null);

        trigger.Execute(new object(), in configured);
        AssertMissingManager(Assert.Throws<InvalidOperationException>(() => trigger.Execute(new object(), in missing)));
        manager.Update(10f, null!);

        Assert.Equal(new[] { Immediate, Delayed }, calls);
        Assert.Same(configured.Context, Assert.Single(delayedContexts));
    }

    [Fact]
    public void Direct_mixed_plan_failure_has_no_partial_actions_or_consumed_once_eligibility()
    {
        var bus = new EventBus();
        var calls = new List<ActionId>();
        var plan = CreateMixedPlan(new TriggerExecutionControlPlan(ETriggerExecutionMode.Once));
        var trigger = new PlannedTrigger<object, object>(plan);
        var actions = CreateActions(calls);
        var missing = CreateContext(bus, actions, null);
        var payload = new object();

        AssertMissingManager(Assert.Throws<InvalidOperationException>(() => trigger.Execute(in payload, in missing)));
        Assert.Empty(calls);

        var manager = new ActionSchedulerManager();
        var configured = CreateContext(bus, actions, manager);
        trigger.Execute(in payload, in configured);
        trigger.Execute(in payload, in configured);
        Assert.Equal(new[] { Immediate }, calls);
        manager.Update(10f, null!);
        Assert.Equal(new[] { Immediate, Delayed }, calls);
    }

    [Fact]
    public void Generic_registration_isolates_the_failure_and_keeps_other_rules_running()
    {
        var bus = new EventBus();
        var calls = new List<ActionId>();
        var runner = new TriggerRunner<object>(bus, new FunctionRegistry(), CreateActions(calls));
        var mixed = new PlannedTrigger<object, object>(CreateMixedPlan());
        var immediate = new TriggerPlan<object>(1, 0, 1, actions: new[] { new ActionCallPlan(Immediate) });
        using var invalid = runner.Register(Event, mixed);
        using var valid = runner.RegisterPlan(Event, in immediate);

        bus.Publish(Event, new object());

        Assert.Equal(new[] { Immediate }, calls);
    }

    [Fact]
    public void Registered_plan_waits_for_the_configured_manager_and_deadline()
    {
        var bus = new EventBus();
        var calls = new List<ActionId>();
        var manager = new ActionSchedulerManager();
        var runner = new TriggerRunner<object>(bus, new FunctionRegistry(), CreateActions(calls), actionSchedulerManager: manager);
        var plan = CreateMixedPlan();
        using var registration = runner.RegisterPlan(Event, in plan);

        bus.Publish(Event, new object());
        new ActionSchedulerManager().Update(10f, null!);
        manager.Update(9f, null!);
        Assert.Equal(new[] { Immediate }, calls);

        manager.Update(1f, null!);
        manager.Update(10f, null!);
        Assert.Equal(new[] { Immediate, Delayed }, calls);
    }

    [Fact]
    public void Execution_root_ignores_unused_flat_actions()
    {
        var executed = 0;
        var plan = CreateMixedPlan();
        var trigger = new PlannedTrigger<object, object>(plan, (in object payload, in ExecCtx<object> context) =>
        {
            executed++;
            return TriggerPlanExecutionResult.Success();
        });

        trigger.Execute(new object(), default(ExecCtx<object>));

        Assert.Equal(1, executed);
    }

    private static TriggerPlan<object> CreateMixedPlan(TriggerExecutionControlPlan control = default) =>
        new(0, 0, PlanId, actions: new[] { new ActionCallPlan(Immediate), Delayed.Delayed(10f) }, executionControl: control);

    private static ActionRegistry CreateActions(List<ActionId> calls, Action<object>? observeDelayedContext = null)
    {
        var actions = new ActionRegistry();
        actions.Register<NamedAction0<object, object, object>>(Immediate, (_, _, _) => calls.Add(Immediate), isDeterministic: true);
        actions.Register<NamedAction0<object, object, object>>(Delayed, (_, _, ctx) =>
        {
            calls.Add(Delayed);
            observeDelayedContext?.Invoke(ctx.Context);
        }, isDeterministic: true);
        return actions;
    }

    private static ExecCtx<object> CreateContext(EventBus bus, ActionRegistry actions, ActionSchedulerManager? manager) =>
        new(new object(), bus, new FunctionRegistry(), actions,
            blackboards: null!, payloads: null!, idNames: null!, numericDomains: null!, numericFunctions: null!,
            policy: default, control: new ExecutionControl(), actionSchedulerManager: manager!);

    private static void AssertMissingManager(InvalidOperationException exception)
    {
        Assert.Contains($"TriggerId={PlanId}", exception.Message);
        Assert.Contains("Actions[1]", exception.Message);
        Assert.Contains("Mode=Delayed", exception.Message);
        Assert.Contains(nameof(ActionSchedulerManager), exception.Message);
    }
}
