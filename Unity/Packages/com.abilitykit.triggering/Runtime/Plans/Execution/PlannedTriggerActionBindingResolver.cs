using System;
using AbilityKit.Triggering.Registry;

namespace AbilityKit.Triggering.Runtime.Plan
{
    /// <summary>
    /// PlannedTrigger 的 Action 委托绑定解析器。
    /// 负责按 ActionCallPlan 顺序解析具名参数委托与位置参数委托，避免 PlannedTrigger 继续承载注册表查找细节。
    /// </summary>
    internal static class PlannedTriggerActionBindingResolver<TArgs, TCtx>
    {
        public static void ResolveAll(
            ActionCallPlan[] actions,
            in ExecCtx<TCtx> ctx,
            NamedAction0<TArgs, object, TCtx>[] actions0,
            NamedAction1<TArgs, object, TCtx>[] actions1,
            NamedAction2<TArgs, object, TCtx>[] actions2,
            bool[] useNamedArgs)
        {
            if (actions == null || actions.Length == 0)
            {
                return;
            }

            for (int i = 0; i < actions.Length; i++)
            {
                ResolveOne(actions[i], i, in ctx, actions0, actions1, actions2, useNamedArgs);
            }
        }

        private static void ResolveOne(
            ActionCallPlan call,
            int index,
            in ExecCtx<TCtx> ctx,
            NamedAction0<TArgs, object, TCtx>[] actions0,
            NamedAction1<TArgs, object, TCtx>[] actions1,
            NamedAction2<TArgs, object, TCtx>[] actions2,
            bool[] useNamedArgs)
        {
            var arguments = call.Arguments;
            if (arguments.HasNamedArgs && TryResolveNamedAction(call, in arguments, index, in ctx, actions0, actions1, actions2, useNamedArgs))
            {
                return;
            }

            ResolvePositionalAction(call, in arguments, index, in ctx, actions0, actions1, actions2, useNamedArgs);
        }

        private static bool TryResolveNamedAction(
            ActionCallPlan call,
            in ActionArgumentsPlan arguments,
            int index,
            in ExecCtx<TCtx> ctx,
            NamedAction0<TArgs, object, TCtx>[] actions0,
            NamedAction1<TArgs, object, TCtx>[] actions1,
            NamedAction2<TArgs, object, TCtx>[] actions2,
            bool[] useNamedArgs)
        {
            switch (arguments.Arity)
            {
                case 0:
                    if (TryResolveAction0(call.Id, in ctx, out var na0, out var na0Det))
                    {
                        EnsureDeterministic(in ctx, call.Id, na0Det, "named action");
                        actions0[index] = na0;
                        useNamedArgs[index] = true;
                        return true;
                    }
                    return false;

                case 1:
                    if (TryResolveAction1(call.Id, in ctx, out var na1, out var na1Det))
                    {
                        EnsureDeterministic(in ctx, call.Id, na1Det, "named action");
                        actions1[index] = na1;
                        useNamedArgs[index] = true;
                        return true;
                    }
                    return false;

                case 2:
                    if (TryResolveAction2(call.Id, in ctx, out var na2, out var na2Det))
                    {
                        EnsureDeterministic(in ctx, call.Id, na2Det, "named action");
                        actions2[index] = na2;
                        useNamedArgs[index] = true;
                        return true;
                    }
                    return false;

                default:
                    return false;
            }
        }

        private static void ResolvePositionalAction(
            ActionCallPlan call,
            in ActionArgumentsPlan arguments,
            int index,
            in ExecCtx<TCtx> ctx,
            NamedAction0<TArgs, object, TCtx>[] actions0,
            NamedAction1<TArgs, object, TCtx>[] actions1,
            NamedAction2<TArgs, object, TCtx>[] actions2,
            bool[] useNamedArgs)
        {
            switch (arguments.Arity)
            {
                case 0:
                    if (!TryResolveAction0(call.Id, in ctx, out var a0, out var a0Det))
                    {
                        ThrowActionNotFound(in ctx, call.Id, arguments.Arity);
                    }

                    EnsureDeterministic(in ctx, call.Id, a0Det, "positional action");
                    actions0[index] = a0;
                    useNamedArgs[index] = false;
                    break;

                case 1:
                    if (!TryResolveAction1(call.Id, in ctx, out var a1, out var a1Det))
                    {
                        ThrowActionNotFound(in ctx, call.Id, arguments.Arity);
                    }

                    EnsureDeterministic(in ctx, call.Id, a1Det, "positional action");
                    actions1[index] = a1;
                    useNamedArgs[index] = false;
                    break;

                case 2:
                    if (!TryResolveAction2(call.Id, in ctx, out var a2, out var a2Det))
                    {
                        ThrowActionNotFound(in ctx, call.Id, arguments.Arity);
                    }

                    EnsureDeterministic(in ctx, call.Id, a2Det, "positional action");
                    actions2[index] = a2;
                    useNamedArgs[index] = false;
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported action arity: {arguments.Arity}");
            }
        }

        private static bool TryResolveAction0(ActionId id, in ExecCtx<TCtx> ctx, out NamedAction0<TArgs, object, TCtx> action, out bool isDeterministic)
        {
            if (ctx.Actions.TryGet<NamedAction0<TArgs, object, TCtx>>(id, out action, out isDeterministic))
            {
                return true;
            }

            if (ctx.Actions.TryGet<NamedAction0<object, object, TCtx>>(id, out var objectAction, out isDeterministic))
            {
                action = (triggerArgs, actionArgs, execCtx) => objectAction(triggerArgs, actionArgs, execCtx);
                return true;
            }

            action = null;
            isDeterministic = false;
            return false;
        }

        private static bool TryResolveAction1(ActionId id, in ExecCtx<TCtx> ctx, out NamedAction1<TArgs, object, TCtx> action, out bool isDeterministic)
        {
            if (ctx.Actions.TryGet<NamedAction1<TArgs, object, TCtx>>(id, out action, out isDeterministic))
            {
                return true;
            }

            if (ctx.Actions.TryGet<NamedAction1<object, object, TCtx>>(id, out var objectAction, out isDeterministic))
            {
                action = (triggerArgs, actionArgs, execCtx) => objectAction(triggerArgs, actionArgs, execCtx);
                return true;
            }

            action = null;
            isDeterministic = false;
            return false;
        }

        private static bool TryResolveAction2(ActionId id, in ExecCtx<TCtx> ctx, out NamedAction2<TArgs, object, TCtx> action, out bool isDeterministic)
        {
            if (ctx.Actions.TryGet<NamedAction2<TArgs, object, TCtx>>(id, out action, out isDeterministic))
            {
                return true;
            }

            if (ctx.Actions.TryGet<NamedAction2<object, object, TCtx>>(id, out var objectAction, out isDeterministic))
            {
                action = (triggerArgs, actionArgs, execCtx) => objectAction(triggerArgs, actionArgs, execCtx);
                return true;
            }

            action = null;
            isDeterministic = false;
            return false;
        }

        private static void EnsureDeterministic(in ExecCtx<TCtx> ctx, ActionId id, bool isDeterministic, string bindingKind)
        {
            if (ctx.Policy.RequireDeterministic && !isDeterministic)
            {
                throw new InvalidOperationException($"Non-deterministic {bindingKind} is not allowed by policy. id={FormatActionId(in ctx, id)}");
            }
        }

        private static void ThrowActionNotFound(in ExecCtx<TCtx> ctx, ActionId id, byte arity)
        {
            throw new InvalidOperationException($"Action not found or signature mismatch. id={FormatActionId(in ctx, id)} arity={arity}");
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
