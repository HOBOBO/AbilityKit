using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.ActionScheduler;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Dispatcher;
using AbilityKit.Triggering.Runtime.Plan;
using Xunit;

namespace AbilityKit.Triggering.ActionScheduling.Tests;

public sealed class FlatActionSchedulerIntegrationTests
{
    private static readonly EventKey<object> Event = new(9121);
    private static readonly ActionId Immediate = new(9122);
    private static readonly ActionId Delayed = new(9123);
    private const int PlanId = 9124;

    [Fact]
    public void Registered_plan_waits_for_the_configured_manager_and_deadline()
    {
        var bus = new EventBus();
        var calls = new List<ActionId>();
        var manager = new ActionSchedulerManager();
        var runner = new TriggerRunner<object>(bus, new FunctionRegistry(), CreateActions(calls), flatActionScheduler: manager);
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
    public void Conflicting_plan_cannot_run_partial_actions_or_replace_another_plans_slot()
    {
        var bus = new EventBus();
        var calls = new List<ActionId>();
        var manager = new ActionSchedulerManager();
        var context = CreateContext(bus, CreateActions(calls), manager);
        var firstPlan = CreateMixedPlan();
        var secondPlan = CreateMixedPlan(new TriggerExecutionControlPlan(ETriggerExecutionMode.Once));
        var first = new PlannedTrigger<object, object>(in firstPlan);
        var second = new PlannedTrigger<object, object>(in secondPlan);

        first.Execute(new object(), in context);
        manager.Update(5f, null!);
        var failure = Assert.Throws<InvalidOperationException>(() => second.Execute(new object(), in context));
        Assert.Contains($"TriggerId={PlanId}", failure.Message);
        Assert.Equal(new[] { Immediate }, calls);
        Assert.Equal(1, manager.TotalActionCount);
        manager.Update(5f, null!);
        Assert.Equal(new[] { Immediate, Delayed }, calls);

        Assert.True(manager.RemoveScheduler(PlanId));
        second.Execute(new object(), in context);
        manager.Update(10f, null!);
        Assert.Equal(new[] { Immediate, Delayed, Immediate, Delayed }, calls);
    }

    [Fact]
    public void Unsubscribing_leaves_pending_actions_until_host_removes_the_group()
    {
        var bus = new EventBus();
        var calls = new List<ActionId>();
        var manager = new ActionSchedulerManager();
        var runner = new TriggerRunner<object>(bus, new FunctionRegistry(), CreateActions(calls), flatActionScheduler: manager);
        var plan = CreateMixedPlan();
        using (runner.RegisterPlan(Event, in plan))
            bus.Publish(Event, new object());

        Assert.False(bus.HasSubscribers(Event));
        Assert.Equal(1, manager.TotalActionCount);
        Assert.True(manager.RemoveScheduler(PlanId));
        manager.Update(10f, null!);
        Assert.Equal(new[] { Immediate }, calls);
        Assert.Equal(0, manager.TotalActionCount);
    }

    [Theory]
    [InlineData(ETriggerExecutionMode.Once)]
    [InlineData(ETriggerExecutionMode.Always)]
    public void Delayed_action_and_condition_use_the_context_of_the_accepted_registration(ETriggerExecutionMode mode)
    {
        var manager = new ActionSchedulerManager();
        var functions = new FunctionRegistry();
        var actions = new ActionRegistry();
        var predicate = new FunctionId(9125);
        var conditionCalls = new List<(string Payload, string Context, ExecutionControl Control, float TimeMs)>();
        var actionCalls = new List<(string Payload, string Context, ExecutionControl Control, float TimeMs)>();
        functions.Register<Predicate0<string, string>>(predicate, (payload, ctx) =>
        {
            conditionCalls.Add((payload, ctx.Context, ctx.Control, ctx.Policy.TotalTimeMs));
            return true;
        }, isDeterministic: true);
        actions.Register<NamedAction0<string, object, string>>(Delayed, (payload, _, ctx) =>
            actionCalls.Add((payload, ctx.Context, ctx.Control, ctx.Policy.TotalTimeMs)), isDeterministic: true);

        var plan = new TriggerPlan<string>(0, 0, PlanId, predicate, predicateArgs: null!,
            actions: new[] { Delayed.Delayed(10f) }, executionControl: new TriggerExecutionControlPlan(mode));
        var trigger = new PlannedTrigger<string, string>(in plan);
        var first = CreateActivation("首次上下文", 100f);
        var second = CreateActivation("后续上下文", 200f);
        var tickControl = new ExecutionControl();

        trigger.Execute("首次参数", in first);
        manager.Update(5f, null!, tickControl);
        trigger.Execute("后续参数", in second);
        manager.Update(4f, null!, tickControl);
        Assert.Empty(conditionCalls);
        Assert.Empty(actionCalls);

        var once = mode == ETriggerExecutionMode.Once;
        manager.Update(1f, null!, tickControl);
        if (!once)
        {
            Assert.Empty(conditionCalls);
            Assert.Empty(actionCalls);
            manager.Update(5f, null!, tickControl);
        }

        var expected = once
            ? ("首次参数", first.Context, first.Control, first.Policy.TotalTimeMs)
            : ("后续参数", second.Context, second.Control, second.Policy.TotalTimeMs);
        Assert.Equal(expected, Assert.Single(conditionCalls));
        Assert.Equal(expected, Assert.Single(actionCalls));

        ExecCtx<string> CreateActivation(string context, float timeMs) =>
            new(context, null!, functions, actions, blackboards: null!, payloads: null!, idNames: null!,
                numericDomains: null!, numericFunctions: null!, policy: new ExecPolicy(false, totalTimeMs: timeMs),
                control: new ExecutionControl(), flatActionScheduler: manager);
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

    private static ExecCtx<object> CreateContext(EventBus bus, ActionRegistry actions, ActionSchedulerManager manager) =>
        new(new object(), bus, new FunctionRegistry(), actions,
            blackboards: null!, payloads: null!, idNames: null!, numericDomains: null!, numericFunctions: null!,
            policy: default, control: new ExecutionControl(), flatActionScheduler: manager);
}
