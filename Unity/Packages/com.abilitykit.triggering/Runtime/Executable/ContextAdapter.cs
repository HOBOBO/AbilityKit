using System;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Abstractions;
using AbilityKit.Triggering.Runtime.Context;
using AbilityKit.Triggering.Variables.Numeric;
using BlackboardResolver = AbilityKit.Triggering.Runtime.Abstractions.IBlackboardResolver;

namespace AbilityKit.Triggering.Runtime.Executable
{
    /// <summary>
    /// 上下文适配器（内部使用）
    /// 将 object/ExecCtx 转换为 ActionContext 以兼容旧代码
    /// </summary>
    public static class ContextAdapter
    {
        private static int _idCounter = 0;

        /// <summary>
        /// 将旧上下文适配为 ActionContext
        /// 支持：
        /// 1. 已经是 ActionContext 的直接返回
        /// 2. ExecCtx<T> 结构体（反射提取字段）
        /// 3. 其他 object 类型（创建空上下文）
        /// </summary>
        public static ActionContext Adapt(object oldCtx)
        {
            if (oldCtx is ActionContext ctx)
                return ctx;

            // 尝试从 ExecCtx 提取服务并创建 ActionContext
            if (TryCreateFromExecCtx(oldCtx, out var actionCtx))
                return actionCtx;

            // 兜底：创建最小上下文
            return new ActionContext
            {
                InstanceId = GenerateId()
            };
        }

        private static bool TryCreateFromExecCtx(object execCtxObj, out ActionContext context)
        {
            context = null;

            if (execCtxObj == null)
                return false;

            var type = execCtxObj.GetType();
            if (!type.IsGenericType || type.GetGenericTypeDefinition()?.Name != "ExecCtx`1")
                return false;

            // 反射提取 ExecCtx 字段
            var actions = type.GetField("Actions")?.GetValue(execCtxObj) as ActionRegistry;
            var blackboards = type.GetField("Blackboards")?.GetValue(execCtxObj) as BlackboardResolver;
            var payloadRegistry = type.GetField("Payloads")?.GetValue(execCtxObj) as IPayloadAccessorRegistry;
            var eventBus = type.GetField("EventBus")?.GetValue(execCtxObj) as IEventBus;
            var numericDomains = type.GetField("NumericDomains")?.GetValue(execCtxObj) as INumericVarDomainRegistry;
            var policyField = type.GetField("Policy");
            var policyValue = policyField?.GetValue(execCtxObj);
            var policy = policyValue is Runtime.ExecPolicy p ? p : default;

            // 创建适配器
            var adapter = new ExecCtxAdapter(
                actions: actions,
                blackboards: blackboards,
                payloadRegistry: payloadRegistry,
                eventBus: eventBus,
                numericDomains: numericDomains,
                policy: policy);

            context = new ActionContext
            {
                InstanceId = GenerateId(),
                ActionName = "FromExecCtx"
            };

            context.SetServiceProvider(adapter);
            context.Blackboard = blackboards;
            context.Payloads = payloadRegistry as IPayloadAccessor;
            context.Events = eventBus;

            return true;
        }

        private static int GenerateId() => System.Threading.Interlocked.Increment(ref _idCounter);
    }
}
