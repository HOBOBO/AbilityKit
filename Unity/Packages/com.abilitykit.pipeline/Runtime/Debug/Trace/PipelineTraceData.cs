using System;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 管线追踪事件数据
    /// </summary>
    public readonly struct PipelineTraceData
    {
        public readonly int Sequence;
        public readonly EPipelineTraceEventType Type;
        public readonly AbilityPipelinePhaseId PhaseId;
        public readonly EAbilityPipelineState State;
        public readonly string Message;
        public readonly DateTime UtcTime;

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
    /// 管线追踪事件记录
    /// </summary>
    public readonly struct PipelineTraceEvent
    {
        public readonly int Seq;
        public readonly EPipelineTraceEventType Type;
        public readonly AbilityPipelinePhaseId PhaseId;
        public readonly EAbilityPipelineState State;
        public readonly string Message;
        public readonly DateTime UtcTime;

        public PipelineTraceEvent(int seq, EPipelineTraceEventType type, AbilityPipelinePhaseId phaseId, EAbilityPipelineState state, string message)
        {
            Seq = seq;
            Type = type;
            PhaseId = phaseId;
            State = state;
            Message = message ?? string.Empty;
            UtcTime = DateTime.UtcNow;
        }

        public override string ToString()
        {
            return $"[{Seq}] {Type} State={State} Phase={PhaseId} {Message}";
        }
    }
}
