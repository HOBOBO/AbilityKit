using System;

namespace AbilityKit.Triggering.Runtime.Schedule.Data
{
    /// <summary>
    /// 调度项数据（纯数据结构）
    /// 框架层定义，不依赖任何运行时实现
    /// </summary>
    public struct ScheduleItemData
    {
        // === 标识 ===
        /// <summary>调度句柄（用于外部引用）</summary>
        public ScheduleHandle Handle;

        /// <summary>业务对象ID（关联具体业务对象，如 BuffId、子弹Id）</summary>
        public int BusinessId;

        /// <summary>关联的 Trigger ID（用于分组管理）</summary>
        public int TriggerId;

        // === 状态 ===
        /// <summary>当前状态</summary>
        public EScheduleItemState State;

        /// <summary>调度模式</summary>
        public EScheduleMode Mode;

        // === 时间控制 ===
        /// <summary>延迟时间（毫秒），首次执行前等待</summary>
        public float DelayMs;

        /// <summary>间隔时间（毫秒），周期性执行间隔</summary>
        public float IntervalMs;

        /// <summary>速度倍率（1.0 = 正常速度）</summary>
        public float Speed;

        /// <summary>从启动至今的总消耗时间（毫秒）</summary>
        public float ElapsedMs;

        /// <summary>上次执行时间（毫秒）</summary>
        public float LastExecuteMs;

        // === 执行计数 ===
        /// <summary>已执行次数</summary>
        public int ExecutionCount;

        /// <summary>最大执行次数，-1表示无限</summary>
        public int MaxExecutions;

        // === 控制标志 ===
        /// <summary>是否可以被打断</summary>
        public bool CanBeInterrupted;

        /// <summary>中断原因</summary>
        public string InterruptReason;

        // === 计算属性 ===
        /// <summary>是否是周期性执行</summary>
        public bool IsPeriodic => MaxExecutions < 0 || MaxExecutions > 1;

        /// <summary>是否达到最大执行次数</summary>
        public bool IsMaxExecutionsReached => MaxExecutions > 0 && ExecutionCount >= MaxExecutions;

        /// <summary>是否活跃（可以被更新）</summary>
        public bool IsActive => State != EScheduleItemState.Completed
                            && State != EScheduleItemState.Interrupted
                            && State != EScheduleItemState.Terminated;

        /// <summary>是否暂停</summary>
        public bool IsPaused => State == EScheduleItemState.Paused;

        /// <summary>是否终止</summary>
        public bool IsTerminated => State == EScheduleItemState.Completed
                                || State == EScheduleItemState.Interrupted
                                || State == EScheduleItemState.Terminated;
    }
}
