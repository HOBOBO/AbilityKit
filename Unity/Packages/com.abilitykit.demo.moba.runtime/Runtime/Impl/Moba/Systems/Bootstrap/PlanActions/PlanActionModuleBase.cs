using System;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public abstract class PlanActionModuleBase : IPlanActionModule
    {
        protected abstract string ActionName { get; }

        protected virtual bool HasAction0 => false;
        protected virtual bool HasAction1 => false;
        protected virtual bool HasAction2 => false;

        public void Register(ActionRegistry actions, IWorldResolver services)
        {
            if (actions == null) return;
            var actionName = ActionName;
            if (string.IsNullOrEmpty(actionName)) return;

            var id = PlanActionRegisterUtil.GetActionId(actionName);

            if (HasAction0)
            {
                actions.Register<PlannedTrigger<object, IWorldResolver>.Action0>(
                    id,
                    (args, ctx) =>
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
                    },
                    isDeterministic: true);
            }

            if (HasAction1)
            {
                actions.Register<PlannedTrigger<object, IWorldResolver>.Action1>(
                    id,
                    (args, a0, ctx) =>
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
                    },
                    isDeterministic: true);
            }

            if (HasAction2)
            {
                actions.Register<PlannedTrigger<object, IWorldResolver>.Action2>(
                    id,
                    (args, a0, a1, ctx) =>
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
                    },
                    isDeterministic: true);
            }
        }

        protected virtual void Execute0(object args, ExecCtx<IWorldResolver> ctx) { }
        protected virtual void Execute1(object args, double a0, ExecCtx<IWorldResolver> ctx) { }
        protected virtual void Execute2(object args, double a0, double a1, ExecCtx<IWorldResolver> ctx) { }
    }
}
