using System;

namespace AbilityKit.Triggering.Runtime.Schedule
{
    /// <summary>
    /// 调度项状态
    /// </summary>
    public enum EScheduleItemState : byte
    {
        /// <summary>已注册，等待首次执行</summary>
        Registered = 0,

        /// <summary>等待延迟结束</summary>
        WaitingDelay = 1,

        /// <summary>等待条件满足</summary>
        WaitingCondition = 2,

        /// <summary>执行中</summary>
        Running = 3,

        /// <summary>已暂停</summary>
        Paused = 4,

        /// <summary>已完成（达到最大执行次数）</summary>
        Completed = 5,

        /// <summary>已中断</summary>
        Interrupted = 6,

        /// <summary>已终止（完成或中断）</summary>
        Terminated = 7,
    }

    /// <summary>
    /// 调度模式
    /// </summary>
    public enum EScheduleMode : byte
    {
        /// <summary>立即执行一次</summary>
        Immediate = 0,

        /// <summary>延迟后执行一次</summary>
        Delayed = 1,

        /// <summary>周期性执行</summary>
        Periodic = 2,

        /// <summary>持续执行（由外部控制终止）</summary>
        Continuous = 3,
    }
}
