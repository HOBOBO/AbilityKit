using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Variables.Numeric.Expression;
using AbilityKit.Triggering.Runtime.ActionScheduler;

namespace AbilityKit.Triggering.Runtime
{
    public readonly struct ExecCtx<TCtx>
    {
        public readonly TCtx Context;
        public readonly IEventBus EventBus;
        public readonly FunctionRegistry Functions;
        public readonly ActionRegistry Actions;
        public readonly IBlackboardResolver Blackboards;
        public readonly IPayloadAccessorRegistry Payloads;
        public readonly IIdNameRegistry IdNames;
        public readonly INumericVarDomainRegistry NumericDomains;
        public readonly INumericRpnFunctionRegistry NumericFunctions;
        public readonly ExecPolicy Policy;
        public readonly ExecutionControl Control;

        /// <summary>
        /// 强类型 Payload 访问器注册表（避免装箱开销）
        /// </summary>
        public readonly IStronglyTypedPayloadAccessorRegistry StronglyTypedPayloads;

        public ExecCtx(TCtx context, IEventBus eventBus, FunctionRegistry functions, ActionRegistry actions, IBlackboardResolver blackboards, IPayloadAccessorRegistry payloads, IIdNameRegistry idNames, INumericVarDomainRegistry numericDomains, INumericRpnFunctionRegistry numericFunctions, ExecPolicy policy, ExecutionControl control, ActionSchedulerManager actionSchedulerManager = null)
        {
            Context = context;
            EventBus = eventBus;
            Functions = functions;
            Actions = actions;
            Blackboards = blackboards;
            Payloads = payloads;
            IdNames = idNames;
            NumericDomains = numericDomains;
            NumericFunctions = numericFunctions;
            Policy = policy;
            Control = control;
            StronglyTypedPayloads = null;
            ActionSchedulerManager = actionSchedulerManager;
        }

        public ExecCtx(TCtx context, IEventBus eventBus, FunctionRegistry functions, ActionRegistry actions, IBlackboardResolver blackboards, IPayloadAccessorRegistry payloads, IStronglyTypedPayloadAccessorRegistry stronglyTypedPayloads, IIdNameRegistry idNames, INumericVarDomainRegistry numericDomains, INumericRpnFunctionRegistry numericFunctions, ExecPolicy policy, ExecutionControl control, ActionSchedulerManager actionSchedulerManager = null)
        {
            Context = context;
            EventBus = eventBus;
            Functions = functions;
            Actions = actions;
            Blackboards = blackboards;
            Payloads = payloads;
            IdNames = idNames;
            NumericDomains = numericDomains;
            NumericFunctions = numericFunctions;
            Policy = policy;
            Control = control;
            StronglyTypedPayloads = stronglyTypedPayloads;
            ActionSchedulerManager = actionSchedulerManager;
        }

        /// <summary>
        /// ActionScheduler 管理器（可选，为 null 时使用旧模式）
        /// </summary>
        public readonly ActionSchedulerManager ActionSchedulerManager;

        /// <summary>
        /// 尝试使用强类型访问器获取 Payload 字段值
        /// </summary>
        public bool TryGetPayloadDouble<TPayload>(in TPayload payload, int fieldId, out double value) where TPayload : struct
        {
            if (StronglyTypedPayloads != null && StronglyTypedPayloads.TryGetAccessor<TPayload>(out var accessor))
            {
                return accessor.TryGetDouble(in payload, fieldId, out value);
            }

            // Fallback to legacy accessor
            if (Payloads != null)
            {
                return Payloads.TryGetDouble(in payload, fieldId, out value);
            }

            value = default;
            return false;
        }

        /// <summary>
        /// 尝试使用强类型访问器获取 Payload 字段值
        /// </summary>
        public bool TryGetPayloadInt<TPayload>(in TPayload payload, int fieldId, out int value) where TPayload : struct
        {
            if (StronglyTypedPayloads != null && StronglyTypedPayloads.TryGetAccessor<TPayload>(out var accessor))
            {
                return accessor.TryGetInt(in payload, fieldId, out value);
            }

            // Fallback to legacy accessor
            if (Payloads != null)
            {
                return Payloads.TryGetInt(in payload, fieldId, out value);
            }

            value = default;
            return false;
        }
    }
}
