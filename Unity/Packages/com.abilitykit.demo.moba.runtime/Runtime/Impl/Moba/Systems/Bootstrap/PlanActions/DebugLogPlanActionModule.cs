using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    [PlanActionModule(order: 0)]
    public sealed class DebugLogPlanActionModule : IPlanActionModule
    {
        public void Register(ActionRegistry actions, IWorldResolver services)
        {
            if (actions == null) return;

            var debugLogId = new ActionId(AbilityKit.Triggering.Eventing.StableStringId.Get("action:debug_log"));
            actions.Register<PlannedTrigger<object, IWorldResolver>.Action0>(
                debugLogId,
                static (args, ctx) =>
                {
                    var ctxType = ctx.Context != null ? ctx.Context.GetType().Name : "<null>";
                    var argsType = args != null ? args.GetType().Name : "<null>";
                    Log.Info($"[Plan] debug_log executed. argsType={argsType}, ctxType={ctxType}");
                },
                isDeterministic: true);

            actions.Register<PlannedTrigger<object, IWorldResolver>.Action2>(
                debugLogId,
                static (args, a0, a1, ctx) =>
                {
                    var msgId = (int)a0;
                    var dump = a1 >= 0.5;
                    var msg = string.Empty;
                    if (ctx.Context != null && ctx.Context.TryResolve<TriggerPlanJsonDatabase>(out var db) && db != null)
                    {
                        if (!db.TryGetString(msgId, out msg)) msg = string.Empty;
                    }
                    Log.Info($"[Plan] debug_log: {msg}");
                    if (dump)
                    {
                        var argsType = args != null ? args.GetType().Name : "<null>";
                        var ctxType = ctx.Context != null ? ctx.Context.GetType().Name : "<null>";
                        Log.Info($"[Plan] debug_log dump. argsType={argsType}, ctxType={ctxType}");
                    }
                },
                isDeterministic: true);
        }
    }
}
