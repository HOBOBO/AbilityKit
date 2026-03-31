using System;

namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// ExecCtx 扩展方法
    /// 提供便捷的上下文访问方式
    /// </summary>
    public static class ExecCtxExtensions
    {
        /// <summary>
        /// 检查上下文是否为空（未设置）
        /// </summary>
        public static bool IsDefault<TCtx>(this in ExecCtx<TCtx> ctx)
        {
            return ctx.Context == null;
        }

        /// <summary>
        /// 获取上下文实例（可能为 null）
        /// </summary>
        public static TCtx GetContext<TCtx>(this in ExecCtx<TCtx> ctx)
        {
            return ctx.Context;
        }

        /// <summary>
        /// 尝试获取上下文（适用于 struct 上下文）
        /// </summary>
        public static bool TryGetContext<TCtx>(this in ExecCtx<TCtx> ctx, out TCtx context)
        {
            context = ctx.Context;
            return true;
        }

        /// <summary>
        /// 安全地访问上下文（当上下文为引用类型时）
        /// </summary>
        public static TResult WithContext<TCtx, TResult>(this in ExecCtx<TCtx> ctx, Func<TCtx, TResult> selector, TResult defaultValue = default)
        {
            if (selector == null) return defaultValue;
            try
            {
                var context = ctx.Context;
                return context != null ? selector(context) : defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 安全地访问上下文（当上下文为引用类型时）
        /// </summary>
        public static void WithContext<TCtx>(this in ExecCtx<TCtx> ctx, Action<TCtx> action)
        {
            if (action == null) return;
            try
            {
                var context = ctx.Context;
                if (context != null)
                {
                    action(context);
                }
            }
            catch
            {
                // 忽略异常
            }
        }
    }

    /// <summary>
    /// ExecCtx 的泛型类型帮助类
    /// 用于在运行时检查和转换上下文类型
    /// </summary>
    public static class ExecCtxTypeHelper<TCtx>
    {
        /// <summary>
        /// 检查上下文是否为特定类型
        /// </summary>
        public static bool IsType<TContext>()
        {
            return typeof(TCtx) == typeof(TContext);
        }

        /// <summary>
        /// 尝试将上下文转换为目标类型
        /// </summary>
        public static bool TryCast<TCtx, TTarget>(in ExecCtx<TCtx> ctx, out ExecCtx<TTarget> result)
        {
            if (typeof(TCtx) == typeof(TTarget))
            {
                result = new ExecCtx<TTarget>(
                    context: (TTarget)(object)ctx.Context,
                    eventBus: ctx.EventBus,
                    functions: ctx.Functions,
                    actions: ctx.Actions,
                    blackboards: ctx.Blackboards,
                    payloads: ctx.Payloads,
                    stronglyTypedPayloads: ctx.StronglyTypedPayloads,
                    idNames: ctx.IdNames,
                    numericDomains: ctx.NumericDomains,
                    numericFunctions: ctx.NumericFunctions,
                    policy: ctx.Policy,
                    control: ctx.Control);
                return true;
            }
            result = default;
            return false;
        }
    }
}
