using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class ExecutionRootOutcomeBaselineTests
{
    private static readonly EventKey<object> Event = new(9180);

    [Theory]
    [InlineData(ETriggerPlanExecutionStatus.Success)]
    [InlineData(ETriggerPlanExecutionStatus.Skipped)]
    [InlineData(ETriggerPlanExecutionStatus.Failed)]
    [InlineData(null)]
    public void Runner_reports_normal_return_and_only_exceptions_as_action_failure(ETriggerPlanExecutionStatus? status)
    {
        var fixture = new RootFixture(status);
        var bus = new EventBus();
        var lifecycle = new RecordingLifecycle();
        var runner = new TriggerRunner<object>(bus, new FunctionRegistry(), new ActionRegistry(), lifecycle: lifecycle);
        var subsequent = new RootFixture(ETriggerPlanExecutionStatus.Success);
        using var first = runner.Register(Event, fixture.Create(interruptPriority: 10), priority: 10);
        using var second = runner.Register(Event, subsequent.Create(), priority: 9);

        bus.Publish(Event, new object());

        Assert.Equal(1, fixture.Attempts);
        Assert.Equal(status.HasValue ? 0 : 1, subsequent.Attempts);
        Assert.Equal(new[] { status.HasValue ? 10 : 9 }, lifecycle.Completed);
        Assert.Equal(status.HasValue ? Array.Empty<string>() : new[] { fixture.Error.Message }, lifecycle.Failures);
        Assert.Equal((1, status.HasValue ? 1 : 0), lifecycle.DispatchCounts);
    }

    [Theory]
    [InlineData(ETriggerPlanExecutionStatus.Success)]
    [InlineData(ETriggerPlanExecutionStatus.Skipped)]
    [InlineData(ETriggerPlanExecutionStatus.Failed)]
    [InlineData(null)]
    public void Root_attempt_consumes_once_and_starts_cooldown_even_when_it_throws(ETriggerPlanExecutionStatus? status)
    {
        var onceFixture = new RootFixture(status);
        var once = onceFixture.Create(new TriggerExecutionControlPlan(ETriggerExecutionMode.Once));
        var cooldownFixture = new RootFixture(status);
        var cooldown = cooldownFixture.Create(new TriggerExecutionControlPlan(ETriggerExecutionMode.Cooldown, cooldownMs: 10));

        AssertAttempt(once, onceFixture, timeMs: 100);
        Execute(once, timeMs: 200);
        Assert.Equal(1, onceFixture.Attempts);

        AssertAttempt(cooldown, cooldownFixture, timeMs: 100);
        Execute(cooldown, timeMs: 109);
        Assert.Equal(1, cooldownFixture.Attempts);
        AssertAttempt(cooldown, cooldownFixture, timeMs: 110);
        Assert.Equal(2, cooldownFixture.Attempts);
    }

    private static void AssertAttempt(PlannedTrigger<object, object> trigger, RootFixture fixture, float timeMs)
    {
        if (fixture.Throws)
            Assert.Same(fixture.Error, Assert.Throws<InvalidOperationException>(() => Execute(trigger, timeMs)));
        else
            Execute(trigger, timeMs);
    }

    private static void Execute(PlannedTrigger<object, object> trigger, float timeMs)
    {
        var ctx = new ExecCtx<object>(new object(), null!, null!, null!, null!, null!, null!, null!, null!,
            new ExecPolicy(requireDeterministic: true, totalTimeMs: timeMs), new ExecutionControl());
        trigger.Execute(new object(), in ctx);
    }

    private sealed class RootFixture(ETriggerPlanExecutionStatus? status)
    {
        public bool Throws => !status.HasValue;
        public int Attempts { get; private set; }
        public InvalidOperationException Error { get; } = new("fixture root exception");

        public PlannedTrigger<object, object> Create(TriggerExecutionControlPlan executionControl = default, int interruptPriority = 0)
            => new(new TriggerPlan<object>(0, 0, interruptPriority: interruptPriority, executionControl: executionControl), ExecuteRoot);

        private TriggerPlanExecutionResult ExecuteRoot(in object args, in ExecCtx<object> ctx)
        {
            Attempts++;
            return status switch
            {
                ETriggerPlanExecutionStatus.Success => TriggerPlanExecutionResult.Success(),
                ETriggerPlanExecutionStatus.Skipped => TriggerPlanExecutionResult.Skipped("fixture skip"),
                ETriggerPlanExecutionStatus.Failed => TriggerPlanExecutionResult.Failed("fixture failure"),
                _ => throw Error,
            };
        }
    }

    private sealed class RecordingLifecycle : ITriggerLifecycle<object>
    {
        public List<int> Completed { get; } = new();
        public List<string> Failures { get; } = new();
        public (int Executed, int ShortCircuited) DispatchCounts { get; private set; }

        public void OnEventDispatched<TArgs>(EventKey<TArgs> key, in TArgs args, int executedCount, int shortCircuitedCount)
            => DispatchCounts = (executedCount, shortCircuitedCount);

        public void OnActionExecuted<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, bool wasInterrupted)
            => Completed.Add(priority);

        public void OnActionFailed<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, string errorMessage)
            => Failures.Add(errorMessage);

        public void OnRegistered<TArgs>(EventKey<TArgs> key, ITrigger<TArgs, object> trigger, int phase, int priority, long order) { }
        public void OnUnregistered<TArgs>(EventKey<TArgs> key, ITrigger<TArgs, object> trigger) { }
        public void OnEventDispatching<TArgs>(EventKey<TArgs> key, in TArgs args) { }
        public void OnBeforeEvaluate<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order) { }
        public void OnAfterEvaluate<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, bool result) { }
        public void OnBeforeExecute<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order) { }
        public void OnAfterExecute<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order) { }
        public void OnShortCircuit<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, ShortCircuitReason reason) { }
        public void OnScopeTransition(string fromScope, string toScope) { }
        public void OnConditionPassed<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int conditionId, string conditionName) { }
        public void OnConditionFailed<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int conditionId, string conditionName) { }
        public void OnActionExecuting<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions) { }
    }
}
