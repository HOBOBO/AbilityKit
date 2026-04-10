using System;
using System.Collections.Generic;
using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Triggering.Runtime.Plan
{
    // ================================================================
    // Shared delegate types for NamedArgs action execution
    // Used by both the triggering runtime (PlannedTrigger) and business
    // packages (PlanActionModule implementations)
    // ================================================================

    /// <summary>
    /// 无参数的具名 Action 委托
    /// </summary>
    public delegate void NamedAction0<TTriggerArgs, TActionArgs, TCtx>(TTriggerArgs triggerArgs, TActionArgs actionArgs, ExecCtx<TCtx> ctx);

    /// <summary>
    /// 单参数的具名 Action 委托
    /// </summary>
    public delegate void NamedAction1<TTriggerArgs, TActionArgs, TCtx>(TTriggerArgs triggerArgs, TActionArgs actionArgs, ExecCtx<TCtx> ctx);

    /// <summary>
    /// 双参数的具名 Action 委托
    /// </summary>
    public delegate void NamedAction2<TTriggerArgs, TActionArgs, TCtx>(TTriggerArgs triggerArgs, TActionArgs actionArgs, ExecCtx<TCtx> ctx);

    /// <summary>
    /// 具名参数字典的只读包装器（避免直接暴露 Dictionary）
    /// </summary>
    public sealed class NamedArgsDict
    {
        public readonly Dictionary<string, ActionArgValue> InnerDict;

        public NamedArgsDict(Dictionary<string, ActionArgValue> inner)
        {
            InnerDict = inner ?? new Dictionary<string, ActionArgValue>();
        }

        public int Count => InnerDict.Count;
        public Dictionary<string, ActionArgValue>.Enumerator GetEnumerator() => InnerDict.GetEnumerator();
        public bool TryGetValue(string key, out ActionArgValue value) => InnerDict.TryGetValue(key, out value);
    }

    // ================================================================
    // Base class for business package implementations
    // Note: IPlanActionModule lives in business packages, not in triggering
    // ================================================================

    /// <summary>
    /// 基于具名参数 Schema 的 PlanAction Module 基类
    /// 子类定义强类型参数 struct（TActionArgs）和对应的 Schema
    /// 运行时从具名参数字典解析为强类型 struct 后调用 Execute
    /// </summary>
    /// <typeparam name="TActionArgs">强类型参数结构体（如 GiveDamageArgs）</typeparam>
    /// <typeparam name="TCtx">执行上下文类型（通常是 IWorldResolver）</typeparam>
    /// <typeparam name="TModule">实现类类型，用于 IPlanActionModule.Register</typeparam>
    public abstract class NamedArgsPlanActionModuleBase<TActionArgs, TCtx, TModule> : IPlanActionModule
        where TModule : NamedArgsPlanActionModuleBase<TActionArgs, TCtx, TModule>
    {
        /// <summary>
        /// Action ID（必须与 Schema 中的 ActionId 一致）
        /// </summary>
        protected abstract ActionId ActionId { get; }

        /// <summary>
        /// 参数 Schema（定义解析和验证逻辑）
        /// </summary>
        protected abstract IActionSchema<TActionArgs, TCtx> Schema { get; }

        /// <summary>
        /// 子类实现具体的 Action 执行逻辑
        /// </summary>
        /// <param name="triggerArgs">Trigger 的事件 payload（用于解析 caster/target 等上下文信息）</param>
        /// <param name="actionArgs">强类型参数结构体（从具名字典解析而来）</param>
        /// <param name="ctx">执行上下文</param>
        protected abstract void Execute(object triggerArgs, TActionArgs actionArgs, ExecCtx<TCtx> ctx);

        public void Register(ActionRegistry actions, IWorldResolver services)
        {
            if (actions == null || ActionId.Value == 0)
                return;

            // 注册具名参数 Action 委托
            // 同时注册 Arity=0/1/2 版本，PlannedTrigger 根据 ActionCallPlan.Arity 选择
            // 注意：TTriggerArgs=object（来自 PlannedTrigger<object>），TActionArgs=object（Schema 在运行时解析）
            // 因为 PlannedTrigger<object, TCtx> 中的 NamedAction 数组类型是 NamedAction2<object, object, TCtx>
            actions.Register<NamedAction0<object, object, TCtx>>(ActionId, CreateHandler0(), isDeterministic: true);
            actions.Register<NamedAction1<object, object, TCtx>>(ActionId, CreateHandler1(), isDeterministic: true);
            actions.Register<NamedAction2<object, object, TCtx>>(ActionId, CreateHandler2(), isDeterministic: true);

            // 注册 Schema（供 PlannedTrigger 解析时使用）
            ActionSchemaRegistry.Register<TActionArgs, TCtx>(Schema);
        }

        private NamedAction0<object, object, TCtx> CreateHandler0()
        {
            return (triggerArgs, actionArgs, ctx) =>
            {
                try
                {
                    if (ctx.Context == null) return;
                    Execute(triggerArgs, default, ctx);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[Plan] Action {ActionId.Value} ({typeof(TActionArgs).Name}) executed failed");
                }
            };
        }

        private NamedAction1<object, object, TCtx> CreateHandler1()
        {
            return (triggerArgs, actionArgs, ctx) =>
            {
                try
                {
                    if (ctx.Context == null) return;
                    var parsedArgs = ParseActionArgs(ctx, actionArgs);
                    Execute(triggerArgs, parsedArgs, ctx);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[Plan] Action {ActionId.Value} ({typeof(TActionArgs).Name}) executed failed");
                }
            };
        }

        private NamedAction2<object, object, TCtx> CreateHandler2()
        {
            return (triggerArgs, actionArgs, ctx) =>
            {
                try
                {
                    if (ctx.Context == null) return;
                    var parsedArgs = ParseActionArgs(ctx, actionArgs);
                    Execute(triggerArgs, parsedArgs, ctx);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[Plan] Action {ActionId.Value} ({typeof(TActionArgs).Name}) executed failed");
                }
            };
        }

        private TActionArgs ParseActionArgs(ExecCtx<TCtx> ctx, object rawArgs)
        {
            if (rawArgs is TActionArgs typed)
            {
                return typed;
            }

            if (rawArgs is NamedArgsDict dict)
            {
                return ActionSchemaRegistry.ParseArgs<TActionArgs, TCtx>(ActionId, dict.InnerDict, ctx);
            }

            if (rawArgs is Dictionary<string, ActionArgValue> namedArgs)
            {
                return ActionSchemaRegistry.ParseArgs<TActionArgs, TCtx>(ActionId, namedArgs, ctx);
            }

            return default;
        }
    }
}
