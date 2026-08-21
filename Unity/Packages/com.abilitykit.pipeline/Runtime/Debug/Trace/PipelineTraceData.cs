using System;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 管线追踪事件数据。
    /// </summary>
    public readonly struct PipelineTraceData
    {
        /// <summary>
        /// 事件序号。
        /// </summary>
        public readonly int Sequence;

        /// <summary>
        /// 事件类型。
        /// </summary>
        public readonly EPipelineTraceEventType Type;

        /// <summary>
        /// 关联阶段 ID。
        /// </summary>
        public readonly AbilityPipelinePhaseId PhaseId;

        /// <summary>
        /// 管线状态。
        /// </summary>
        public readonly EAbilityPipelineState State;

        /// <summary>
        /// 事件消息。
        /// </summary>
        public readonly string Message;

        /// <summary>
        /// 协调世界时时间戳。
        /// </summary>
        public readonly DateTime UtcTime;

        /// <summary>
        /// 创建追踪数据。
        /// </summary>
        public PipelineTraceData(
            int sequence,
            EPipelineTraceEventType type,
            AbilityPipelinePhaseId phaseId,
            EAbilityPipelineState state,
            string message)
        {
            Sequence = sequence;
            Type = type;
            PhaseId = phaseId;
            State = state;
            Message = message ?? string.Empty;
            UtcTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// 管线追踪事件记录。
    /// </summary>
    public readonly struct PipelineTraceEvent
    {
        /// <summary>
        /// 事件序号。
        /// </summary>
        public readonly int Seq;

        /// <summary>
        /// 事件类型。
        /// </summary>
        public readonly EPipelineTraceEventType Type;

        /// <summary>
        /// 关联阶段 ID。
        /// </summary>
        public readonly AbilityPipelinePhaseId PhaseId;

        /// <summary>
        /// 管线状态。
        /// </summary>
        public readonly EAbilityPipelineState State;

        /// <summary>
        /// 事件消息。
        /// </summary>
        public readonly string Message;

        /// <summary>
        /// 协调世界时时间戳。
        /// </summary>
        public readonly DateTime UtcTime;

        /// <summary>
        /// 创建追踪事件记录。
        /// </summary>
        public PipelineTraceEvent(int seq, EPipelineTraceEventType type, AbilityPipelinePhaseId phaseId, EAbilityPipelineState state, string message)
            : this(seq, type, phaseId, state, message, DateTime.UtcNow)
        {
        }

        /// <summary>
        /// 使用已捕获的时间戳创建追踪事件记录。
        /// </summary>
        public PipelineTraceEvent(int seq, EPipelineTraceEventType type, AbilityPipelinePhaseId phaseId, EAbilityPipelineState state, string message, DateTime utcTime)
        {
            Seq = seq;
            Type = type;
            PhaseId = phaseId;
            State = state;
            Message = message ?? string.Empty;
            UtcTime = utcTime;
        }

        /// <summary>
        /// 转换为调试字符串。
        /// </summary>
        public override string ToString()
        {
            return $"[{Seq}] {Type} State={State} Phase={PhaseId} {Message}";
        }
    }
}
