using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Dispatcher;
using AbilityKit.Triggering.Runtime.Plan;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class FlatActionSchedulingAssemblyTests
{
    private static readonly EventKey<object> Event = new(9121);
    private static readonly ActionId Immediate = new(9122);
    private static readonly ActionId Delayed = new(9123);
    private const int PlanId = 9124;

    [Fact]
    public void Core_runtime_does_not_reference_or_export_the_optional_scheduler()
    {
        var core = typeof(TriggerRunner<>).Assembly;

        Assert.DoesNotContain(core.GetReferencedAssemblies(), name => name.Name == "AbilityKit.Triggering.ActionScheduling");
        Assert.DoesNotContain(core.GetExportedTypes(), type => type.Namespace == "AbilityKit.Triggering.Runtime.ActionScheduler");
    }

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
        var manager = new OneShotDelayExtension();
        var trigger = new PlannedTrigger<object, object>(CreateMixedPlan());
        var configured = CreateContext(bus, actions, manager);
        var missing = CreateContext(bus, actions, null);

        trigger.Execute(new object(), in configured);
        AssertMissingManager(Assert.Throws<InvalidOperationException>(() => trigger.Execute(new object(), in missing)));
        manager.Advance(10f);

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

        var manager = new OneShotDelayExtension();
        var configured = CreateContext(bus, actions, manager);
        trigger.Execute(in payload, in configured);
        trigger.Execute(in payload, in configured);
        Assert.Equal(new[] { Immediate }, calls);
        manager.Advance(10f);
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
    public void Flat_delay_can_be_driven_by_an_external_implementation_without_the_default_manager()
    {
        var bus = new EventBus();
        var calls = new List<ActionId>();
        var extension = new OneShotDelayExtension();
        var runner = new TriggerRunner<object>(bus, new FunctionRegistry(), CreateActions(calls), flatActionScheduler: extension);
        var plan = CreateMixedPlan();
        using var registration = runner.RegisterPlan(Event, in plan);

        bus.Publish(Event, new object());
        extension.Advance(9f);
        Assert.Equal(new[] { Immediate }, calls);
        extension.Advance(1f);
        extension.Advance(10f);
        Assert.Equal(new[] { Immediate, Delayed }, calls);
    }

    [Fact]
    public void External_implementation_can_refuse_unsupported_modes_before_immediate_actions()
    {
        var bus = new EventBus();
        var calls = new List<ActionId>();
        var extension = new OneShotDelayExtension();
        var context = new ExecCtx<object>(new object(), bus, new FunctionRegistry(), CreateActions(calls),
            blackboards: null!, payloads: null!, idNames: null!, numericDomains: null!, numericFunctions: null!,
            policy: default, control: new ExecutionControl(), flatActionScheduler: extension);
        var plan = new TriggerPlan<object>(0, 0, PlanId,
            actions: new[] { new ActionCallPlan(Immediate), new ActionCallPlan(Delayed).WithSchedule(EActionScheduleMode.Periodic, 10f) });
        var trigger = new PlannedTrigger<object, object>(in plan);

        Assert.Throws<NotSupportedException>(() => trigger.Execute(new object(), in context));
        Assert.Empty(calls);
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

    private static ExecCtx<object> CreateContext(EventBus bus, ActionRegistry actions, IFlatActionScheduler? manager) =>
        new(new object(), bus, new FunctionRegistry(), actions,
            blackboards: null!, payloads: null!, idNames: null!, numericDomains: null!, numericFunctions: null!,
            policy: default, control: new ExecutionControl(), flatActionScheduler: manager!);

    private static void AssertMissingManager(InvalidOperationException exception)
    {
        Assert.Contains($"TriggerId={PlanId}", exception.Message);
        Assert.Contains("Actions[1]", exception.Message);
        Assert.Contains("Mode=Delayed", exception.Message);
        Assert.Contains(nameof(IFlatActionScheduler), exception.Message);
    }

    private sealed class OneShotDelayExtension : IFlatActionScheduler, IFlatActionScheduleGroup
    {
        private object? _owner;
        private float _remainingMs;
        private Action? _pending;

        public IFlatActionScheduleGroup BindPlan(int triggerId, object owner, ReadOnlySpan<ActionCallPlan> actions)
        {
            if (triggerId != PlanId || (_owner != null && !ReferenceEquals(_owner, owner)))
                throw new InvalidOperationException("Test extension accepts one plan owner.");
            var delayed = 0;
            foreach (ref readonly var action in actions)
            {
                if (action.Schedule.Mode == EActionScheduleMode.Immediate) continue;
                if (action.Schedule.Mode != EActionScheduleMode.Delayed ||
                    action.Execution.Policy != EActionExecutionPolicy.Immediate || ++delayed > 1)
                    throw new NotSupportedException("Test extension accepts one delayed action with the default execution policy.");
            }
            _owner = owner;
            return this;
        }

        public void RegisterOrReplace(int planIndex, in ActionCallPlan plan,
            Action<object, ITriggerDispatcherContext> action, TriggerPredicate<object> condition, object boundArgs)
        {
            _remainingMs = plan.Schedule.Param;
            _pending = () =>
            {
                if (condition == null || condition(boundArgs, null!)) action(boundArgs, null!);
            };
        }

        public void Advance(float deltaMs)
        {
            _remainingMs -= deltaMs;
            if (_pending == null || _remainingMs > 0) return;
            var pending = _pending;
            _pending = null;
            pending();
        }
    }
}
