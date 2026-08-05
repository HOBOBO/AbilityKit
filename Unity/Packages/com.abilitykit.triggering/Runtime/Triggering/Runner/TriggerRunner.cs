using System;
using System.Collections.Generic;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Logging;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Variables.Numeric.Expression;
using AbilityKit.Triggering.Runtime.ActionScheduler;

namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// Trigger 运行时主线编排器。
    /// 负责事件订阅、触发器排序、条件评估、执行控制、生命周期通知与 ActionScheduler 推进。
    /// Dispatcher 目录中的调度器仅负责外部驱动方式适配，不承载 TriggerRunner 的主线执行语义。
    /// </summary>
    public sealed class TriggerRunner<TCtx>
    {
        private readonly IEventBus _eventBus;
        private readonly ITriggerContextSource<TCtx> _contextSource;
        private readonly ITriggerObserver<TCtx> _observer;
        private readonly ITriggerLifecycle<TCtx> _lifecycle;
        private readonly ITriggerTracer<TCtx> _tracer;

        private readonly TriggerRunnerRuntimeServices<TCtx> _runtimeServices;
        private readonly EInterruptPolicy _interruptPolicy;
        private readonly ActionSchedulerManager _actionSchedulerManager;

        private readonly Dictionary<Type, object> _triggerListsByArgsType = new Dictionary<Type, object>();
        private readonly Dictionary<Type, object> _subscriptionsByArgsType = new Dictionary<Type, object>();
        private TraceScope _currentTraceScope;
        private long _registrationOrder;

        public TriggerRunner(
            IEventBus eventBus,
            FunctionRegistry functions,
            ActionRegistry actions,
            ITriggerContextSource<TCtx> contextSource = null,
            ITriggerObserver<TCtx> observer = null,
            ITriggerLifecycle<TCtx> lifecycle = null,
            IBlackboardResolver blackboards = null,
            IPayloadAccessorRegistry payloads = null,
            IIdNameRegistry idNames = null,
            INumericVarDomainRegistry numericDomains = null,
            INumericRpnFunctionRegistry numericFunctions = null,
            ExecPolicy policy = default,
            EInterruptPolicy interruptPolicy = EInterruptPolicy.None,
            ActionSchedulerManager actionSchedulerManager = null,
            ITriggerTracer<TCtx> tracer = null)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            if (functions == null) throw new ArgumentNullException(nameof(functions));
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            _contextSource = contextSource;
            _observer = observer ?? NullTriggerObserver<TCtx>.Instance;
            _lifecycle = lifecycle ?? NullTriggerLifecycle<TCtx>.Instance;
            _tracer = tracer ?? NullTriggerTracer<TCtx>.Instance;
            _runtimeServices = new TriggerRunnerRuntimeServices<TCtx>(
                _eventBus,
                functions,
                actions,
                blackboards,
                payloads,
                idNames,
                numericDomains,
                numericFunctions,
                policy);
            _interruptPolicy = interruptPolicy;
            _actionSchedulerManager = actionSchedulerManager ?? new ActionSchedulerManager();
        }

        public IDisposable Register<TArgs>(EventKey<TArgs> key, ITrigger<TArgs, TCtx> trigger, int phase = 0, int priority = 0)
        {
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));

            var list = GetOrCreateTriggerList<TArgs>();
            if (!list.TryGetValue(key, out var triggers))
            {
                triggers = new List<TriggerRunnerEntry<TArgs, TCtx>>(4);
                list.Add(key, triggers);
            }

            if (triggers.Count == 0) EnsureSubscribed(key, list);

            var entry = new TriggerRunnerEntry<TArgs, TCtx>(phase, priority, _registrationOrder++, trigger);
            TriggerRunnerEntryList.InsertSorted(triggers, entry);
            _lifecycle.OnRegistered(key, trigger, phase, priority, entry.Order);
            var subscription = GetSubscription<TArgs>(key);
            return new TriggerRunnerRegistration<TArgs, TCtx>(triggers, entry, key, subscription, _lifecycle, TryUnsubscribe);
        }

        private Dictionary<EventKey<TArgs>, List<TriggerRunnerEntry<TArgs, TCtx>>> GetOrCreateTriggerList<TArgs>()
        {
            var type = typeof(TArgs);
            if (_triggerListsByArgsType.TryGetValue(type, out var obj)) return (Dictionary<EventKey<TArgs>, List<TriggerRunnerEntry<TArgs, TCtx>>>)obj;

            var dict = new Dictionary<EventKey<TArgs>, List<TriggerRunnerEntry<TArgs, TCtx>>>();
            _triggerListsByArgsType.Add(type, dict);
            return dict;
        }

        private void EnsureSubscribed<TArgs>(EventKey<TArgs> key, Dictionary<EventKey<TArgs>, List<TriggerRunnerEntry<TArgs, TCtx>>> list)
        {
            var type = typeof(TArgs);
            var dispatcher = new Dispatcher<TArgs>(this, key, list);

            if (_subscriptionsByArgsType.TryGetValue(type, out var obj))
            {
                var subs = (Dictionary<EventKey<TArgs>, IDisposable>)obj;
                if (subs.ContainsKey(key)) return;

                subs[key] = _eventBus.Subscribe(key, (args, control) => dispatcher.OnEvent(args, control));
                return;
            }

            var newSubs = new Dictionary<EventKey<TArgs>, IDisposable>();
            _subscriptionsByArgsType.Add(type, newSubs);

            newSubs[key] = _eventBus.Subscribe(key, (args, control) => dispatcher.OnEvent(args, control));
        }

        private IDisposable GetSubscription<TArgs>(EventKey<TArgs> key)
        {
            var type = typeof(TArgs);
            if (_subscriptionsByArgsType.TryGetValue(type, out var obj))
            {
                var subs = (Dictionary<EventKey<TArgs>, IDisposable>)obj;
                if (subs.TryGetValue(key, out var subscription))
                    return subscription;
            }
            return null;
        }

        private void TryUnsubscribe<TArgs>(EventKey<TArgs> key, IDisposable subscription)
        {
            var type = typeof(TArgs);
            if (_subscriptionsByArgsType.TryGetValue(type, out var obj))
            {
                var subs = (Dictionary<EventKey<TArgs>, IDisposable>)obj;
                subs.Remove(key);
                subscription?.Dispose();
            }
        }

        private void Dispatch<TArgs>(EventKey<TArgs> key, in TArgs args, ExecutionControl control, Dictionary<EventKey<TArgs>, List<TriggerRunnerEntry<TArgs, TCtx>>> list)
        {
            if (!list.TryGetValue(key, out var triggers) || triggers.Count == 0) return;

            _lifecycle.OnEventDispatching(key, in args);

            control = PrepareDispatchControl(control);
            var execCtx = CreateExecCtx(control);
            _currentTraceScope = _tracer.BeginTrace(key, in args);

            int executedCount = 0;
            int shortCircuitedCount = 0;

            try
            {
                for (int i = 0; i < triggers.Count; i++)
                {
                    var entry = triggers[i];

                    if (TryHandlePriorityBlock(key, in args, in entry, control, in execCtx))
                    {
                        shortCircuitedCount++;
                        continue;
                    }

                    var evaluation = EvaluateEntry(key, in args, in entry, control, in execCtx);
                    if (evaluation == DispatchEvaluationResult.FailedByException)
                    {
                        break;
                    }

                    if (evaluation == DispatchEvaluationResult.ConditionFailed)
                    {
                        shortCircuitedCount++;
                        if (HandleConditionRejected(key, in args, in entry, control, in execCtx))
                        {
                            break;
                        }

                        continue;
                    }

                    if (ExecuteEntry(key, in args, in entry, control, in execCtx, out var wasInterrupted))
                    {
                        executedCount++;
                    }

                    if (wasInterrupted)
                    {
                        shortCircuitedCount++;
                        break;
                    }
                }
            }
            finally
            {
                _tracer.EndTrace(_currentTraceScope);
                _currentTraceScope = default;
            }

            _lifecycle.OnEventDispatched(key, in args, executedCount, shortCircuitedCount);
        }

        private static int GetDebugTriggerId<TArgs>(ITrigger<TArgs, TCtx> trigger)
        {
            return trigger is ITriggerWithId withId ? withId.TriggerId : 0;
        }

        private static ExecutionControl PrepareDispatchControl(ExecutionControl control)
        {
            if (control == null) control = new ExecutionControl();
            control.Reset();
            return control;
        }

        private ExecCtx<TCtx> CreateExecCtx(ExecutionControl control)
        {
            var ctx = _contextSource != null ? _contextSource.GetContext() : default;
            return _runtimeServices.CreateExecCtx(ctx, control, _actionSchedulerManager);
        }

        /// <summary>
        /// 处理因更高优先级或失败条件传播导致的触发器跳过。
        /// </summary>
        private bool TryHandlePriorityBlock<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            in TriggerRunnerEntry<TArgs, TCtx> entry,
            ExecutionControl control,
            in ExecCtx<TCtx> execCtx)
        {
            if (!control.ShouldBlock(entry.Phase, entry.Priority))
            {
                return false;
            }

            var reason = control.InterruptConditionPassed
                ? ShortCircuitReason.InterruptedByHigherPriority
                : ShortCircuitReason.InterruptedByFailedCondition;

            NotifyShortCircuit(
                key,
                in args,
                in entry,
                control,
                in execCtx,
                reason,
                control.InterruptSourceName,
                control.InterruptTriggerId,
                control.InterruptConditionPassed,
                ShortCircuitCueKind.Skipped);
            return true;
        }

        /// <summary>
        /// 执行触发条件评估，并统一派发生命周期、观察者与 Cue 回调。
        /// </summary>
        private DispatchEvaluationResult EvaluateEntry<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            in TriggerRunnerEntry<TArgs, TCtx> entry,
            ExecutionControl control,
            in ExecCtx<TCtx> execCtx)
        {
            _lifecycle.OnBeforeEvaluate(key, in args, entry.Phase, entry.Priority, entry.Order);
            _observer.OnEvaluate(key, in args, entry.Phase, entry.Priority, entry.Order, false, in execCtx);

            bool ok;
            var startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                ok = entry.Trigger.Evaluate(in args, in execCtx);
            }
            catch (Exception ex)
            {
                RecordTrace(key, in entry, TriggerRecordKind.Evaluated, false, null, System.Diagnostics.Stopwatch.GetTimestamp() - startTicks);
                NotifyEvaluationException(key, in args, in entry, control, in execCtx, ex);
                return DispatchEvaluationResult.FailedByException;
            }

            var elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;
            _lifecycle.OnAfterEvaluate(key, in args, entry.Phase, entry.Priority, entry.Order, ok);
            _observer.OnEvaluate(key, in args, entry.Phase, entry.Priority, entry.Order, ok, in execCtx);
            RecordTrace(key, in entry, TriggerRecordKind.Evaluated, ok, null, elapsedTicks);

            if (!ok)
            {
                NotifyConditionFailed(key, in args, in entry, control, in execCtx);
                return DispatchEvaluationResult.ConditionFailed;
            }

            NotifyConditionPassed(key, in args, in entry, control, in execCtx);
            return DispatchEvaluationResult.ConditionPassed;
        }

        private void NotifyEvaluationException<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            in TriggerRunnerEntry<TArgs, TCtx> entry,
            ExecutionControl control,
            in ExecCtx<TCtx> execCtx,
            Exception ex)
        {
            Log.Error($"[TriggerRunner] Trigger evaluation exception. argsType={typeof(TArgs).Name} triggerId={GetDebugTriggerId(entry.Trigger)} triggerType={entry.Trigger.GetType().Name} phase={entry.Phase} priority={entry.Priority} order={entry.Order} exception={ex}");

            _lifecycle.OnConditionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name);
            _observer.OnConditionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, in execCtx);
            _lifecycle.OnActionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, "Evaluate", 0, 0, ex.Message);
            _observer.OnActionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, "Evaluate", 0, 0, ex.Message, in execCtx);

            var cue = entry.Trigger.Cue;
            if (ReferenceEquals(cue, NullTriggerCue.Instance))
            {
                return;
            }

            var failCtx = BuildCueContext(
                key,
                in args,
                entry.Phase,
                entry.Priority,
                entry.Order,
                entry.Trigger,
                ShortCircuitReason.ConditionFailed,
                null,
                0,
                false,
                control,
                Config.ECueLevel.Trigger,
                Config.ECueLifecycleStage.ConditionFailed);
            cue.OnConditionFailed(in failCtx);
        }

        private void NotifyConditionPassed<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            in TriggerRunnerEntry<TArgs, TCtx> entry,
            ExecutionControl control,
            in ExecCtx<TCtx> execCtx)
        {
            _lifecycle.OnConditionPassed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name);
            _observer.OnConditionPassed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, in execCtx);

            var cue = entry.Trigger.Cue;
            if (ReferenceEquals(cue, NullTriggerCue.Instance))
            {
                return;
            }

            var passCtx = BuildCueContext(
                key,
                in args,
                entry.Phase,
                entry.Priority,
                entry.Order,
                entry.Trigger,
                ShortCircuitReason.None,
                null,
                0,
                true,
                control,
                Config.ECueLevel.Trigger,
                Config.ECueLifecycleStage.ConditionPassed);
            cue.OnConditionPassed(in passCtx);
        }

        private void NotifyConditionFailed<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            in TriggerRunnerEntry<TArgs, TCtx> entry,
            ExecutionControl control,
            in ExecCtx<TCtx> execCtx)
        {
            _lifecycle.OnConditionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name);
            _observer.OnConditionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, in execCtx);

            var cue = entry.Trigger.Cue;
            if (ReferenceEquals(cue, NullTriggerCue.Instance))
            {
                return;
            }

            var failCtx = BuildCueContext(
                key,
                in args,
                entry.Phase,
                entry.Priority,
                entry.Order,
                entry.Trigger,
                ShortCircuitReason.ConditionFailed,
                null,
                0,
                false,
                control,
                Config.ECueLevel.Trigger,
                Config.ECueLifecycleStage.ConditionFailed);
            cue.OnConditionFailed(in failCtx);
        }

        /// <summary>
        /// 处理条件失败后的中断策略；返回 true 表示当前事件派发应立即结束。
        /// </summary>
        private bool HandleConditionRejected<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            in TriggerRunnerEntry<TArgs, TCtx> entry,
            ExecutionControl control,
            in ExecCtx<TCtx> execCtx)
        {
            if (_interruptPolicy != EInterruptPolicy.Strict)
            {
                return false;
            }

            NotifyShortCircuit(
                key,
                in args,
                in entry,
                control,
                in execCtx,
                ShortCircuitReason.ConditionFailed,
                null,
                0,
                false,
                ShortCircuitCueKind.Interrupted);
            return true;
        }

        /// <summary>
        /// 执行触发器动作；返回 true 表示动作完整执行并触发完成 Cue。
        /// </summary>
        private bool ExecuteEntry<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            in TriggerRunnerEntry<TArgs, TCtx> entry,
            ExecutionControl control,
            in ExecCtx<TCtx> execCtx,
            out bool wasInterrupted)
        {
            wasInterrupted = false;
            var cue = entry.Trigger.Cue;

            _lifecycle.OnBeforeExecute(key, in args, entry.Phase, entry.Priority, entry.Order);
            _lifecycle.OnActionExecuting(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, 0, 1);
            _observer.OnActionExecuting(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, 0, 1, in execCtx);
            if (!ReferenceEquals(cue, NullTriggerCue.Instance))
            {
                var executeCtx = BuildCueContext(
                    key,
                    in args,
                    entry.Phase,
                    entry.Priority,
                    entry.Order,
                    entry.Trigger,
                    ShortCircuitReason.None,
                    null,
                    0,
                    true,
                    control,
                    Config.ECueLevel.Trigger,
                    Config.ECueLifecycleStage.BeforeAction,
                    0);
                cue.OnBeforeAction(in executeCtx, 0);
            }

            var actionExecuted = TryExecuteTrigger(key, in args, in entry, in execCtx);
            if (TryHandleHardStop(key, in args, in entry, control, in execCtx))
            {
                wasInterrupted = true;
                _lifecycle.OnActionExecuted(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, 0, 1, true);
                _observer.OnActionExecuted(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, 0, 1, true, in execCtx);
                return false;
            }

            _lifecycle.OnAfterExecute(key, in args, entry.Phase, entry.Priority, entry.Order);
            _observer.OnExecute(key, in args, entry.Phase, entry.Priority, entry.Order, in execCtx);
            if (actionExecuted)
            {
                _lifecycle.OnActionExecuted(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, 0, 1, false);
                _observer.OnActionExecuted(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, 0, 1, false, in execCtx);
            }

            if (!actionExecuted)
            {
                return false;
            }

            if (!ReferenceEquals(cue, NullTriggerCue.Instance))
            {
                var executedCtx = BuildCueContext(
                    key,
                    in args,
                    entry.Phase,
                    entry.Priority,
                    entry.Order,
                    entry.Trigger,
                    ShortCircuitReason.None,
                    null,
                    0,
                    true,
                    control,
                    Config.ECueLevel.Trigger,
                    Config.ECueLifecycleStage.Executed);
                cue.OnExecuted(in executedCtx);
            }
            return true;
        }

        private bool TryExecuteTrigger<TArgs>(EventKey<TArgs> key, in TArgs args, in TriggerRunnerEntry<TArgs, TCtx> entry, in ExecCtx<TCtx> execCtx)
        {
            var startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                entry.Trigger.Execute(in args, in execCtx);
                RecordTrace(key, in entry, TriggerRecordKind.Executed, null, null, System.Diagnostics.Stopwatch.GetTimestamp() - startTicks);
                return true;
            }
            catch (Exception ex)
            {
                RecordTrace(key, in entry, TriggerRecordKind.Executed, null, null, System.Diagnostics.Stopwatch.GetTimestamp() - startTicks);
                NotifyActionFailed(key, in args, in entry, in execCtx, entry.Trigger.GetType().Name, 0, 1, ex.Message);
                return false;
            }
        }

        private bool TryHandleHardStop<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            in TriggerRunnerEntry<TArgs, TCtx> entry,
            ExecutionControl control,
            in ExecCtx<TCtx> execCtx)
        {
            if (!control.IsHardStopped)
            {
                return false;
            }

            var reason = control.Cancel ? ShortCircuitReason.Cancel : ShortCircuitReason.StopPropagation;
            NotifyShortCircuit(
                key,
                in args,
                in entry,
                control,
                in execCtx,
                reason,
                control.InterruptSourceName ?? entry.Trigger.GetType().Name,
                control.InterruptTriggerId,
                true,
                ShortCircuitCueKind.Interrupted);
            return true;
        }

        private void NotifyShortCircuit<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            in TriggerRunnerEntry<TArgs, TCtx> entry,
            ExecutionControl control,
            in ExecCtx<TCtx> execCtx,
            ShortCircuitReason reason,
            string interruptSourceName,
            int interruptTriggerId,
            bool interruptConditionPassed,
            ShortCircuitCueKind cueKind)
        {
            _lifecycle.OnShortCircuit(key, in args, entry.Phase, entry.Priority, entry.Order, reason);
            _observer.OnShortCircuit(key, in args, entry.Phase, entry.Priority, entry.Order, TriggerRunnerCueDispatcher.MapReason(reason), in execCtx);
            RecordTrace(key, in entry, TriggerRecordKind.ShortCircuited, null, reason, 0L);

            var cue = entry.Trigger.Cue;
            if (ReferenceEquals(cue, NullTriggerCue.Instance))
            {
                return;
            }

            var cueContext = TriggerRunnerCueDispatcher.BuildCueContext(
                key,
                in args,
                entry.Phase,
                entry.Priority,
                entry.Order,
                entry.Trigger,
                reason,
                interruptSourceName,
                interruptTriggerId,
                interruptConditionPassed,
                control,
                Config.ECueLevel.Trigger,
                cueKind == ShortCircuitCueKind.Skipped ? Config.ECueLifecycleStage.Skipped : Config.ECueLifecycleStage.Interrupted);
            switch (cueKind)
            {
                case ShortCircuitCueKind.Skipped:
                    cue.OnSkipped(in cueContext);
                    break;
                case ShortCircuitCueKind.Interrupted:
                    cue.OnInterrupted(in cueContext);
                    break;
            }
        }

        private static void DispatchShortCircuitCue<TArgs>(
            ITrigger<TArgs, TCtx> trigger,
            in TriggerCueContext cueContext,
            ShortCircuitCueKind cueKind)
        {
            TriggerRunnerCueDispatcher.DispatchShortCircuitCue(trigger, in cueContext, (TriggerRunnerShortCircuitCueKind)cueKind);
        }

        private void RecordTrace<TArgs>(
            EventKey<TArgs> key,
            in TriggerRunnerEntry<TArgs, TCtx> entry,
            TriggerRecordKind kind,
            bool? predicateResult,
            ShortCircuitReason? reason,
            long elapsedTicks)
        {
            var record = new TriggerTraceRecord(
                (int)entry.Order,
                entry.Phase,
                entry.Priority,
                entry.Order,
                kind,
                predicateResult,
                reason,
                System.Diagnostics.Stopwatch.GetTimestamp(),
                elapsedTicks,
                string.Empty);
            _tracer.RecordTrigger<TArgs>(_currentTraceScope, record);
        }

        private void NotifyActionFailed<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            in TriggerRunnerEntry<TArgs, TCtx> entry,
            in ExecCtx<TCtx> execCtx,
            string actionName,
            int actionIndex,
            int actionCount,
            string message)
        {
            _lifecycle.OnActionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, actionName, actionIndex, actionCount, message);
            _observer.OnActionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, actionName, actionIndex, actionCount, message, in execCtx);
        }

        private enum ShortCircuitCueKind
        {
            Skipped,
            Interrupted
        }

        private enum DispatchEvaluationResult
        {
            ConditionPassed,
            ConditionFailed,
            FailedByException
        }

        private TriggerCueContext BuildCueContext<TArgs>(
            EventKey<TArgs> key,
            in TArgs args,
            int phase,
            int priority,
            long order,
            ITrigger<TArgs, TCtx> trigger,
            ShortCircuitReason reason,
            string interruptSourceName,
            int interruptTriggerId,
            bool interruptConditionPassed,
            ExecutionControl control,
            Config.ECueLevel cueLevel = Config.ECueLevel.Trigger,
            Config.ECueLifecycleStage cueStage = Config.ECueLifecycleStage.None,
            int actionIndex = -1,
            in TriggerCueDescriptor cueDescriptor = default)
        {
            return TriggerRunnerCueDispatcher.BuildCueContext(
                key,
                in args,
                phase,
                priority,
                order,
                trigger,
                reason,
                interruptSourceName,
                interruptTriggerId,
                interruptConditionPassed,
                control,
                cueLevel,
                cueStage,
                actionIndex,
                in cueDescriptor);
        }

        private static ETriggerShortCircuitReason MapReason(ShortCircuitReason reason)
        {
            return TriggerRunnerCueDispatcher.MapReason(reason);
        }

        private sealed class Dispatcher<TArgs>
        {
            private readonly TriggerRunner<TCtx> _runner;
            private readonly EventKey<TArgs> _key;
            private readonly Dictionary<EventKey<TArgs>, List<TriggerRunnerEntry<TArgs, TCtx>>> _list;

            public Dispatcher(TriggerRunner<TCtx> runner, EventKey<TArgs> key, Dictionary<EventKey<TArgs>, List<TriggerRunnerEntry<TArgs, TCtx>>> list)
            {
                _runner = runner;
                _key = key;
                _list = list;
            }

            public void OnEvent(TArgs args, ExecutionControl control)
            {
                _runner.Dispatch(_key, in args, control, _list);
            }
        }

    }
}
