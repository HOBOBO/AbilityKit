#nullable enable
using System;
using AbilityKit.Triggering.Registry;

namespace AbilityKit.Triggering.Runtime.Plan
{
    /// <summary>
    /// 单个动作的已绑定委托与参数模式；不保存调用时的上下文。
    /// </summary>
    internal readonly struct TriggerPlanActionBinding
    {
        internal readonly Delegate Callback;
        internal readonly bool UseNamedArgs;

        internal TriggerPlanActionBinding(Delegate callback, bool useNamedArgs)
        {
            Callback = callback;
            UseNamedArgs = useNamedArgs;
        }
    }

    /// <summary>
    /// 按动作 ID 和元数解析委托，优先使用类型化事件参数，再尝试 object 参数委托。
    /// </summary>
    internal static class PlannedTriggerActionBindingResolver<TArgs, TCtx>
    {
        /// <summary>
        /// 为一次动作调用建立绑定；不决定绑定的保存时间或动作何时运行。
        /// </summary>
        internal static TriggerPlanActionBinding Resolve(in ActionCallPlan call, in ExecCtx<TCtx> ctx)
        {
            var arguments = call.Arguments;
            if (arguments.Arity > 2)
            {
                throw new InvalidOperationException($"Unsupported action arity: {arguments.Arity}");
            }

            var useNamedArgs = arguments.HasNamedArgs;
            var callback = FindAction(call.Id, arguments.Arity, in ctx, out var isDeterministic);
            if (callback == null)
            {
                throw new InvalidOperationException($"Action not found or signature mismatch. id={FormatActionId(in ctx, call.Id)} arity={arguments.Arity}");
            }

            EnsureDeterministic(in ctx, call.Id, isDeterministic, useNamedArgs ? "named action" : "positional action");
            return new TriggerPlanActionBinding(callback, useNamedArgs);
        }

        private static Delegate? FindAction(ActionId id, byte arity, in ExecCtx<TCtx> ctx, out bool isDeterministic)
        {
            switch (arity)
            {
                case 0:
                    if (ctx.Actions.TryGet<NamedAction0<TArgs, object, TCtx>>(id, out var typed0, out isDeterministic)) return typed0;
                    if (ctx.Actions.TryGet<NamedAction0<object, object, TCtx>>(id, out var fallback0, out isDeterministic))
                    {
                        return new NamedAction0<TArgs, object, TCtx>((args, actionArgs, execCtx) => fallback0(args!, actionArgs, execCtx));
                    }
                    break;
                case 1:
                    if (ctx.Actions.TryGet<NamedAction1<TArgs, object, TCtx>>(id, out var typed1, out isDeterministic)) return typed1;
                    if (ctx.Actions.TryGet<NamedAction1<object, object, TCtx>>(id, out var fallback1, out isDeterministic))
                    {
                        return new NamedAction1<TArgs, object, TCtx>((args, actionArgs, execCtx) => fallback1(args!, actionArgs, execCtx));
                    }
                    break;
                case 2:
                    if (ctx.Actions.TryGet<NamedAction2<TArgs, object, TCtx>>(id, out var typed2, out isDeterministic)) return typed2;
                    if (ctx.Actions.TryGet<NamedAction2<object, object, TCtx>>(id, out var fallback2, out isDeterministic))
                    {
                        return new NamedAction2<TArgs, object, TCtx>((args, actionArgs, execCtx) => fallback2(args!, actionArgs, execCtx));
                    }
                    break;
            }

            isDeterministic = false;
            return null;
        }

        private static void EnsureDeterministic(in ExecCtx<TCtx> ctx, ActionId id, bool isDeterministic, string bindingKind)
        {
            if (ctx.Policy.RequireDeterministic && !isDeterministic)
            {
                throw new InvalidOperationException($"Non-deterministic {bindingKind} is not allowed by policy. id={FormatActionId(in ctx, id)}");
            }
        }

        private static string FormatActionId(in ExecCtx<TCtx> ctx, ActionId id)
        {
            var names = ctx.IdNames;
            if (names != null && names.TryGetActionName(id, out var name) && !string.IsNullOrEmpty(name))
            {
                return $"{id.Value}('{name}')";
            }

            return id.Value.ToString();
        }
    }
}
