using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class ExecutionRootOutcomeBaselineTests
{
    private static readonly EventKey<object> Event = new(9180);

    [Theory]
    [InlineData("Direct")]
    [InlineData("TypedPlan")]
    [InlineData("DynamicPlan")]
    public void Registration_notification_failure_still_returns_the_active_handle(string entryPoint)
    {
        var registeredCalls = 0;
        var lifecycle = new RecordingLifecycle
        {
            Registered = () =>
            {
                registeredCalls++;
                throw new InvalidOperationException("测试注册通知异常");
            },
        };
        var bus = new EventBus();
        var runner = new TriggerRunner<object>(bus, new FunctionRegistry(), new ActionRegistry(), lifecycle: lifecycle);
        var plan = new TriggerPlan<object>(0, 10);
        using var registration = entryPoint switch
        {
            "TypedPlan" => runner.RegisterPlan(Event, in plan),
            "DynamicPlan" => runner.RegisterPlan(Event.IntId, typeof(object), in plan),
            _ => runner.Register(Event, new DelegateTrigger<object, object>(null!, (_, _) => { }), priority: 10),
        };

        Assert.True(bus.HasSubscribers(Event));
        bus.Publish(Event, new object());
        Assert.Equal(10, Assert.Single(lifecycle.Completed));
        Assert.Equal(1, registeredCalls);

        registration.Dispose();
        Assert.False(bus.HasSubscribers(Event));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unregistration_notification_failure_cleans_up_only_the_disposed_rule(bool keepAnotherRule)
    {
        var unregisteredCalls = 0;
        var lifecycle = new RecordingLifecycle
        {
            Unregistered = () =>
            {
                unregisteredCalls++;
                throw new InvalidOperationException("测试注销通知异常");
            },
        };
        var bus = new EventBus();
        var runner = new TriggerRunner<object>(bus, new FunctionRegistry(), new ActionRegistry(), lifecycle: lifecycle);
        var removed = new RootFixture(ETriggerPlanExecutionStatus.Success);
        var remaining = new RootFixture(ETriggerPlanExecutionStatus.Success);
        using var registration = runner.Register(Event, removed.Create());
        using var other = keepAnotherRule ? runner.Register(Event, remaining.Create()) : null;

        registration.Dispose();
        registration.Dispose();
        Assert.Equal(1, unregisteredCalls);
        Assert.Equal(keepAnotherRule, bus.HasSubscribers(Event));
        bus.Publish(Event, new object());
        Assert.Equal(0, removed.Attempts);
        Assert.Equal(keepAnotherRule ? 1 : 0, remaining.Attempts);

        other?.Dispose();
        Assert.False(bus.HasSubscribers(Event));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unregistration_notification_can_reregister_the_same_key_without_losing_its_subscription(bool throwAfterRegistering)
    {
        var replacement = new RootFixture(ETriggerPlanExecutionStatus.Success);
        var bus = new EventBus();
        TriggerRunner<object> runner = null!;
        IDisposable? replacementRegistration = null;
        var lifecycle = new RecordingLifecycle
        {
            Unregistered = () =>
            {
                if (replacementRegistration != null) return;
                replacementRegistration = runner.Register(Event, replacement.Create());
                if (throwAfterRegistering) throw new InvalidOperationException("测试重入注销通知异常");
            },
        };
        runner = new TriggerRunner<object>(bus, new FunctionRegistry(), new ActionRegistry(), lifecycle: lifecycle);
        var removed = new RootFixture(ETriggerPlanExecutionStatus.Success);
        using var registration = runner.Register(Event, removed.Create());

        registration.Dispose();
        using var current = Assert.IsAssignableFrom<IDisposable>(replacementRegistration);

        Assert.True(bus.HasSubscribers(Event));
        bus.Publish(Event, new object());
        Assert.Equal(0, removed.Attempts);
        Assert.Equal(1, replacement.Attempts);

        current.Dispose();
        Assert.False(bus.HasSubscribers(Event));
    }

    [Theory]
    [InlineData("Pass", 1, 0, 1)]
    [InlineData("Reject", 0, 1, 1)]
    [InlineData("Cancel", 0, 1, 0)]
    public void Dispatch_completion_failure_preserves_counts_and_event_propagation(
        string outcome, int executed, int shortCircuited, int expectedDownstreamCalls)
    {
        var tracer = new DispatchTracer();
        var completionCalls = 0;
        string? lastTraceAtCompletion = null;
        var lifecycle = new RecordingLifecycle
        {
            DispatchCompleted = () =>
            {
                completionCalls++;
                lastTraceAtCompletion = tracer.Calls.LastOrDefault();
                throw new InvalidOperationException("测试派发完成通知异常");
            },
        };
        var bus = new EventBus();
        var runner = new TriggerRunner<object>(bus, new FunctionRegistry(), new ActionRegistry(), lifecycle: lifecycle, tracer: tracer);
        var attempts = 0;
        var trigger = new DelegateTrigger<object, object>((_, _) => outcome != "Reject", (_, ctx) =>
        {
            attempts++;
            if (outcome == "Cancel") ctx.Control.Cancel = true;
        });
        using var registration = runner.Register(Event, trigger);
        var downstreamCalls = 0;
        using var subscriber = bus.Subscribe(Event, _ => downstreamCalls++);
        var control = new ExecutionControl();

        bus.Publish(Event, new object(), control);

        Assert.Equal("End:1", lastTraceAtCompletion);
        Assert.Equal(1, completionCalls);
        Assert.Equal(outcome == "Reject" ? 0 : 1, attempts);
        Assert.Equal((executed, shortCircuited), lifecycle.DispatchCounts);
        Assert.Equal(outcome == "Cancel", control.IsHardStopped);
        Assert.Equal(expectedDownstreamCalls, downstreamCalls);
    }

    [Theory]
    [InlineData("Lifecycle")]
    [InlineData("Observer")]
    [InlineData("Cue")]
    public void Pre_execution_hooks_keep_their_exception_behavior(string failingHook)
    {
        var originalFailure = new InvalidOperationException("测试执行前回调异常");
        var completionCalls = 0;
        var lifecycle = new RecordingLifecycle
        {
            BeforeEvaluate = failingHook == "Lifecycle" ? () => throw originalFailure : null,
            DispatchCompleted = () => completionCalls++,
        };
        var hooks = new DispatchHooks
        {
            ActionExecuting = failingHook == "Observer" ? () => throw originalFailure : null,
            BeforeAction = failingHook == "Cue" ? () => throw originalFailure : null,
        };
        var bus = new EventBus();
        var runner = new TriggerRunner<object>(bus, new FunctionRegistry(), new ActionRegistry(), lifecycle: lifecycle, observer: hooks);
        var attempts = 0;
        var trigger = new PlannedTrigger<object, object>(new TriggerPlan<object>(0, 0, cue: hooks), (in object args, in ExecCtx<object> ctx) =>
        {
            attempts++;
            return TriggerPlanExecutionResult.Success();
        });
        using var registration = runner.Register(Event, trigger);

        Assert.Same(originalFailure, Assert.Throws<InvalidOperationException>(() => bus.Publish(Event, new object())));
        Assert.Equal(0, attempts);
        Assert.Equal(0, completionCalls);
    }

    [Theory]
    [InlineData("Begin:1")]
    [InlineData("Evaluated:1")]
    [InlineData("End:1")]
    public void Trace_callback_failure_does_not_block_dispatch_or_its_completion(string failingCall)
    {
        var tracer = new DispatchTracer(failingCall);
        var lifecycle = new RecordingLifecycle();
        var bus = new EventBus();
        var runner = new TriggerRunner<object>(bus, new FunctionRegistry(), new ActionRegistry(), lifecycle: lifecycle, tracer: tracer);
        var fixture = new RootFixture(ETriggerPlanExecutionStatus.Success);
        using var registration = runner.Register(Event, fixture.Create(), priority: 10);
        var downstreamCalls = 0;
        using var subscriber = bus.Subscribe(Event, _ => downstreamCalls++);

        bus.Publish(Event, new object());

        Assert.Equal(failingCall == "Begin:1" ? new[] { "Begin:1" } : new[] { "Begin:1", "Evaluated:1", "Executed:1", "End:1" }, tracer.Calls);
        Assert.Equal(1, fixture.Attempts);
        Assert.Equal((1, 0), lifecycle.DispatchCounts);
        Assert.Equal(1, downstreamCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Begin:2")]
    [InlineData("End:2")]
    public void Nested_dispatch_keeps_each_trace_scope_even_if_the_inner_tracer_throws(string? failingCall)
    {
        var tracer = new DispatchTracer(failingCall);
        var bus = new EventBus();
        var runner = new TriggerRunner<object>(bus, new FunctionRegistry(), new ActionRegistry(), tracer: tracer);
        var innerEvent = new EventKey<int>(9181);
        using var outer = runner.Register(Event, new DelegateTrigger<object, object>(null!, (_, _) => bus.Publish(innerEvent, 42)), priority: 10);
        var innerCalls = 0;
        using var inner = runner.Register(innerEvent, new DelegateTrigger<int, object>(null!, (_, _) => innerCalls++), priority: 10);
        var subsequent = new RootFixture(ETriggerPlanExecutionStatus.Success);
        using var second = runner.Register(Event, subsequent.Create(), priority: 9);

        bus.Publish(Event, new object());

        var expected = new List<string> { "Begin:1", "Evaluated:1", "Begin:2" };
        if (failingCall != "Begin:2") expected.AddRange(new[] { "Evaluated:2", "Executed:2", "End:2" });
        expected.AddRange(new[] { "Executed:1", "Evaluated:1", "Executed:1", "End:1" });
        Assert.Equal(expected, tracer.Calls);
        Assert.Equal(1, innerCalls);
        Assert.Equal(1, subsequent.Attempts);
    }

    [Fact]
    public void A_default_scope_returned_by_the_tracer_still_receives_records_and_end()
    {
        var tracer = new DispatchTracer(useDefaultScope: true);
        var bus = new EventBus();
        var runner = new TriggerRunner<object>(bus, new FunctionRegistry(), new ActionRegistry(), tracer: tracer);
        var fixture = new RootFixture(ETriggerPlanExecutionStatus.Success);
        using var registration = runner.Register(Event, fixture.Create());

        bus.Publish(Event, new object());

        Assert.Equal(new[] { "Begin:1", "Evaluated:0", "Executed:0", "End:0" }, tracer.Calls);
    }

    [Fact]
    public void Trace_end_failure_does_not_replace_an_unhandled_dispatch_exception()
    {
        var originalFailure = new InvalidOperationException("原始派发异常");
        var tracer = new DispatchTracer("End:1");
        var lifecycle = new RecordingLifecycle { BeforeEvaluate = () => throw originalFailure };
        var bus = new EventBus();
        var runner = new TriggerRunner<object>(bus, new FunctionRegistry(), new ActionRegistry(), lifecycle: lifecycle, tracer: tracer);
        var fixture = new RootFixture(ETriggerPlanExecutionStatus.Success);
        using var registration = runner.Register(Event, fixture.Create());

        Assert.Same(originalFailure, Assert.Throws<InvalidOperationException>(() => bus.Publish(Event, new object())));
        Assert.Equal(new[] { "Begin:1", "End:1" }, tracer.Calls);
    }

    [Theory]
    [InlineData("Lifecycle", false)]
    [InlineData("Observer", false)]
    [InlineData("Trace", false)]
    [InlineData("Cue", false)]
    [InlineData("Lifecycle", true)]
    [InlineData("Observer", true)]
    [InlineData("Trace", true)]
    [InlineData("Cue", true)]
    public void Short_circuit_notification_failure_does_not_change_dispatch_decision(string failingHook, bool hardStop)
    {
        var bus = new EventBus();
        var hooks = new DispatchHooks(failingHook);
        var lifecycle = new RecordingLifecycle { ShortCircuit = hooks.Visit };
        var runner = new TriggerRunner<object>(bus, new FunctionRegistry(), new ActionRegistry(),
            lifecycle: lifecycle, observer: hooks, tracer: hooks);
        var blockedAttempts = 0;
        var unrelatedAttempts = 0;
        var stopper = new PlannedTrigger<object, object>(
            new TriggerPlan<object>(0, 10, interruptPriority: hardStop ? 0 : 10, cue: hardStop ? hooks : null!),
            (in object args, in ExecCtx<object> ctx) =>
            {
                if (hardStop) ctx.Control.Cancel = true;
                return TriggerPlanExecutionResult.Success();
            });
        var blocked = new PlannedTrigger<object, object>(
            new TriggerPlan<object>(0, 9, cue: hooks), (in object args, in ExecCtx<object> ctx) =>
            {
                blockedAttempts++;
                return TriggerPlanExecutionResult.Success();
            });
        var unrelated = new PlannedTrigger<object, object>(
            new TriggerPlan<object>(1, 10), (in object args, in ExecCtx<object> ctx) =>
            {
                unrelatedAttempts++;
                return TriggerPlanExecutionResult.Success();
            });
        using var first = runner.Register(Event, stopper, phase: 0, priority: 10);
        using var second = runner.Register(Event, blocked, phase: 0, priority: 9);
        using var third = runner.Register(Event, unrelated, phase: 1, priority: 10);
        var control = new ExecutionControl();

        bus.Publish(Event, new object(), control);

        Assert.Equal(new[] { "Lifecycle", "Observer", "Trace", hardStop ? "Interrupted" : "Skipped" }, hooks.Calls);
        Assert.Equal(0, blockedAttempts);
        Assert.Equal(hardStop ? 0 : 1, unrelatedAttempts);
        Assert.Equal(hardStop, control.IsHardStopped);
        Assert.Equal(hardStop ? (0, 1) : (2, 1), lifecycle.DispatchCounts);
    }

    [Theory]
    [InlineData("Lifecycle", false)]
    [InlineData("Observer", false)]
    [InlineData("Trace", false)]
    [InlineData("Lifecycle", true)]
    [InlineData("Observer", true)]
    [InlineData("Trace", true)]
    public void Failing_action_diagnostics_preserve_dispatch_and_original_failure(string failingHook, bool hardStop)
    {
        var bus = new EventBus();
        var originalFailure = new InvalidOperationException("测试动作执行失败");
        var diagnosticsFailure = new InvalidOperationException("测试失败诊断异常");
        var diagnostics = new List<string>();
        void Record(string hook)
        {
            diagnostics.Add(hook);
            if (hook == failingHook) throw diagnosticsFailure;
        }

        var hooks = new DispatchHooks
        {
            ExecutionTrace = priority => { if (priority == 10) Record("Trace"); },
            ActionFailure = () => Record("Observer"),
        };
        var lifecycle = new RecordingLifecycle
        {
            ShortCircuit = hooks.Visit,
            ActionFailure = () => Record("Lifecycle"),
        };
        var runner = new TriggerRunner<object>(bus, new FunctionRegistry(), new ActionRegistry(),
            lifecycle: lifecycle, observer: hooks, tracer: hooks);
        var failing = new PlannedTrigger<object, object>(
            new TriggerPlan<object>(0, 10, cue: hooks), (in object args, in ExecCtx<object> ctx) =>
            {
                if (hardStop) ctx.Control.Cancel = true;
                throw originalFailure;
            });
        var subsequent = new RootFixture(ETriggerPlanExecutionStatus.Success);
        using var first = runner.Register(Event, failing, priority: 10);
        using var second = runner.Register(Event, subsequent.Create(), priority: 9);
        var downstreamCalls = 0;
        using var subscriber = bus.Subscribe(Event, _ => downstreamCalls++);
        var control = new ExecutionControl();

        bus.Publish(Event, new object(), control);

        Assert.Equal(new[] { "Trace", "Lifecycle", "Observer" }, diagnostics);
        Assert.Equal(originalFailure.Message, Assert.Single(lifecycle.Failures));
        Assert.Equal(originalFailure.Message, Assert.Single(hooks.Failures));
        Assert.Equal(hardStop ? new[] { "Lifecycle", "Observer", "Trace", "Interrupted" } : Array.Empty<string>(), hooks.Calls);
        Assert.Equal(hardStop, control.IsHardStopped);
        Assert.Equal(hardStop ? (0, 1) : (1, 0), lifecycle.DispatchCounts);
        Assert.Equal(hardStop ? 0 : 1, subsequent.Attempts);
        Assert.Equal(hardStop ? 0 : 1, downstreamCalls);
    }

    [Fact]
    public void Failing_execution_trace_does_not_reclassify_success_or_retry_the_trace()
    {
        var traceCalls = 0;
        var hooks = new DispatchHooks
        {
            ExecutionTrace = _ =>
            {
                traceCalls++;
                throw new InvalidOperationException("测试执行记录异常");
            },
        };
        var bus = new EventBus();
        var lifecycle = new RecordingLifecycle();
        var runner = new TriggerRunner<object>(bus, new FunctionRegistry(), new ActionRegistry(), lifecycle: lifecycle, tracer: hooks);
        var fixture = new RootFixture(ETriggerPlanExecutionStatus.Success);
        using var registration = runner.Register(Event, fixture.Create(), priority: 10);

        bus.Publish(Event, new object());

        Assert.Equal(1, fixture.Attempts);
        Assert.Equal(1, traceCalls);
        Assert.Empty(lifecycle.Failures);
        Assert.Equal(10, Assert.Single(lifecycle.Completed));
        Assert.Equal((1, 0), lifecycle.DispatchCounts);
    }

    [Theory]
    [InlineData(EInterruptPolicy.None, "Trace")]
    [InlineData(EInterruptPolicy.None, "Lifecycle.Condition")]
    [InlineData(EInterruptPolicy.None, "Observer.Condition")]
    [InlineData(EInterruptPolicy.None, "Lifecycle.Action")]
    [InlineData(EInterruptPolicy.None, "Observer.Action")]
    [InlineData(EInterruptPolicy.None, "CueContext")]
    [InlineData(EInterruptPolicy.None, "Cue")]
    [InlineData(EInterruptPolicy.Strict, "Trace")]
    [InlineData(EInterruptPolicy.Strict, "Lifecycle.Condition")]
    [InlineData(EInterruptPolicy.Strict, "Observer.Condition")]
    [InlineData(EInterruptPolicy.Strict, "Lifecycle.Action")]
    [InlineData(EInterruptPolicy.Strict, "Observer.Action")]
    [InlineData(EInterruptPolicy.Strict, "CueContext")]
    [InlineData(EInterruptPolicy.Strict, "Cue")]
    public void Failing_evaluation_diagnostics_preserve_runner_abort_and_original_failure(EInterruptPolicy policy, string failingHook)
    {
        var originalFailure = new InvalidOperationException("原始条件求值异常");
        var diagnostics = new List<string>();
        var evaluations = new List<TriggerTraceRecord>();
        void Record(string hook)
        {
            diagnostics.Add(hook);
            if (hook == failingHook)
                throw new InvalidOperationException($"测试条件求值诊断异常：{hook}");
        }

        var hooks = new DispatchHooks
        {
            EvaluationTrace = record =>
            {
                evaluations.Add(record);
                Record("Trace");
            },
            ConditionFailure = () => Record("Observer.Condition"),
            ActionFailure = () => Record("Observer.Action"),
            CueFailure = () => Record("Cue"),
        };
        var lifecycle = new RecordingLifecycle
        {
            ConditionFailure = () => Record("Lifecycle.Condition"),
            ActionFailure = () => Record("Lifecycle.Action"),
            ShortCircuit = hooks.Visit,
        };
        var bus = new EventBus();
        var runner = new TriggerRunner<object>(bus, new FunctionRegistry(), new ActionRegistry(),
            lifecycle: lifecycle, observer: hooks, tracer: hooks, interruptPolicy: policy);
        var failing = new ThrowingEvaluationTrigger(originalFailure, hooks);
        var subsequent = new RootFixture(ETriggerPlanExecutionStatus.Success);
        using var first = runner.Register(Event, failing, priority: 10);
        using var second = runner.Register(Event, subsequent.Create(), priority: 9);
        var downstreamCalls = 0;
        using var subscriber = bus.Subscribe(Event, _ => downstreamCalls++);
        var control = new ExecutionControl();

        bus.Publish(Event, new EvaluationCueArgs(() => Record("CueContext")), control);

        var expected = new List<string> { "Trace", "Lifecycle.Condition", "Observer.Condition", "Lifecycle.Action", "Observer.Action", "CueContext" };
        if (failingHook != "CueContext") expected.Add("Cue");
        Assert.Equal(expected, diagnostics);
        Assert.False(Assert.Single(evaluations).PredicateResult);
        Assert.Equal(originalFailure.Message, Assert.Single(lifecycle.Failures));
        Assert.Equal(originalFailure.Message, Assert.Single(hooks.Failures));
        Assert.Empty(hooks.Calls);
        Assert.Empty(lifecycle.Completed);
        Assert.Equal((0, 0), lifecycle.DispatchCounts);
        Assert.Equal(0, failing.Executions);
        Assert.Equal(0, subsequent.Attempts);
        Assert.False(control.IsHardStopped);
        Assert.Equal(1, downstreamCalls);
    }

    [Theory]
    [InlineData(EInterruptPolicy.None, 1)]
    [InlineData(EInterruptPolicy.Strict, 0)]
    public void Returning_false_uses_the_existing_condition_rejection_policy(EInterruptPolicy policy, int expectedExecutions)
    {
        var lifecycle = new RecordingLifecycle();
        var bus = new EventBus();
        var runner = new TriggerRunner<object>(bus, new FunctionRegistry(), new ActionRegistry(), lifecycle: lifecycle, interruptPolicy: policy);
        var rejected = new DelegateTrigger<object, object>((_, _) => false, (_, _) => throw new InvalidOperationException("条件拒绝后不应执行动作"));
        var subsequent = new RootFixture(ETriggerPlanExecutionStatus.Success);
        using var first = runner.Register(Event, rejected, priority: 10);
        using var second = runner.Register(Event, subsequent.Create(), priority: 9);

        bus.Publish(Event, new object());

        Assert.Empty(lifecycle.Failures);
        Assert.Equal(expectedExecutions, subsequent.Attempts);
        Assert.Equal((expectedExecutions, 1), lifecycle.DispatchCounts);
    }

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

    private sealed class ThrowingEvaluationTrigger(Exception failure, ITriggerCue cue) : ITrigger<object, object>
    {
        public ITriggerCue Cue => cue;
        public int Executions { get; private set; }
        public bool Evaluate(in object args, in ExecCtx<object> ctx) => throw failure;
        public void Execute(in object args, in ExecCtx<object> ctx) => Executions++;
    }

    private sealed class EvaluationCueArgs(Action onBuildContext) : ITriggerCueDataProvider
    {
        public bool TryGetCueData(ECueLevel cueLevel, ECueLifecycleStage cueStage, int actionIndex,
            in TriggerCueDescriptor cueDescriptor, out object cueData, out string cuePayload)
        {
            onBuildContext();
            cueData = null!;
            cuePayload = null!;
            return false;
        }
    }

    private sealed class DispatchTracer(string? failingCall = null, bool useDefaultScope = false) : ITriggerTracer<object>
    {
        private long _nextScopeId;
        public List<string> Calls { get; } = new();

        public TraceScope BeginTrace<TArgs>(EventKey<TArgs> key, in TArgs args)
        {
            var scopeId = ++_nextScopeId;
            Record($"Begin:{scopeId}");
            return useDefaultScope ? default : new TraceScope(scopeId, 0, string.Empty, key.IntId);
        }

        public void RecordTrigger<TArgs>(TraceScope scope, TriggerTraceRecord record) => Record($"{record.Kind}:{scope.ScopeId}");
        public void EndTrace(TraceScope scope) => Record($"End:{scope.ScopeId}");

        private void Record(string call)
        {
            Calls.Add(call);
            if (call == failingCall) throw new InvalidOperationException($"测试 Trace 异常：{call}");
        }
    }

    private sealed class RecordingLifecycle : ITriggerLifecycle<object>
    {
        public List<int> Completed { get; } = new();
        public List<string> Failures { get; } = new();
        public (int Executed, int ShortCircuited) DispatchCounts { get; private set; }
        public Action<string>? ShortCircuit { get; init; }
        public Action? BeforeEvaluate { get; init; }
        public Action? Registered { get; init; }
        public Action? Unregistered { get; init; }
        public Action? DispatchCompleted { get; init; }
        public Action? ConditionFailure { get; init; }
        public Action? ActionFailure { get; init; }

        public void OnEventDispatched<TArgs>(EventKey<TArgs> key, in TArgs args, int executedCount, int shortCircuitedCount)
        {
            DispatchCounts = (executedCount, shortCircuitedCount);
            DispatchCompleted?.Invoke();
        }

        public void OnActionExecuted<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, bool wasInterrupted)
            => Completed.Add(priority);

        public void OnActionFailed<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, string errorMessage)
        {
            Failures.Add(errorMessage);
            ActionFailure?.Invoke();
        }

        public void OnRegistered<TArgs>(EventKey<TArgs> key, ITrigger<TArgs, object> trigger, int phase, int priority, long order)
            => Registered?.Invoke();
        public void OnUnregistered<TArgs>(EventKey<TArgs> key, ITrigger<TArgs, object> trigger)
            => Unregistered?.Invoke();
        public void OnEventDispatching<TArgs>(EventKey<TArgs> key, in TArgs args) { }
        public void OnBeforeEvaluate<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order)
            => BeforeEvaluate?.Invoke();
        public void OnAfterEvaluate<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, bool result) { }
        public void OnBeforeExecute<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order) { }
        public void OnAfterExecute<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order) { }
        public void OnShortCircuit<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, ShortCircuitReason reason)
            => ShortCircuit?.Invoke("Lifecycle");
        public void OnScopeTransition(string fromScope, string toScope) { }
        public void OnConditionPassed<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int conditionId, string conditionName) { }
        public void OnConditionFailed<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int conditionId, string conditionName)
            => ConditionFailure?.Invoke();
        public void OnActionExecuting<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions) { }
    }

    private sealed class DispatchHooks(string? failingHook = null) : ITriggerObserver<object>, ITriggerTracer<object>, ITriggerCue
    {
        public List<string> Calls { get; } = new();
        public List<string> Failures { get; } = new();
        public Action? ActionExecuting { get; init; }
        public Action? BeforeAction { get; init; }
        public Action? ConditionFailure { get; init; }
        public Action? ActionFailure { get; init; }
        public Action? CueFailure { get; init; }
        public Action<TriggerTraceRecord>? EvaluationTrace { get; init; }
        public Action<int>? ExecutionTrace { get; init; }

        public void Visit(string hook)
        {
            Calls.Add(hook);
            if (hook == failingHook || (failingHook == "Cue" && (hook == "Interrupted" || hook == "Skipped")))
                throw new InvalidOperationException($"测试短路通知异常：{hook}");
        }

        public void OnShortCircuit<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, ETriggerShortCircuitReason reason, in ExecCtx<object> ctx) => Visit("Observer");
        public void RecordTrigger<TArgs>(TraceScope scope, TriggerTraceRecord record)
        {
            if (record.Kind == TriggerRecordKind.ShortCircuited) Visit("Trace");
            if (record.Kind == TriggerRecordKind.Evaluated) EvaluationTrace?.Invoke(record);
            if (record.Kind == TriggerRecordKind.Executed) ExecutionTrace?.Invoke(record.Priority);
        }
        public void OnInterrupted(in TriggerCueContext context) => Visit("Interrupted");
        public void OnSkipped(in TriggerCueContext context) => Visit("Skipped");
        public void OnConditionPassed(in TriggerCueContext context) { }
        public void OnConditionFailed(in TriggerCueContext context) => CueFailure?.Invoke();
        public void OnBeforeAction(in TriggerCueContext context, int actionIndex) => BeforeAction?.Invoke();
        public void OnExecuted(in TriggerCueContext context) { }
        public TraceScope BeginTrace<TArgs>(EventKey<TArgs> key, in TArgs args) => default;
        public void EndTrace(TraceScope scope) { }
        public void OnEvaluate<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, bool passed, in ExecCtx<object> ctx) { }
        public void OnExecute<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, in ExecCtx<object> ctx) { }
        public void OnConditionPassed<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int conditionId, string conditionName, in ExecCtx<object> ctx) { }
        public void OnConditionFailed<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int conditionId, string conditionName, in ExecCtx<object> ctx)
            => ConditionFailure?.Invoke();
        public void OnActionExecuting<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, in ExecCtx<object> ctx)
            => ActionExecuting?.Invoke();
        public void OnActionExecuted<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, bool wasInterrupted, in ExecCtx<object> ctx) { }
        public void OnActionFailed<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, string errorMessage, in ExecCtx<object> ctx)
        {
            Failures.Add(errorMessage);
            ActionFailure?.Invoke();
        }
    }
}
