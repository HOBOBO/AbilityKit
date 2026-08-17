using System;
using System.Collections.Generic;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.ActionScheduler;
using AbilityKit.Triggering.Runtime.Context;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Dispatcher;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.RuleScheduler;
using AbilityKit.Triggering.Runtime.Schedule;
using AbilityKit.Triggering.Runtime.Schedule.Behavior;
using AbilityKit.Triggering.Runtime.Schedule.Data;
using AbilityKit.Triggering.Validation;
using NUnit.Framework;

namespace AbilityKit.Triggering.Tests
{
    public sealed class TriggerRunnerMainlineTests
    {
        private sealed class TestContext
        {
            public TestContext(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        private sealed class ObservingContext : System.IServiceProvider, ITriggerActionExecutionScopeObserver
        {
            public int EnterCount { get; private set; }
            public int ExitCount { get; private set; }
            public int LastActionIndex { get; private set; } = -1;
            public long LastActionId { get; private set; }
            public bool LastSucceeded { get; private set; }

            public object GetService(Type serviceType)
            {
                return serviceType == typeof(ITriggerActionExecutionScopeObserver) ? this : null;
            }

            public void EnterActionExecution(int actionIndex, long actionId)
            {
                EnterCount++;
                LastActionIndex = actionIndex;
                LastActionId = actionId;
            }

            public void ExitActionExecution(int actionIndex, long actionId, bool succeeded)
            {
                ExitCount++;
                LastActionIndex = actionIndex;
                LastActionId = actionId;
                LastSucceeded = succeeded;
            }
        }

        private sealed class ObservingContextSource : ITriggerContextSource<ObservingContext>
        {
            public ObservingContext Current { get; set; }

            public ObservingContext GetContext() => Current;
        }

        private sealed class MutableContextSource : ITriggerContextSource<TestContext>
        {
            public TestContext Current { get; set; }

            public TestContext GetContext() => Current;
        }

        private sealed class RecordingLifecycle<TCtx> : ITriggerLifecycle<TCtx>
        {
            private readonly List<long> _registeredOrders = new List<long>();

            public long[] RegisteredOrders => _registeredOrders.ToArray();

            public void OnRegistered<TArgs>(EventKey<TArgs> key, ITrigger<TArgs, TCtx> trigger, int phase, int priority, long order)
            {
                _registeredOrders.Add(order);
            }

            public void OnUnregistered<TArgs>(EventKey<TArgs> key, ITrigger<TArgs, TCtx> trigger)
            {
            }

            public void OnEventDispatching<TArgs>(EventKey<TArgs> key, in TArgs args)
            {
            }

            public void OnEventDispatched<TArgs>(EventKey<TArgs> key, in TArgs args, int executedCount, int shortCircuitedCount)
            {
            }

            public void OnBeforeEvaluate<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order)
            {
            }

            public void OnAfterEvaluate<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, bool result)
            {
            }

            public void OnBeforeExecute<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order)
            {
            }

            public void OnAfterExecute<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order)
            {
            }

            public void OnShortCircuit<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, ShortCircuitReason reason)
            {
            }

            public void OnScopeTransition(string fromScope, string toScope)
            {
            }

            public void OnConditionPassed<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int conditionId, string conditionName)
            {
            }

            public void OnConditionFailed<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int conditionId, string conditionName)
            {
            }

            public void OnActionExecuting<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions)
            {
            }

            public void OnActionExecuted<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, bool wasInterrupted)
            {
            }

            public void OnActionFailed<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, string errorMessage)
            {
            }
        }

        private sealed class Ping
        {
        }

        private sealed class TestDispatcherContext : ITriggerDispatcherContext
        {
            public TestDispatcherContext(TestContext context)
            {
                Context = context;
            }

            public object Context { get; }

            public TestContext Current => (TestContext)Context;

            public T GetService<T>() where T : class => Context as T;

            public float CurrentTimeMs => 0f;
        }

        [Test]
        public void Register_NotifiesLifecycleForEveryTriggerOnSameKey()
        {
            var bus = new EventBus();
            var lifecycle = new RecordingLifecycle<TestContext>();
            var runner = new TriggerRunner<TestContext>(
                bus,
                new FunctionRegistry(),
                new ActionRegistry(),
                lifecycle: lifecycle);
            var key = new EventKey<Ping>(StableStringId.Get("test:trigger_runner:registered_each_trigger"));

            runner.Register(key, new DelegateTrigger<Ping, TestContext>((evt, ctx) => true, (evt, ctx) => { }));
            runner.Register(key, new DelegateTrigger<Ping, TestContext>((evt, ctx) => true, (evt, ctx) => { }));

            Assert.That(lifecycle.RegisteredOrders, Is.EqualTo(new long[] { 0, 1 }));
        }

        [Test]
        public void Register_AfterLastRegistrationDisposed_ResubscribesToEventBus()
        {
            var bus = new EventBus();
            var runner = new TriggerRunner<TestContext>(
                bus,
                new FunctionRegistry(),
                new ActionRegistry());
            var key = new EventKey<Ping>(StableStringId.Get("test:trigger_runner:reregister_after_dispose"));
            var executionCount = 0;

            var first = runner.Register(
                key,
                new DelegateTrigger<Ping, TestContext>((evt, ctx) => true, (evt, ctx) => executionCount++));
            bus.Publish(key, new Ping());
            first.Dispose();
            bus.Publish(key, new Ping());

            runner.Register(
                key,
                new DelegateTrigger<Ping, TestContext>((evt, ctx) => true, (evt, ctx) => executionCount++));
            bus.Publish(key, new Ping());

            Assert.That(executionCount, Is.EqualTo(2));
        }

        [Test]
        public void PlannedTrigger_ImmediateNamedAction_PassesArgsAndContext()
        {
            var bus = new EventBus();
            var actions = new ActionRegistry();
            var contextSource = new MutableContextSource { Current = new TestContext(7) };
            var observedContextValues = new List<int>();
            NamedArgsDict observedArgs = null;
            var actionId = new ActionId(StableStringId.Get("test:trigger_runner:immediate_named_action"));

            actions.Register<NamedAction1<Ping, object, TestContext>>(
                actionId,
                (triggerArgs, actionArgs, ctx) =>
                {
                    observedContextValues.Add(ctx.Context.Value);
                    observedArgs = (NamedArgsDict)actionArgs;
                },
                isDeterministic: true);

            var runner = new TriggerRunner<TestContext>(
                bus,
                new FunctionRegistry(),
                actions,
                contextSource: contextSource);

            var key = new EventKey<Ping>(StableStringId.Get("test:trigger_runner:immediate_named_action_event"));
            var actionArgs = new Dictionary<string, ActionArgValue>
            {
                ["amount"] = ActionArgValue.OfConst(12, "amount")
            };
            var call = ActionCallPlan.WithArgs(actionId, actionArgs);
            var plan = new TriggerPlan<Ping>(phase: 0, priority: 0, triggerId: 1000, actions: new[] { call });

            runner.RegisterPlan<Ping, TestContext>(key, in plan);
            bus.Publish(key, new Ping());

            Assert.That(observedContextValues, Is.EqualTo(new[] { 7 }));
            Assert.That(observedArgs, Is.Not.Null);
            Assert.That(observedArgs.TryGetValue("amount", out var amount), Is.True);
            Assert.That(amount.Ref.ConstValue, Is.EqualTo(12));
        }

        [Test]
        public void PlannedTrigger_ActionScopeObserver_ExitsOnceAsSucceeded()
        {
            var bus = new EventBus();
            var actions = new ActionRegistry();
            var context = new ObservingContext();
            var contextSource = new ObservingContextSource { Current = context };
            var actionId = new ActionId(StableStringId.Get("test:trigger_runner:action_scope_success"));

            actions.Register<NamedAction0<Ping, object, ObservingContext>>(
                actionId,
                (triggerArgs, actionArgs, ctx) => { },
                isDeterministic: true);

            var runner = new TriggerRunner<ObservingContext>(
                bus,
                new FunctionRegistry(),
                actions,
                contextSource: contextSource);
            var key = new EventKey<Ping>(StableStringId.Get("test:trigger_runner:action_scope_success_event"));
            var plan = new TriggerPlan<Ping>(
                phase: 0,
                priority: 0,
                triggerId: 1004,
                actions: new[] { new ActionCallPlan(actionId) });

            runner.RegisterPlan<Ping, ObservingContext>(key, in plan);
            bus.Publish(key, new Ping());

            Assert.That(context.EnterCount, Is.EqualTo(1));
            Assert.That(context.ExitCount, Is.EqualTo(1));
            Assert.That(context.LastActionIndex, Is.EqualTo(0));
            Assert.That(context.LastActionId, Is.EqualTo(actionId.Value));
            Assert.That(context.LastSucceeded, Is.True);
        }

        [Test]
        public void PlannedTrigger_ActionScopeObserver_ExitsOnceAsFailedWhenActionThrows()
        {
            var bus = new EventBus();
            var actions = new ActionRegistry();
            var context = new ObservingContext();
            var contextSource = new ObservingContextSource { Current = context };
            var actionId = new ActionId(StableStringId.Get("test:trigger_runner:action_scope_failure"));

            actions.Register<NamedAction0<Ping, object, ObservingContext>>(
                actionId,
                (triggerArgs, actionArgs, ctx) => throw new InvalidOperationException("expected action failure"),
                isDeterministic: true);

            var runner = new TriggerRunner<ObservingContext>(
                bus,
                new FunctionRegistry(),
                actions,
                contextSource: contextSource);
            var key = new EventKey<Ping>(StableStringId.Get("test:trigger_runner:action_scope_failure_event"));
            var plan = new TriggerPlan<Ping>(
                phase: 0,
                priority: 0,
                triggerId: 1005,
                actions: new[] { new ActionCallPlan(actionId) });

            runner.RegisterPlan<Ping, ObservingContext>(key, in plan);

            var error = Assert.Throws<InvalidOperationException>(() => bus.Publish(key, new Ping()));
            Assert.That(error.Message, Is.EqualTo("expected action failure"));
            Assert.That(context.EnterCount, Is.EqualTo(1));
            Assert.That(context.ExitCount, Is.EqualTo(1));
            Assert.That(context.LastActionIndex, Is.EqualTo(0));
            Assert.That(context.LastActionId, Is.EqualTo(actionId.Value));
            Assert.That(context.LastSucceeded, Is.False);
        }

        [Test]
        public void PlannedTrigger_ImmediatePositionalAction_PassesResolvedArgs()
        {
            var bus = new EventBus();
            var actions = new ActionRegistry();
            var contextSource = new MutableContextSource { Current = new TestContext(5) };
            NamedArgsDict observedArgs = null;
            var actionId = new ActionId(StableStringId.Get("test:trigger_runner:immediate_positional_action"));

            actions.Register<NamedAction1<Ping, object, TestContext>>(
                actionId,
                (triggerArgs, actionArgs, ctx) => observedArgs = (NamedArgsDict)actionArgs,
                isDeterministic: true);

            var runner = new TriggerRunner<TestContext>(
                bus,
                new FunctionRegistry(),
                actions,
                contextSource: contextSource);

            var key = new EventKey<Ping>(StableStringId.Get("test:trigger_runner:immediate_positional_action_event"));
            var call = new ActionCallPlan(
                actionId,
                1,
                NumericValueRef.Const(12),
                default,
                null,
                EActionScheduleMode.Immediate,
                0f,
                -1,
                true,
                EActionExecutionPolicy.Immediate);
            var plan = new TriggerPlan<Ping>(phase: 0, priority: 0, triggerId: 1001, actions: new[] { call });

            runner.RegisterPlan<Ping, TestContext>(key, in plan);
            bus.Publish(key, new Ping());

            Assert.That(observedArgs, Is.Not.Null);
            Assert.That(observedArgs.TryGetValue("0", out var firstArg), Is.True);
            Assert.That(firstArg.Ref.ConstValue, Is.EqualTo(12));
        }

        [Test]
        public void ScheduledPlannedTrigger_DelayedAction_UsesLatestContextAtExecutionTime()
        {
            var bus = new EventBus();
            var actions = new ActionRegistry();
            var schedulerManager = new ActionSchedulerManager();
            var contextSource = new MutableContextSource { Current = new TestContext(1) };
            var observedContextValues = new List<int>();
            var actionId = new ActionId(StableStringId.Get("test:trigger_runner:scheduled_context_action"));

            actions.Register<NamedAction0<Ping, object, TestContext>>(
                actionId,
                (triggerArgs, actionArgs, ctx) => observedContextValues.Add(ctx.Context.Value),
                isDeterministic: true);

            var runner = new TriggerRunner<TestContext>(
                bus,
                new FunctionRegistry(),
                actions,
                contextSource: contextSource,
                actionSchedulerManager: schedulerManager);

            var key = new EventKey<Ping>(StableStringId.Get("test:trigger_runner:scheduled_context_event"));
            var call = new ActionCallPlan(
                actionId,
                0,
                default,
                default,
                null,
                EActionScheduleMode.Delayed,
                1f,
                1,
                true,
                EActionExecutionPolicy.Immediate);
            var plan = new TriggerPlan<Ping>(phase: 0, priority: 0, triggerId: 1001, actions: new[] { call });

            runner.RegisterPlan<Ping, TestContext>(key, in plan);

            contextSource.Current = new TestContext(1);
            bus.Publish(key, new Ping());

            contextSource.Current = new TestContext(2);
            bus.Publish(key, new Ping());

            schedulerManager.Update(1f, new TestDispatcherContext(contextSource.Current));

            Assert.That(observedContextValues, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void ScheduledPlannedTrigger_ImmediateAction_UsesLatestContextAtExecutionTime()
        {
            var bus = new EventBus();
            var actions = new ActionRegistry();
            var schedulerManager = new ActionSchedulerManager();
            var contextSource = new MutableContextSource { Current = new TestContext(1) };
            var observedContextValues = new List<int>();
            var actionId = new ActionId(StableStringId.Get("test:trigger_runner:immediate_latest_context"));

            actions.Register<NamedAction0<Ping, object, TestContext>>(
                actionId,
                (triggerArgs, actionArgs, ctx) => observedContextValues.Add(ctx.Context.Value),
                isDeterministic: true);

            var runner = new TriggerRunner<TestContext>(
                bus,
                new FunctionRegistry(),
                actions,
                contextSource: contextSource,
                actionSchedulerManager: schedulerManager);

            var key = new EventKey<Ping>(StableStringId.Get("test:trigger_runner:immediate_latest_context_event"));
            var call = new ActionCallPlan(
                actionId,
                0,
                default,
                default,
                null,
                EActionScheduleMode.Immediate,
                0f,
                -1,
                true,
                EActionExecutionPolicy.Immediate);
            var plan = new TriggerPlan<Ping>(phase: 0, priority: 0, triggerId: 1003, actions: new[] { call });

            runner.RegisterPlan<Ping, TestContext>(key, in plan);
            contextSource.Current = new TestContext(7);
            bus.Publish(key, new Ping());

            Assert.That(observedContextValues, Is.EqualTo(new[] { 7 }));
        }

        [Test]
        public void ScheduledPlannedTrigger_PeriodicAction_UsesScheduleSubPlanForIntervalsAndMaxExecutions()
        {
            var bus = new EventBus();
            var actions = new ActionRegistry();
            var schedulerManager = new ActionSchedulerManager();
            var contextSource = new MutableContextSource { Current = new TestContext(3) };
            var observedContextValues = new List<int>();
            var actionId = new ActionId(StableStringId.Get("test:trigger_runner:periodic_schedule_sub_plan"));

            actions.Register<NamedAction0<Ping, object, TestContext>>(
                actionId,
                (triggerArgs, actionArgs, ctx) => observedContextValues.Add(ctx.Context.Value),
                isDeterministic: true);

            var runner = new TriggerRunner<TestContext>(
                bus,
                new FunctionRegistry(),
                actions,
                contextSource: contextSource,
                actionSchedulerManager: schedulerManager);

            var key = new EventKey<Ping>(StableStringId.Get("test:trigger_runner:periodic_schedule_sub_plan_event"));
            var call = new ActionCallPlan(
                actionId,
                0,
                default,
                default,
                null,
                EActionScheduleMode.Periodic,
                10f,
                2,
                true,
                EActionExecutionPolicy.Immediate);
            var plan = new TriggerPlan<Ping>(phase: 0, priority: 0, triggerId: 1002, actions: new[] { call });

            runner.RegisterPlan<Ping, TestContext>(key, in plan);
            bus.Publish(key, new Ping());

            schedulerManager.Update(9f, new TestDispatcherContext(contextSource.Current));
            schedulerManager.Update(1f, new TestDispatcherContext(contextSource.Current));
            schedulerManager.Update(9f, new TestDispatcherContext(contextSource.Current));
            schedulerManager.Update(1f, new TestDispatcherContext(contextSource.Current));
            schedulerManager.Update(10f, new TestDispatcherContext(contextSource.Current));

            Assert.That(observedContextValues, Is.EqualTo(new[] { 3, 3 }));
        }
    }
}
