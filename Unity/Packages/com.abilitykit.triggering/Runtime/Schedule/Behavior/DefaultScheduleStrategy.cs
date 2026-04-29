using AbilityKit.Triggering.Runtime.Schedule.Data;

namespace AbilityKit.Triggering.Runtime.Schedule.Behavior
{
    /// <summary>
    /// 默认调度策略
    /// 实现标准的周期/延迟调度行为
    /// </summary>
    public sealed class DefaultScheduleStrategy : IScheduleStrategy
    {
        public bool OnUpdate(ref ScheduleItemData item, float deltaTimeMs, IScheduleExecutor executor)
        {
            // 计算缩放后的时间增量
            float scaledDelta = deltaTimeMs * item.Speed;
            item.ElapsedMs += scaledDelta;

            // 状态机转换
            switch (item.State)
            {
                case EScheduleItemState.Registered:
                    return HandleRegistered(ref item, scaledDelta);

                case EScheduleItemState.WaitingDelay:
                    return HandleWaitingDelay(ref item);

                case EScheduleItemState.Running:
                    return HandleRunning(ref item, scaledDelta, deltaTimeMs, executor);

                case EScheduleItemState.Paused:
                case EScheduleItemState.Completed:
                case EScheduleItemState.Interrupted:
                case EScheduleItemState.Terminated:
                    return item.IsTerminated;
            }

            return item.IsTerminated;
        }

        public ScheduleContext CreateContext(in ScheduleItemData item, float deltaTimeMs, float scaledDeltaMs)
        {
            return ScheduleContext.Create(item, deltaTimeMs, scaledDeltaMs);
        }

        /// <summary>
        /// 处理 Registered 状态
        /// </summary>
        private bool HandleRegistered(ref ScheduleItemData item, float scaledDelta)
        {
            // 有延迟则进入等待状态
            if (item.DelayMs > 0)
            {
                item.State = EScheduleItemState.WaitingDelay;
                return false;
            }

            // 无延迟直接进入运行状态
            item.State = EScheduleItemState.Running;
            item.ElapsedMs = 0;
            return false;
        }

        /// <summary>
        /// 处理 WaitingDelay 状态
        /// </summary>
        private bool HandleWaitingDelay(ref ScheduleItemData item)
        {
            // 延迟结束，进入运行状态
            if (item.ElapsedMs >= item.DelayMs)
            {
                item.State = EScheduleItemState.Running;
                item.ElapsedMs = 0;
            }
            return false;
        }

        /// <summary>
        /// 处理 Running 状态
        /// </summary>
        private bool HandleRunning(
            ref ScheduleItemData item,
            float scaledDelta,
            float deltaTimeMs,
            IScheduleExecutor executor)
        {
            // 周期性执行
            if (item.IntervalMs > 0)
            {
                float timeSinceLast = item.ElapsedMs - item.LastExecuteMs;
                if (timeSinceLast >= item.IntervalMs)
                {
                    // 创建上下文并执行
                    var context = CreateContext(item, deltaTimeMs, scaledDelta);

                    if (executor.TryExecute(item, context))
                    {
                        item.ExecutionCount++;
                        item.LastExecuteMs = item.ElapsedMs;
                    }

                    // 检查是否完成
                    if (item.IsMaxExecutionsReached)
                    {
                        item.State = EScheduleItemState.Completed;
                        return true;
                    }
                }
            }
            // 一次性执行（无间隔）
            else if (item.ExecutionCount == 0)
            {
                var context = CreateContext(item, deltaTimeMs, scaledDelta);
                executor.TryExecute(item, context);
                item.ExecutionCount++;
                item.State = EScheduleItemState.Completed;
                return true;
            }

            return item.IsTerminated;
        }
    }
}
