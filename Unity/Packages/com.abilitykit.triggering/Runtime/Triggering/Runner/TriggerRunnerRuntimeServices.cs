using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Variables.Numeric.Expression;

namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 持有构建触发器执行上下文所需的运行时服务。
    /// </summary>
    internal sealed class TriggerRunnerRuntimeServices<TCtx>
    {
        public TriggerRunnerRuntimeServices(
            IEventBus eventBus,
            FunctionRegistry functions,
            ActionRegistry actions,
            IBlackboardResolver blackboards,
            IPayloadAccessorRegistry payloads,
            IIdNameRegistry idNames,
            INumericVarDomainRegistry numericDomains,
            INumericRpnFunctionRegistry numericFunctions,
            ExecPolicy policy)
        {
            EventBus = eventBus;
            Functions = functions;
            Actions = actions;
            Blackboards = blackboards;
            Payloads = payloads;
            IdNames = idNames;
            NumericDomains = numericDomains;
            NumericFunctions = numericFunctions;
            Policy = policy;
        }

        public IEventBus EventBus { get; }
        public FunctionRegistry Functions { get; }
        public ActionRegistry Actions { get; }
        public IBlackboardResolver Blackboards { get; }
        public IPayloadAccessorRegistry Payloads { get; }
        public IIdNameRegistry IdNames { get; }
        public INumericVarDomainRegistry NumericDomains { get; }
        public INumericRpnFunctionRegistry NumericFunctions { get; }
        public ExecPolicy Policy { get; }

        public ExecCtx<TCtx> CreateExecCtx(TCtx context, ExecutionControl control, IFlatActionScheduler flatActionScheduler = null)
        {
            return new ExecCtx<TCtx>(
                context,
                EventBus,
                Functions,
                Actions,
                Blackboards,
                Payloads,
                IdNames,
                NumericDomains,
                NumericFunctions,
                Policy,
                control,
                flatActionScheduler);
        }
    }
}
