using System;
using System.Collections.Generic;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Variables.Numeric.Expression;

namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 分层触发器运行器配置选项
    /// </summary>
    public readonly struct HierarchicalOptions
    {
        /// <summary>
        /// 是否先执行父级触发器，再执行子级触发器
        /// 默认为 true（父级先执行）
        /// </summary>
        public readonly bool ExecuteParentFirst;

        /// <summary>
        /// 子级短路时是否停止父级后续触发器
        /// 默认为 true
        /// </summary>
        public readonly bool ShortCircuitStopsParent;

        /// <summary>
        /// 是否在父级也触发事件派发
        /// 默认为 false（仅在当前层级触发）
        /// </summary>
        public readonly bool PropagateToParent;

        /// <summary>
        /// 层级名称（用于调试）
        /// </summary>
        public readonly string ScopeName;

        public HierarchicalOptions(bool executeParentFirst = true, bool shortCircuitStopsParent = true, bool propagateToParent = false, string scopeName = null)
        {
            ExecuteParentFirst = executeParentFirst;
            ShortCircuitStopsParent = shortCircuitStopsParent;
            PropagateToParent = propagateToParent;
            ScopeName = scopeName;
        }

        public static HierarchicalOptions Default => new HierarchicalOptions();

        /// <summary>
        /// 技能层级预设（子级优先，因为技能逻辑通常覆盖全局逻辑）
        /// </summary>
        public static HierarchicalOptions SkillScope => new HierarchicalOptions(
            executeParentFirst: false,
            shortCircuitStopsParent: true,
            propagateToParent: false,
            scopeName: "Skill"
        );

        /// <summary>
        /// Buff 层级预设（全局先执行，Buff 可能需要覆盖）
        /// </summary>
        public static HierarchicalOptions BuffScope => new HierarchicalOptions(
            executeParentFirst: true,
            shortCircuitStopsParent: false,
            propagateToParent: false,
            scopeName: "Buff"
        );
    }

    /// <summary>
    /// 分层触发器运行器
    /// 支持父子层级结构，父级触发器可被子级覆盖或补充
    /// </summary>
    /// <typeparam name="TCtx">上下文类型</typeparam>
    public sealed class HierarchicalTriggerRunner<TCtx>
    {
        private readonly IEventBus _eventBus;
        private readonly ITriggerContextSource<TCtx> _contextSource;
        private readonly ITriggerLifecycle<TCtx> _lifecycle;
        private readonly ITriggerObserver<TCtx> _observer;

        private readonly FunctionRegistry _functions;
        private readonly ActionRegistry _actions;
        private readonly IBlackboardResolver _blackboards;
        private readonly IPayloadAccessorRegistry _payloads;
        private readonly IIdNameRegistry _idNames;
        private readonly INumericVarDomainRegistry _numericDomains;
        private readonly INumericRpnFunctionRegistry _numericFunctions;
        private readonly ExecPolicy _policy;
        private readonly EInterruptPolicy _interruptPolicy;
        private readonly HierarchicalOptions _options;

        private readonly Dictionary<Type, object> _triggerListsByArgsType = new Dictionary<Type, object>();
        private readonly Dictionary<Type, object> _subscriptionsByArgsType = new Dictionary<Type, object>();
        private long _registrationOrder;

        /// <summary>
        /// 父级触发器运行器（可为 null）
        /// </summary>
        public HierarchicalTriggerRunner<TCtx> Parent { get; }

        /// <summary>
        /// 层级名称（用于调试）
        /// </summary>
        public string ScopeName => _options.ScopeName;

        /// <summary>
        /// 当前层级的触发器数量
        /// </summary>
        public int LocalTriggerCount
        {
            get
            {
                int count = 0;
                foreach (var kvp in _triggerListsByArgsType)
                {
                    var dict = (Dictionary<EventKey<object>, List<Entry<object>>>)kvp.Value;
                    foreach (var listKvp in dict)
                    {
                        count += listKvp.Value.Count;
                    }
                }
                return count;
            }
        }

        public HierarchicalTriggerRunner(
            IEventBus eventBus,
            FunctionRegistry functions,
            ActionRegistry actions,
            ITriggerContextSource<TCtx> contextSource = null,
            ITriggerLifecycle<TCtx> lifecycle = null,
            IBlackboardResolver blackboards = null,
            IPayloadAccessorRegistry payloads = null,
            IIdNameRegistry idNames = null,
            INumericVarDomainRegistry numericDomains = null,
            INumericRpnFunctionRegistry numericFunctions = null,
            ExecPolicy policy = default,
            HierarchicalOptions options = default,
            HierarchicalTriggerRunner<TCtx> parent = null,
            EInterruptPolicy interruptPolicy = EInterruptPolicy.None)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _functions = functions ?? throw new ArgumentNullException(nameof(functions));
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
            _contextSource = contextSource;
            _lifecycle = lifecycle ?? NullTriggerLifecycle<TCtx>.Instance;
            _observer = NullTriggerObserver<TCtx>.Instance;
            _blackboards = blackboards;
            _payloads = payloads;
            _idNames = idNames;
            _numericDomains = numericDomains;
            _numericFunctions = numericFunctions;
            _policy = policy;
            _options = options;
            Parent = parent;
            _interruptPolicy = interruptPolicy;
        }

        /// <summary>
        /// 创建子级触发器运行器（便捷方法）
        /// </summary>
        public HierarchicalTriggerRunner<TCtx> CreateChild(HierarchicalOptions options = default)
        {
            return new HierarchicalTriggerRunner<TCtx>(
                _eventBus,
                _functions,
                _actions,
                _contextSource,
                _lifecycle,
                _blackboards,
                _payloads,
                _idNames,
                _numericDomains,
                _numericFunctions,
                _policy,
                options,
                this,
                _interruptPolicy
            );
        }

        /// <summary>
        /// 注册触发器
        /// </summary>
        public IDisposable Register<TArgs>(EventKey<TArgs> key, ITrigger<TArgs, TCtx> trigger, int phase = 0, int priority = 0)
        {
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));

            var list = GetOrCreateTriggerList<TArgs>();
            if (!list.TryGetValue(key, out var triggers))
            {
                triggers = new List<Entry<TArgs>>(4);
                list.Add(key, triggers);
                EnsureSubscribed(key, list);
            }

            var entry = new Entry<TArgs>(phase, priority, _registrationOrder++, trigger);
            InsertSorted(triggers, entry);
            _lifecycle.OnRegistered(key, trigger, phase, priority, entry.Order);
            return new Registration<TArgs>(this, key, triggers, entry, _lifecycle);
        }

        /// <summary>
        /// 批量注册触发器
        /// </summary>
        public void RegisterAll<TArgs>(EventKey<TArgs> key, IEnumerable<ITrigger<TArgs, TCtx>> triggers, int phase = 0, int priority = 0)
        {
            foreach (var trigger in triggers)
            {
                Register(key, trigger, phase, priority);
                priority++; // 每个触发器递增优先级
            }
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
            if (_subscriptionsByArgsType.TryGetValue(type, out var obj))
            {
                var subs = (Dictionary<EventKey<TArgs>, IDisposable>)obj;
                if (subs.ContainsKey(key)) return;
                var dispatcher = new Dispatcher<TArgs>(this, key, list);
                subs[key] = _eventBus.Subscribe(key, dispatcher.OnEvent);
                return;
            }

            var newSubs = new Dictionary<EventKey<TArgs>, IDisposable>();
            _subscriptionsByArgsType.Add(type, newSubs);
            {
                var dispatcher = new Dispatcher<TArgs>(this, key, list);
                newSubs[key] = _eventBus.Subscribe(key, dispatcher.OnEvent);
            }
        }

        private void Dispatch<TArgs>(EventKey<TArgs> key, in TArgs args, ExecutionControl control, Dictionary<EventKey<TArgs>, List<Entry<TArgs>>> list)
        {
            if (!list.TryGetValue(key, out var localTriggers) || localTriggers.Count == 0)
            {
                // 本地没有触发器，尝试父级
                if (Parent != null && _options.PropagateToParent)
                {
                    Parent.DispatchFromChild(key, in args, control);
                }
                return;
            }

            var ctx = _contextSource != null ? _contextSource.GetContext() : default;
            if (control == null) control = new ExecutionControl();
            control.Reset();

            // 创建执行上下文
            var execCtx = new ExecCtx<TCtx>(ctx, _eventBus, _functions, _actions, _blackboards, _payloads, _idNames, _numericDomains, _numericFunctions, _policy, control);

            // 执行顺序：父级先还是子级先
            if (_options.ExecuteParentFirst && Parent != null)
            {
                // 父级先执行
                var parentControl = new ExecutionControl();
                var parentScope = Parent.ScopeName ?? "Parent";
                _lifecycle.OnScopeTransition(parentScope, ScopeName ?? "Current");
                Parent.DispatchInternal(key, in args, parentControl, false);

                // 检查是否需要停止子级执行
                if (parentControl.StopPropagation || parentControl.Cancel)
                {
                    if (_options.ShortCircuitStopsParent)
                    {
                        _lifecycle.OnShortCircuit(key, in args, -1, -1, -1, parentControl.StopPropagation ? ShortCircuitReason.StopPropagation : ShortCircuitReason.Cancel);
                        return;
                    }
                }

                // 检查是否需要停止后续执行
                if (control.StopPropagation || control.Cancel)
                {
                    return;
                }
            }

            // 执行本地触发器
            DispatchTriggers(key, in args, localTriggers, in execCtx, control);

            // 父级后执行（如果子级先执行）
            if (!_options.ExecuteParentFirst && Parent != null && !control.StopPropagation && !control.Cancel)
            {
                var parentControl = new ExecutionControl();
                var parentScope = Parent.ScopeName ?? "Parent";
                _lifecycle.OnScopeTransition(ScopeName ?? "Current", parentScope);
                Parent.DispatchInternal(key, in args, parentControl, false);
            }
        }

        /// <summary>
        /// 内部调度方法（供父级调用）
        /// </summary>
        internal void DispatchInternal<TArgs>(EventKey<TArgs> key, in TArgs args, ExecutionControl control, bool fromChild)
        {
            var list = GetTriggerList<TArgs>(key);
            if (list == null || list.Count == 0) return;

            var ctx = _contextSource != null ? _contextSource.GetContext() : default;
            if (control == null) control = new ExecutionControl();

            var execCtx = new ExecCtx<TCtx>(ctx, _eventBus, _functions, _actions, _blackboards, _payloads, _idNames, _numericDomains, _numericFunctions, _policy, control);

            DispatchTriggers(key, in args, list, in execCtx, control);
        }

        /// <summary>
        /// 从子级触发的调度（用于 PropagateToParent 场景）
        /// </summary>
        internal void DispatchFromChild<TArgs>(EventKey<TArgs> key, in TArgs args, ExecutionControl control)
        {
            DispatchInternal(key, in args, control, true);
        }

        private void DispatchTriggers<TArgs>(EventKey<TArgs> key, in TArgs args, List<Entry<TArgs>> triggers, in ExecCtx<TCtx> execCtx, ExecutionControl control)
        {
            int executedCount = 0;
            int shortCircuitedCount = 0;

            _lifecycle.OnEventDispatching(key, in args);

            for (int i = 0; i < triggers.Count; i++)
            {
                var entry = triggers[i];

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
                    break;
                }

                _lifecycle.OnAfterEvaluate(key, in args, entry.Phase, entry.Priority, entry.Order, ok);
                _observer.OnEvaluate(key, in args, entry.Phase, entry.Priority, entry.Order, ok, in execCtx);

                if (ok)
                {
                    _lifecycle.OnConditionPassed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name);
                    _observer.OnConditionPassed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, in execCtx);
                }
                else
                {
                    _lifecycle.OnConditionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name);
                    _observer.OnConditionFailed(key, in args, entry.Phase, entry.Priority, entry.Order, 0, entry.Trigger.GetType().Name, in execCtx);
                    shortCircuitedCount++;

                    if (_interruptPolicy == EInterruptPolicy.Strict)
                    {
                        _lifecycle.OnShortCircuit(key, in args, entry.Phase, entry.Priority, entry.Order, ShortCircuitReason.ConditionFailed);
                        _observer.OnShortCircuit(key, in args, entry.Phase, entry.Priority, entry.Order, ETriggerShortCircuitReason.ConditionFailed, in execCtx);
                        break;
                    }

                    continue;
                }

                // ========== Execute 阶段 ==========
                _lifecycle.OnBeforeExecute(key, in args, entry.Phase, entry.Priority, entry.Order);

                bool wasInterrupted = false;
                try
                {
                    entry.Trigger.Execute(in args, in execCtx);
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
                    break;
                }

                _lifecycle.OnAfterExecute(key, in args, entry.Phase, entry.Priority, entry.Order);
                _observer.OnExecute(key, in args, entry.Phase, entry.Priority, entry.Order, in execCtx);

                if (!wasInterrupted)
                    executedCount++;
            }

            _lifecycle.OnEventDispatched(key, in args, executedCount, shortCircuitedCount);
        }

        private List<Entry<TArgs>> GetTriggerList<TArgs>(EventKey<TArgs> key)
        {
            var type = typeof(TArgs);
            if (_triggerListsByArgsType.TryGetValue(type, out var obj))
            {
                var dict = (Dictionary<EventKey<TArgs>, List<Entry<TArgs>>>)obj;
                if (dict.TryGetValue(key, out var list)) return list;
            }
            return null;
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

        /// <summary>
        /// 获取当前 Runner 的完整层级路径（用于调试）
        /// </summary>
        public string GetScopePath()
        {
            if (Parent == null) return _options.ScopeName ?? "Root";
            return $"{Parent.GetScopePath()}/{_options.ScopeName ?? "Anonymous"}";
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
            private readonly HierarchicalTriggerRunner<TCtx> _runner;
            private readonly EventKey<TArgs> _key;
            private readonly Dictionary<EventKey<TArgs>, List<Entry<TArgs>>> _list;

            public Dispatcher(HierarchicalTriggerRunner<TCtx> runner, EventKey<TArgs> key, Dictionary<EventKey<TArgs>, List<Entry<TArgs>>> list)
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
            private readonly HierarchicalTriggerRunner<TCtx> _runner;
            private readonly EventKey<TArgs> _key;
            private List<Entry<TArgs>> _list;
            private Entry<TArgs> _entry;
            private readonly ITriggerLifecycle<TCtx> _lifecycle;

            public Registration(HierarchicalTriggerRunner<TCtx> runner, EventKey<TArgs> key, List<Entry<TArgs>> list, Entry<TArgs> entry, ITriggerLifecycle<TCtx> lifecycle)
            {
                _runner = runner;
                _key = key;
                _list = list;
                _entry = entry;
                _lifecycle = lifecycle;
            }

            public void Dispose()
            {
                if (_list == null) return;
                for (int i = 0; i < _list.Count; i++)
                {
                    if (!ReferenceEquals(_list[i].Trigger, _entry.Trigger)) continue;
                    if (_list[i].Phase != _entry.Phase) continue;
                    if (_list[i].Priority != _entry.Priority) continue;
                    if (_list[i].Order != _entry.Order) continue;
                    _list.RemoveAt(i);
                    _lifecycle.OnUnregistered(_key, _entry.Trigger);
                    break;
                }
                _list = null;
                _entry = default;
            }
        }
    }
}
