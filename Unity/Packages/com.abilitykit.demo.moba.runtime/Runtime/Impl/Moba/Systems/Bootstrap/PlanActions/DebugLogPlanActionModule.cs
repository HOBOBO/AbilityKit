using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    /// <summary>
    /// 调试日志Plan Action模块
    /// 演示使用基类API（多Action场景）
    /// </summary>
    [PlanActionModule(order: 0)]
    public sealed class DebugLogPlanActionModule : PlanActionModuleBase
    {
        protected override string ActionName => TriggeringConstants.Actions.DebugLog;

        /// <summary>
        /// 同时支持无参数和双参数
        /// </summary>
        protected override bool HasAction0 => true;
        protected override bool HasAction2 => true;

        /// <summary>
        /// 无参数版本：简单打印上下文信息
        /// </summary>
        protected override void Execute0(object args, ExecCtx<IWorldResolver> ctx)
        {
            var ctxType = ctx.Context != null ? ctx.Context.GetType().Name : "<null>";
            var argsType = args != null ? args.GetType().Name : "<null>";
            Log.Info($"[Plan] debug_log executed. argsType={argsType}, ctxType={ctxType}");
        }

        /// <summary>
        /// 双参数版本：打印指定消息
        /// </summary>
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
