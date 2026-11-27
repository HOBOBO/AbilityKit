using System;

namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 帧边界的有序调度进度和下一个执行 ID，不包含定义或执行绑定。
    /// 构造后，调用方在预检和恢复期间不得修改传入的数组。
    /// </summary>
    public readonly struct TriggerExecutionProgress
    {
        /// <summary>
        /// 使用调用方已有的独立进度数组，不另行复制。
        /// </summary>
        public TriggerExecutionProgress(long nextExecutionId, TriggerScheduledExecutionInfo[] entries)
        {
            NextExecutionId = nextExecutionId;
            Entries = entries ?? Array.Empty<TriggerScheduledExecutionInfo>();
        }

        /// <summary>
        /// 此帧之后分配的下一个执行 ID。
        /// </summary>
        public long NextExecutionId { get; }

        /// <summary>
        /// 捕获时按调度顺序排列的进度，包含仍保留的终态。
        /// </summary>
        public TriggerScheduledExecutionInfo[] Entries { get; }
    }
}
