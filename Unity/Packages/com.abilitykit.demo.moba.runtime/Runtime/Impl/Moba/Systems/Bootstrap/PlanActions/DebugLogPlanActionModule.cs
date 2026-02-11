using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    [PlanActionModule(order: 0)]
    public sealed class DebugLogPlanActionModule : PlanActionModuleBase
    {
        protected override string ActionName => "debug_log";

        protected override bool HasAction0 => true;
        protected override bool HasAction2 => true;

        protected override void Execute0(object args, ExecCtx<IWorldResolver> ctx)
        {
            var ctxType = ctx.Context != null ? ctx.Context.GetType().Name : "<null>";
            var argsType = args != null ? args.GetType().Name : "<null>";
            Log.Info($"[Plan] debug_log executed. argsType={argsType}, ctxType={ctxType}");
        }

        protected override void Execute2(object args, double a0, double a1, ExecCtx<IWorldResolver> ctx)
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
        }
    }
}
