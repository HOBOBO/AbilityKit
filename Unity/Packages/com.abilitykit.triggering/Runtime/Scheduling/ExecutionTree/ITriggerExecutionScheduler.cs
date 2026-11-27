using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 执行树节点请求登记子树的宿主接口，不负责应用的帧驱动或快照编解码。
    /// </summary>
    public interface ITriggerExecutionScheduler
    {
        /// <summary>
        /// 提交子树、载荷和上下文，以及调度方式、毫秒间隔、次数上限和中断选项。
        /// </summary>
        TriggerScheduledExecutionHandle Schedule<TCtx>(
            ITriggerPlanExecutable executable,
            object args,
            in ExecCtx<TCtx> context,
            EScheduleMode mode,
            float intervalMs,
            int maxExecutions,
            bool canBeInterrupted)
            where TCtx : class;
    }
}
