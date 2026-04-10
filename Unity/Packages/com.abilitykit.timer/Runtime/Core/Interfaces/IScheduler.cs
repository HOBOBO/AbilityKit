using System;

namespace AbilityKit.Timer
{
    /// <summary>
    /// 调度器接口。
    /// 负责管理一组调度任务的生命周期。
    ///
    /// 设计原则：
    /// - 零 GC：Tick 返回值不分配堆内存
    /// - 批量处理：支持批量更新多个任务
    /// - 可查询：提供任务检索能力
    /// </summary>
    public interface IScheduler
    {
        /// <summary>调度器名称</summary>
        string Name { get; set; }

        /// <summary>当前任务数量</summary>
        int Count { get; }

        /// <summary>添加延时任务</summary>
        IScheduledTask ScheduleDelay(Action callback, float delaySeconds);

        /// <summary>添加周期任务</summary>
        IScheduledTask SchedulePeriodic(Action callback, float periodSeconds, float durationSeconds = -1, int maxExecutions = -1);

        /// <summary>添加持续任务（外部控制结束）</summary>
        IScheduledTask ScheduleContinuous(Action<float> onTick, Action onComplete = null, float durationSeconds = -1);

        /// <summary>取消所有任务</summary>
        void CancelAll();

        /// <summary>按名称取消任务</summary>
        void CancelByName(string name);

        /// <summary>更新所有任务</summary>
        void Tick(float deltaTime);
    }
}
