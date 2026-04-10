using System;
using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    public abstract class PlanActionModuleBase : IPlanActionModule
    {
        /// <summary>
        /// Action名称，需要子类实现
        /// </summary>
        protected abstract string ActionName { get; }

        /// <summary>
        /// 是否注册无参数Action
        /// </summary>
        protected virtual bool HasAction0 => false;

        /// <summary>
        /// 是否注册单参数Action
        /// </summary>
        protected virtual bool HasAction1 => false;

        /// <summary>
        /// 是否注册双参数Action
        /// </summary>
        protected virtual bool HasAction2 => false;

        public void Register(ActionRegistry actions, IWorldResolver services)
        {
            if (actions == null || string.IsNullOrEmpty(ActionName))
                return;

            var id = TriggeringConstants.GetActionId(ActionName);
            var actionName = ActionName;

            if (HasAction0)
            {
                actions.Register<PlannedTrigger<object, IWorldResolver>.Action0>(
                    id,
                    CreateHandler0(actionName),
                    isDeterministic: true);
            }

            if (HasAction1)
            {
                actions.Register<PlannedTrigger<object, IWorldResolver>.Action1>(
                    id,
                    CreateHandler1(actionName),
                    isDeterministic: true);
            }

            if (HasAction2)
            {
                actions.Register<PlannedTrigger<object, IWorldResolver>.Action2>(
                    id,
                    CreateHandler2(actionName),
                    isDeterministic: true);
            }
        }

        private PlannedTrigger<object, IWorldResolver>.Action0 CreateHandler0(string actionName)
        {
            return (args, ctx) =>
            {
                try
                {
                    if (ctx.Context == null) return;
                    Execute0(args, ctx);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[Plan] {actionName} executed failed");
                }
            };
        }

        private PlannedTrigger<object, IWorldResolver>.Action1 CreateHandler1(string actionName)
        {
            return (args, a0, ctx) =>
            {
                try
                {
                    if (ctx.Context == null) return;
                    Execute1(args, a0, ctx);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[Plan] {actionName} executed failed");
                }
            };
        }

        private PlannedTrigger<object, IWorldResolver>.Action2 CreateHandler2(string actionName)
        {
            return (args, a0, a1, ctx) =>
            {
                try
                {
                    if (ctx.Context == null) return;
                    Execute2(args, a0, a1, ctx);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[Plan] {actionName} executed failed");
                }
            };
        }

        /// <summary>
        /// 无参数Action的执行逻辑
        /// </summary>
        protected virtual void Execute0(object args, ExecCtx<IWorldResolver> ctx) { }

        /// <summary>
        /// 单参数Action的执行逻辑
        /// </summary>
        protected virtual void Execute1(object args, double a0, ExecCtx<IWorldResolver> ctx) { }

        /// <summary>
        /// 双参数Action的执行逻辑
        /// </summary>
        protected virtual void Execute2(object args, double a0, double a1, ExecCtx<IWorldResolver> ctx) { }
    }

    /// <summary>
    /// 简化版的PlanActionModule，直接使用常量定义Action
    /// 适合只有一个Action的场景
    /// </summary>
    public abstract class SimplePlanActionModuleBase : IPlanActionModule
    {
        /// <summary>
        /// Action ID，从TriggeringConstants获取
        /// </summary>
        protected abstract ActionId ActionId { get; }

        /// <summary>
        /// Action参数个数
        /// </summary>
        protected abstract int Arity { get; }

        public void Register(ActionRegistry actions, IWorldResolver services)
        {
            if (actions == null || ActionId.Value == 0)
                return;

            switch (Arity)
            {
                case 0:
                    actions.Register<PlannedTrigger<object, IWorldResolver>.Action0>(
                        ActionId,
                        CreateHandler0(),
                        isDeterministic: true);
                    break;
                case 1:
                    actions.Register<PlannedTrigger<object, IWorldResolver>.Action1>(
                        ActionId,
                        CreateHandler1(),
                        isDeterministic: true);
                    break;
                case 2:
                    actions.Register<PlannedTrigger<object, IWorldResolver>.Action2>(
                        ActionId,
                        CreateHandler2(),
                        isDeterministic: true);
                    break;
            }
        }

        protected virtual PlannedTrigger<object, IWorldResolver>.Action0 CreateHandler0()
        {
            return (args, ctx) =>
            {
                try
                {
                    if (ctx.Context == null) return;
                    Execute0(args, ctx);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[Plan] Action {ActionId.Value} executed failed");
                }
            };
        }

        protected virtual PlannedTrigger<object, IWorldResolver>.Action1 CreateHandler1()
        {
            return (args, a0, ctx) =>
            {
                try
                {
                    if (ctx.Context == null) return;
                    Execute1(args, a0, ctx);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[Plan] Action {ActionId.Value} executed failed");
                }
            };
        }

        protected virtual PlannedTrigger<object, IWorldResolver>.Action2 CreateHandler2()
        {
            return (args, a0, a1, ctx) =>
            {
                try
                {
                    if (ctx.Context == null) return;
                    Execute2(args, a0, a1, ctx);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[Plan] Action {ActionId.Value} executed failed");
                }
            };
        }

        protected virtual void Execute0(object args, ExecCtx<IWorldResolver> ctx) { }
        protected virtual void Execute1(object args, double a0, ExecCtx<IWorldResolver> ctx) { }
        protected virtual void Execute2(object args, double a0, double a1, ExecCtx<IWorldResolver> ctx) { }
    }
}
