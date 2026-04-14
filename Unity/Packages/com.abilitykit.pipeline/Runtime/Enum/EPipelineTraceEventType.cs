using System;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 管线追踪事件类型
    /// </summary>
    public enum EPipelineTraceEventType
    {
        /// <summary>
        /// 管线开始
        /// </summary>
        RunStart = 0,
        /// <summary>
        /// 管线结束
        /// </summary>
        RunEnd = 1,
        /// <summary>
        /// 阶段开始
        /// </summary>
        PhaseStart = 2,
        /// <summary>
        /// 阶段完成
        /// </summary>
        PhaseComplete = 3,
        /// <summary>
        /// 阶段错误
        /// </summary>
        PhaseError = 4,
        /// <summary>
        /// 每帧 Tick
        /// </summary>
        Tick = 5,
        /// <summary>
        /// 管线暂停
        /// </summary>
        Pause = 6,
        /// <summary>
        /// 管线恢复
        /// </summary>
        Resume = 7,
        /// <summary>
        /// 管线中断
        /// </summary>
        Interrupt = 8,
    }
}
