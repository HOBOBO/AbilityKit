using System.Collections.Generic;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 管线追踪记录器接口
    /// Runtime 使用 NoOp 实现，Editor 使用完整实现
    /// </summary>
    public interface IPipelineTraceRecorder
    {
        /// <summary>
        /// 是否启用追踪
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// 记录追踪数据
        /// </summary>
        void Record(IPipelineLifeOwner owner, PipelineTraceData data);

        /// <summary>
        /// 获取指定拥有者的追踪记录
        /// </summary>
        IPipelineRunTrace GetTrace(int ownerId);
    }

    /// <summary>
    /// 管线追踪记录接口（Ring Buffer 实现）
    /// </summary>
    public interface IPipelineRunTrace
    {
        /// <summary>
        /// 容量
        /// </summary>
        int Capacity { get; }

        /// <summary>
        /// 当前记录数量
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 添加追踪事件
        /// </summary>
        void Add(EPipelineTraceEventType type, AbilityPipelinePhaseId phaseId, EAbilityPipelineState state, string message);
    }
}
