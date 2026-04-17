using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Time;
using AbilityKit.Triggering.Runtime.Random;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Variables.Numeric.Expression;
using AbilityKit.Triggering.Payload;

namespace AbilityKit.Triggering.Runtime.Context
{
    /// <summary>
    /// 触发器上下文默认实现
    /// 聚合所有必要的服务和提供者
    /// </summary>
    public sealed class TriggerContext : ITriggerContext
    {
        private readonly IServiceProvider _serviceProvider;

        /// <summary>黑板解析器</summary>
        public IBlackboardResolver Blackboards { get; }

        /// <summary>事件总线</summary>
        public IEventBus EventBus { get; }

        /// <summary>帧时钟</summary>
        public IFrameClock FrameClock { get; }

        /// <summary>随机数提供者</summary>
        public IRandomProvider Random { get; }

        /// <summary>函数注册表</summary>
        public FunctionRegistry Functions { get; }

        /// <summary>动作注册表</summary>
        public ActionRegistry Actions { get; }

        /// <summary>载荷访问器注册表</summary>
        public IPayloadAccessorRegistry Payloads { get; }

        /// <summary>强类型载荷访问器注册表</summary>
        public IStronglyTypedPayloadAccessorRegistry StronglyTypedPayloads { get; }

        /// <summary>ID名称映射</summary>
        public IIdNameRegistry IdNames { get; }

        /// <summary>数值变量域注册表</summary>
        public INumericVarDomainRegistry NumericDomains { get; }

        /// <summary>数值RPN函数注册表</summary>
        public INumericRpnFunctionRegistry NumericFunctions { get; }

        /// <summary>执行策略</summary>
        public ExecPolicy Policy { get; }

        /// <summary>执行控制</summary>
        public ExecutionControl Control { get; }

        /// <summary>服务提供者（用于获取游戏特定上下文）</summary>
        public IServiceProvider ServiceProvider => _serviceProvider;

        /// <summary>
        /// 创建触发器上下文
        /// </summary>
        public TriggerContext(
            IBlackboardResolver blackboards,
            IEventBus eventBus,
            IFrameClock frameClock,
            IRandomProvider random,
            FunctionRegistry functions,
            ActionRegistry actions,
            IPayloadAccessorRegistry payloads,
            IIdNameRegistry idNames,
            INumericVarDomainRegistry numericDomains,
            INumericRpnFunctionRegistry numericFunctions,
            ExecPolicy policy = default,
            ExecutionControl control = null,
            IServiceProvider serviceProvider = null,
            IStronglyTypedPayloadAccessorRegistry stronglyTypedPayloads = null)
        {
            Blackboards = blackboards ?? throw new ArgumentNullException(nameof(blackboards));
            EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            FrameClock = frameClock ?? throw new ArgumentNullException(nameof(frameClock));
            Random = random ?? throw new ArgumentNullException(nameof(random));
            Functions = functions ?? throw new ArgumentNullException(nameof(functions));
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
            Payloads = payloads;
            IdNames = idNames;
            NumericDomains = numericDomains;
            NumericFunctions = numericFunctions;
            Policy = policy;
            Control = control ?? new ExecutionControl();
            _serviceProvider = serviceProvider;
            StronglyTypedPayloads = stronglyTypedPayloads;
        }

        /// <summary>
        /// 获取游戏特定上下文（包外扩展）
        /// </summary>
        public T GetGameContext<T>() where T : class
        {
            if (_serviceProvider != null)
                return _serviceProvider.GetService(typeof(T)) as T;
            return null;
        }

        /// <summary>
        /// 创建执行上下文（用于 Trigger 执行）
        /// </summary>
        public ExecCtx<object> CreateExecContext(object userContext = null)
        {
            return new ExecCtx<object>(
                userContext,
                EventBus,
                Functions,
                Actions,
                Blackboards,
                Payloads,
                StronglyTypedPayloads,
                IdNames,
                NumericDomains,
                NumericFunctions,
                Policy,
                Control ?? new ExecutionControl(),
                null);
        }

        /// <summary>
        /// 创建条件评估上下文（用于 Predicate 评估）
        /// </summary>
        public PredicateEvalContext CreatePredicateContext(object userContext = null)
        {
            return new PredicateEvalContext(
                userContext,
                this,
                NumericDomains,
                Functions);
        }
    }
}
