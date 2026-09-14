using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Variables.Numeric.Expression;
using AbilityKit.Triggering.Runtime.ActionScheduler;
using AbilityKit.Triggering.Collections;

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
        public readonly ITriggerCollectionResolver Collections;
        public readonly ITriggerRandomSource RandomSource;
        public readonly ITriggerExecutionScheduler ExecutionScheduler;

        /// <summary>
        /// 强类型 Payload 访问器注册表（避免装箱开销）
        /// </summary>
        public readonly IStronglyTypedPayloadAccessorRegistry StronglyTypedPayloads;

        public ExecCtx(TCtx context, IEventBus eventBus, FunctionRegistry functions, ActionRegistry actions, IBlackboardResolver blackboards, IPayloadAccessorRegistry payloads, IIdNameRegistry idNames, INumericVarDomainRegistry numericDomains, INumericRpnFunctionRegistry numericFunctions, ExecPolicy policy, ExecutionControl control, ActionSchedulerManager actionSchedulerManager = null, ITriggerCollectionResolver collections = null, ITriggerRandomSource randomSource = null, ITriggerExecutionScheduler executionScheduler = null)
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
            Collections = collections;
            RandomSource = randomSource;
            ExecutionScheduler = executionScheduler;
        }

        public ExecCtx(TCtx context, IEventBus eventBus, FunctionRegistry functions, ActionRegistry actions, IBlackboardResolver blackboards, IPayloadAccessorRegistry payloads, IStronglyTypedPayloadAccessorRegistry stronglyTypedPayloads, IIdNameRegistry idNames, INumericVarDomainRegistry numericDomains, INumericRpnFunctionRegistry numericFunctions, ExecPolicy policy, ExecutionControl control, ActionSchedulerManager actionSchedulerManager = null, ITriggerCollectionResolver collections = null, ITriggerRandomSource randomSource = null, ITriggerExecutionScheduler executionScheduler = null)
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
            Collections = collections;
            RandomSource = randomSource;
            ExecutionScheduler = executionScheduler;
        }

        /// <summary>
        /// ActionScheduler 管理器（可选，为 null 时表示当前执行上下文不启用 ActionScheduler）
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

            // 当未注册强类型访问器时，使用通用 Payload 访问器作为正式兼容入口。
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

            // 当未注册强类型访问器时，使用通用 Payload 访问器作为正式兼容入口。
            if (Payloads != null)
            {
                return Payloads.TryGetInt(in payload, fieldId, out value);
            }

            value = default;
            return false;
        }
    }
}
