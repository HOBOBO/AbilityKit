using System;
using System.Collections.Generic;
using AbilityKit.Core.Eventing;
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
    /// 将类型化事件订阅连接到触发器评估与执行，并向生命周期、观察者和诊断接口报告结果。
    /// </summary>
    /// <typeparam name="TCtx">触发器执行上下文的类型。</typeparam>
    public sealed partial class TriggerRunner<TCtx>
    {
        private readonly IEventBus _eventBus;
        private readonly ITriggerContextSource<TCtx> _contextSource;
        private readonly ITriggerObserver<TCtx> _observer;
        private readonly ITriggerLifecycle<TCtx> _lifecycle;
        private readonly ITriggerTracer<TCtx> _tracer;

        private readonly TriggerRunnerRuntimeServices<TCtx> _runtimeServices;
        private readonly EInterruptPolicy _interruptPolicy;
        private readonly ActionSchedulerManager _actionSchedulerManager;

        /// <summary>
        /// 装配运行器；事件总线、函数注册表和动作注册表为必需依赖。
        /// 未提供上下文来源时使用上下文类型的默认值，未提供通知接口时使用空实现。
        /// </summary>
        /// <remarks>
        /// 只有非即时扁平动作需要调度管理器；宿主须显式传入并推进同一实例。
        /// 不传入时仍支持即时触发，运行器不创建或推进调度器。
        /// </remarks>
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
            _actionSchedulerManager = actionSchedulerManager;
        }

        /// <summary>
        /// 当前运行器是否已由宿主装配扁平动作调度。
        /// </summary>
        internal bool HasActionSchedulerManager => _actionSchedulerManager != null;

        /// <summary>
        /// 注册触发器；同一事件内先按阶段升序，再按优先级降序，同值按注册先后派发。
        /// </summary>
        /// <returns>当前注册的释放句柄；正常释放最后一条注册时解除对应的总线订阅。</returns>
        /// <remarks>
        /// 注册通知在条目插入后同步发出，通知异常不回滚注册。派发遍历活动列表，调用方不应依赖回调中变更注册时的遍历结果。
        /// </remarks>
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
    }
}
