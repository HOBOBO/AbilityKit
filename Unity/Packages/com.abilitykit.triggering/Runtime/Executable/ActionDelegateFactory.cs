using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Context;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime.Executable
{
    /// <summary>
    /// Action 委托工厂
    /// 负责将 ActionRegistry 中的委托适配为 ActionContext 委托
    /// </summary>
    internal static class ActionDelegateFactory
    {
        /// <summary>
        /// 创建 ActionCallExecutable 实例（通过 ActionRegistry 查找并绑定委托）
        /// </summary>
        public static ActionCallExecutable Create(ActionId actionId, NumericValueRef arg0, NumericValueRef arg1, ActionRegistry actions)
        {
            if (actions == null) throw new ArgumentNullException(nameof(actions));

            // 根据 arity 创建适配器
            if (!actions.TryGet<Action<object>>(actionId, out _, out _) &&
                !actions.TryGet<Action<object, double>>(actionId, out _, out _) &&
                !actions.TryGet<Action<object, double, double>>(actionId, out _, out _))
            {
                throw new InvalidOperationException($"Action with id {actionId} not found in registry or signature mismatch");
            }

            // 判断 arity
            byte arity = 0;
            if (actions.TryGet<Action<object, double>>(actionId, out _, out _)) arity = 1;
            else if (actions.TryGet<Action<object, double, double>>(actionId, out _, out _)) arity = 2;

            return arity switch
            {
                0 => new ActionCallExecutable(
                    action: (ctx) => Invoke0(actionId, actions, ctx),
                    actionId: actionId
                ),
                1 => new ActionCallExecutable(
                    action: (ctx, a0) => Invoke1(actionId, actions, ctx, a0),
                    actionId: actionId,
                    arg0: arg0
                ),
                2 => new ActionCallExecutable(
                    action: (ctx, a0, a1) => Invoke2(actionId, actions, ctx, a0, a1),
                    actionId: actionId,
                    arg0: arg0,
                    arg1: arg1
                ),
                _ => throw new NotSupportedException($"Unsupported arity: {arity}")
            };
        }

        private static void Invoke0(ActionId id, ActionRegistry actions, Context.ActionContext ctx)
        {
            if (actions.TryGet<Action<object>>(id, out var action, out _))
                action(ctx);
        }

        private static void Invoke1(ActionId id, ActionRegistry actions, Context.ActionContext ctx, double arg0)
        {
            if (actions.TryGet<Action<object, double>>(id, out var action, out _))
                action(ctx, arg0);
        }

        private static void Invoke2(ActionId id, ActionRegistry actions, Context.ActionContext ctx, double arg0, double arg1)
        {
            if (actions.TryGet<Action<object, double, double>>(id, out var action, out _))
                action(ctx, arg0, arg1);
        }
    }
}
