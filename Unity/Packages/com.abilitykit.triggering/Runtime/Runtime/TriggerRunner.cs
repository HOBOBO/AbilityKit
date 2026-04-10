using System;
using System.Collections.Generic;
using AbilityKit.Core.Common.Event;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Variables.Numeric.Expression;

namespace AbilityKit.Triggering.Runtime
{
    public sealed class TriggerRunner<TCtx>
    {
        private readonly IEventBus _eventBus;
        private readonly ITriggerContextSource<TCtx> _contextSource;
        private readonly ITriggerObserver<TCtx> _observer;
        private readonly ITriggerLifecycle<TCtx> _lifecycle;

        private readonly FunctionRegistry _functions;
        private readonly ActionRegistry _actions;
        private readonly IBlackboardResolver _blackboards;
        private readonly IPayloadAccessorRegistry _payloads;
        private readonly IIdNameRegistry _idNames;
        private readonly INumericVarDomainRegistry _numericDomains;
        private readonly INumericRpnFunctionRegistry _numericFunctions;
        private readonly ExecPolicy _policy;
        private readonly EInterruptPolicy _interruptPolicy;

        private readonly Dictionary<Type, object> _triggerListsByArgsType = new Dictionary<Type, object>();
        private readonly Dictionary<Type, object> _subscriptionsByArgsType = new Dictionary<Type, object>();
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
            EInterruptPolicy interruptPolicy = EInterruptPolicy.None)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _functions = functions ?? throw new ArgumentNullException(nameof(functions));
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
            _contextSource = contextSource;
            _observer = observer ?? NullTriggerObserver<TCtx>.Instance;
            _lifecycle = lifecycle ?? NullTriggerLifecycle<TCtx>.Instance;
            _blackboards = blackboards;
            _payloads = payloads;
            _idNames = idNames;
            _numericDomains = numericDomains;
            _numericFunctions = numericFunctions;
            _policy = policy;
            _interruptPolicy = interruptPolicy;
        }

        public IDisposable Register<TArgs>(EventKey<TArgs> key, ITrigger<TArgs, TCtx> trigger, int phase = 0, int priority = 0)
        {
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));

            var list = GetOrCreateTriggerList<TArgs>();
            if (!list.TryGetValue(key, out var triggers))
            {
                triggers = new List<Entry<TArgs>>(4);
                list.Add(key, triggers);
                EnsureSubscribed(key, list);
                _lifecycle.OnRegistered(key, trigger, phase, priority, _registrationOrder);
            }

            var entry = new Entry<TArgs>(phase, priority, _registrationOrder++, trigger);
            InsertSorted(triggers, entry);
            var subscription = GetSubscription<TArgs>(key);
            return new Registration<TArgs>(triggers, entry, this, key, subscription);
        }

        private Dictionary<EventKey<TArgs>, List<Entry<TArgs>>> GetOrCreateTriggerList<TArgs>()
        {
            var type = typeof(TArgs);
            if (_triggerListsByArgsType.TryGetValue(type, out var obj)) return (Dictionary<EventKey<TArgs>, List<Entry<TArgs>>>)obj;

            var dict = new Dictionary<EventKey<TArgs>, List<Entry<TArgs>>>();
            _triggerListsByArgsType.Add(type, dict);
            return dict;
        }

        private void EnsureSubscribed<TArgs>(EventKey<TArgs> key, Dictionary<EventKey<TArgs>, List<Entry<TArgs>>> list)
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

        private void Dispatch<TArgs>(EventKey<TArgs> key, in TArgs args, ExecutionControl control, Dictionary<EventKey<TArgs>, List<Entry<TArgs>>> list)
        {
            if (!list.TryGetValue(key, out var triggers) || triggers.Count == 0) return;

            _lifecycle.OnEventDispatching(key, in args);

            var ctx = _contextSource != null ? _contextSource.GetContext() : default;
            if (control == null) control = new ExecutionControl();
            control.Reset();
            var execCtx = new ExecCtx<TCtx>(ctx, _eventBus, _functions, _actions, _blackboards, _payloads, _idNames, _numericDomains, _numericFunctions, _policy, control);

            int executedCount = 0;
            int shortCircuitedCount = 0;

            for (int i = 0; i < triggers.Count; i++)
            {
                var entry = triggers[i];
                var cue = entry.Trigger.Cue;

                // ========== 检查优先级打断 ==========
                if (control.ShouldBlock(entry.Phase, entry.Priority))
                {
                    var reason = control.InterruptConditionPassed
                        ? ShortCircuitReason.InterruptedByHigherPriority
                        : ShortCircuitReason.InterruptedByFailedCondition;
                    _lifecycle.OnShortCircuit(key, in args, entry.Phase, entry.Priority, entry.Order, reason);
                    _observer.OnShortCircuit(key, in args, entry.Phase, entry.Priority, entry.Order,
                        control.InterruptConditionPassed
                            ? ETriggerShortCircuitReason.InterruptedByHigherPriority
                            : ETriggerShortCircuitReason.InterruptedByFailedCondition,
                        in execCtx);
                    shortCircuitedCount++;

                    // --- Cue: 被优先级打断跳过 ---
                    var cueCtx = BuildCueContext(key, in args, entry.Phase, entry.Priority, entry.Order, entry.Trigger, reason,
                        control.InterruptSourceName, control.InterruptTriggerId, control.InterruptConditionPassed, control);
                    cue.OnSkipped(in cueCtx);
                    continue;
                }

                // ========== Evaluate 阶段 ==========
                _lifecycle.OnBeforeEvaluate(key, in args, entry.Phase, entry.Priority, entry.Order);
                _observer.OnEvaluate(key, in args, entry.Phase, entry.Priority, entry.Order, false, in execCtx);

                bool ok;
                try
                {
                    ok = entry.Trigger.Evaluate(in args, in execCtx);
                }
                catch (Exception ex)
                {
                    _lifecycle.OnConditionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name);
                    _observer.OnConditionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, in execCtx);
                    _lifecycle.OnActionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, "Evaluate", 0, 0, ex.Message);
                    _observer.OnActionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, "Evaluate", 0, 0, ex.Message, in execCtx);

                    // --- Cue: 条件异常视为失败 ---
                    var failCtx = BuildCueContext(key, in args, entry.Phase, entry.Priority, entry.Order, entry.Trigger,
                        ShortCircuitReason.ConditionFailed, null, 0, false, control);
                    cue.OnConditionFailed(in failCtx);
                    break;
                }

                _lifecycle.OnAfterEvaluate(key, in args, entry.Phase, entry.Priority, entry.Order, ok);
                _observer.OnEvaluate(key, in args, entry.Phase, entry.Priority, entry.Order, ok, in execCtx);

                if (ok)
                {
                    _lifecycle.OnConditionPassed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name);
                    _observer.OnConditionPassed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, in execCtx);

                    // --- Cue: 条件通过，进入 Execute ---
                    var passCtx = BuildCueContext(key, in args, entry.Phase, entry.Priority, entry.Order, entry.Trigger,
                        ShortCircuitReason.None, null, 0, true, control);
                    cue.OnConditionPassed(in passCtx);
                }
                else
                {
                    // 条件失败：默认跳过（continue），仅记录生命周期
                    // 只有 Strict 策略才打断全部
                    _lifecycle.OnConditionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name);
                    _observer.OnConditionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, in execCtx);
                    shortCircuitedCount++;

                    // --- Cue: 条件失败跳过 ---
                    var failCtx = BuildCueContext(key, in args, entry.Phase, entry.Priority, entry.Order, entry.Trigger,
                        ShortCircuitReason.ConditionFailed, null, 0, false, control);
                    cue.OnConditionFailed(in failCtx);

                    if (_interruptPolicy == EInterruptPolicy.Strict)
                    {
                        _lifecycle.OnShortCircuit(key, in args, entry.Phase, entry.Priority, entry.Order, ShortCircuitReason.ConditionFailed);
                        _observer.OnShortCircuit(key, in args, entry.Phase, entry.Priority, entry.Order, ETriggerShortCircuitReason.ConditionFailed, in execCtx);

                        // --- Cue: 严格模式下被条件失败打断 ---
                        var strictCtx = BuildCueContext(key, in args, entry.Phase, entry.Priority, entry.Order, entry.Trigger,
                            ShortCircuitReason.ConditionFailed, null, 0, false, control);
                        cue.OnInterrupted(in strictCtx);
                        break;
                    }

                    continue;
                }

                // ========== Execute 阶段 ==========
                _lifecycle.OnBeforeExecute(key, in args, entry.Phase, entry.Priority, entry.Order);

                bool wasInterrupted = false;
                bool actionExecuted = false;

                try
                {
                    entry.Trigger.Execute(in args, in execCtx);
                    actionExecuted = true;
                }
                catch (Exception ex)
                {
                    _lifecycle.OnActionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, 0, 1, ex.Message);
                    _observer.OnActionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, 0, 1, ex.Message, in execCtx);
                }

                if (control.StopPropagation || control.Cancel)
                {
                    wasInterrupted = true;
                    shortCircuitedCount++;
                    var reason = control.StopPropagation ? ShortCircuitReason.StopPropagation : ShortCircuitReason.Cancel;
                    _lifecycle.OnShortCircuit(key, in args, entry.Phase, entry.Priority, entry.Order, reason);
                    _observer.OnShortCircuit(key, in args, entry.Phase, entry.Priority, entry.Order,
                        control.StopPropagation ? ETriggerShortCircuitReason.StopPropagation : ETriggerShortCircuitReason.Cancel, in execCtx);

                    // --- Cue: Execute 被打断 ---
                    var interruptCtx = BuildCueContext(key, in args, entry.Phase, entry.Priority, entry.Order, entry.Trigger,
                        ShortCircuitReason.StopPropagation, control.InterruptSourceName ?? entry.Trigger.GetType().Name, 0, true, control);
                    cue.OnInterrupted(in interruptCtx);
                    break;
                }

                _lifecycle.OnAfterExecute(key, in args, entry.Phase, entry.Priority, entry.Order);
                _observer.OnExecute(key, in args, entry.Phase, entry.Priority, entry.Order, in execCtx);

                if (!wasInterrupted && actionExecuted)
                {
                    executedCount++;
                }
            }

            _lifecycle.OnEventDispatched(key, in args, executedCount, shortCircuitedCount);
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
            ExecutionControl control)
        {
            var triggerId = 0;
            var triggerTypeName = trigger?.GetType().Name ?? "Unknown";
            if (trigger is ITriggerWithId tid) triggerId = tid.TriggerId;

            return new TriggerCueContext(
                key.IntId,
                key.StringId,
                args,
                phase,
                priority,
                order,
                triggerId,
                triggerTypeName,
                MapReason(reason),
                interruptSourceName,
                interruptTriggerId,
                interruptConditionPassed,
                control);
        }

        private static ETriggerShortCircuitReason MapReason(ShortCircuitReason reason)
        {
            switch (reason)
            {
                case ShortCircuitReason.ConditionFailed: return ETriggerShortCircuitReason.ConditionFailed;
                case ShortCircuitReason.StopPropagation: return ETriggerShortCircuitReason.StopPropagation;
                case ShortCircuitReason.Cancel: return ETriggerShortCircuitReason.Cancel;
                case ShortCircuitReason.InterruptedByHigherPriority: return ETriggerShortCircuitReason.InterruptedByHigherPriority;
                case ShortCircuitReason.InterruptedByFailedCondition: return ETriggerShortCircuitReason.InterruptedByFailedCondition;
                default: return ETriggerShortCircuitReason.None;
            }
        }

        private static void InsertSorted<TArgs>(List<Entry<TArgs>> list, Entry<TArgs> entry)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var other = list[i];
                if (entry.Phase < other.Phase)
                {
                    list.Insert(i, entry);
                    return;
                }

                if (entry.Phase == other.Phase && entry.Priority < other.Priority)
                {
                    list.Insert(i, entry);
                    return;
                }

                if (entry.Phase == other.Phase && entry.Priority == other.Priority && entry.Order < other.Order)
                {
                    list.Insert(i, entry);
                    return;
                }
            }

            list.Add(entry);
        }

        private readonly struct Entry<TArgs>
        {
            public readonly int Phase;
            public readonly int Priority;
            public readonly long Order;
            public readonly ITrigger<TArgs, TCtx> Trigger;

            public Entry(int phase, int priority, long order, ITrigger<TArgs, TCtx> trigger)
            {
                Phase = phase;
                Priority = priority;
                Order = order;
                Trigger = trigger;
            }
        }

        private sealed class Dispatcher<TArgs>
        {
            private readonly TriggerRunner<TCtx> _runner;
            private readonly EventKey<TArgs> _key;
            private readonly Dictionary<EventKey<TArgs>, List<Entry<TArgs>>> _list;

            public Dispatcher(TriggerRunner<TCtx> runner, EventKey<TArgs> key, Dictionary<EventKey<TArgs>, List<Entry<TArgs>>> list)
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

            private sealed class Registration<TArgs> : IDisposable
            {
                private List<Entry<TArgs>> _list;
                private Entry<TArgs> _entry;
                private readonly TriggerRunner<TCtx> _runner;
                private readonly EventKey<TArgs> _key;
                private readonly IDisposable _subscription;
                private bool _disposed;

                public Registration(List<Entry<TArgs>> list, Entry<TArgs> entry, TriggerRunner<TCtx> runner, EventKey<TArgs> key, IDisposable subscription)
                {
                    _list = list;
                    _entry = entry;
                    _runner = runner;
                    _key = key;
                    _subscription = subscription;
                }

                public void Dispose()
                {
                    if (_list == null || _disposed) return;
                    _disposed = true;

                    // 从列表中移除条目
                    int removeIndex = -1;
                    for (int i = 0; i < _list.Count; i++)
                    {
                        if (!ReferenceEquals(_list[i].Trigger, _entry.Trigger)) continue;
                        if (_list[i].Phase != _entry.Phase) continue;
                        if (_list[i].Priority != _entry.Priority) continue;
                        if (_list[i].Order != _entry.Order) continue;
                        removeIndex = i;
                        break;
                    }

                    if (removeIndex >= 0)
                    {
                        _list.RemoveAt(removeIndex);
                    }

                    var entry = _entry;
                    var key = _key;
                    var runner = _runner;
                    var subscription = _subscription;
                    var listWasEmpty = _list.Count == 0;
                    _list = null;
                    _entry = default;

                    runner._lifecycle.OnUnregistered(key, entry.Trigger);

                    // 检查列表是否为空，如果为空则取消事件订阅
                    if (listWasEmpty && subscription != null)
                    {
                        runner.TryUnsubscribe(key, subscription);
                    }
                }
            }
    }
}
