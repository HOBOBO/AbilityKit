using System;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Time;
using AbilityKit.Triggering.Runtime.Random;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Variables.Numeric.Expression;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Instance;

namespace AbilityKit.Triggering.Runtime.Context
{
    /// <summary>
    /// 触发器上下文接口
    /// </summary>
    public interface ITriggerContext
    {
        IBlackboardResolver Blackboards { get; }
        IEventBus EventBus { get; }
        IFrameClock FrameClock { get; }
        IRandomProvider Random { get; }
        FunctionRegistry Functions { get; }
        ActionRegistry Actions { get; }
        IPayloadAccessorRegistry Payloads { get; }
        IStronglyTypedPayloadAccessorRegistry StronglyTypedPayloads { get; }
        IIdNameRegistry IdNames { get; }
        INumericVarDomainRegistry NumericDomains { get; }
        INumericRpnFunctionRegistry NumericFunctions { get; }
        ExecPolicy Policy { get; }
        ExecutionControl Control { get; }
        IServiceProvider ServiceProvider { get; }
        ExecCtx<object> CreateExecContext(object userContext = null);
        PredicateEvalContext CreatePredicateContext(object userContext = null);
    }

    /// <summary>
    /// 条件评估上下文
    /// </summary>
    public sealed class PredicateEvalContext
    {
        public object UserContext { get; }
        public ITriggerContext Context { get; }
        public INumericVarDomainRegistry NumericDomains { get; }
        public FunctionRegistry Functions { get; }

        public PredicateEvalContext(
            object userContext,
            ITriggerContext context,
            INumericVarDomainRegistry numericDomains,
            FunctionRegistry functions)
        {
            UserContext = userContext;
            Context = context;
            NumericDomains = numericDomains;
            Functions = functions;
        }
    }
}

namespace AbilityKit.Triggering.Runtime
{
    using System.Collections.Generic;

    /// <summary>
    /// 触发器注册表接口
    /// </summary>
    public interface ITriggerRegistry
    {
        int Count { get; }
        ITriggerHandle Register<TArgs>(object config) where TArgs : class;
        bool Unregister(int triggerId);
        bool TryGet(int triggerId, out ITriggerInstance trigger);
        IEnumerable<ITriggerInstance> GetAllTriggers();
        void Clear();
    }

    /// <summary>
    /// 触发器句柄接口
    /// </summary>
    public interface ITriggerHandle : IDisposable
    {
        int TriggerId { get; }
    }

    /// <summary>
    /// 触发器快照接口
    /// </summary>
    public interface ITriggerSnapshot
    {
        int TriggerId { get; }
        byte[] Data { get; }
    }
}