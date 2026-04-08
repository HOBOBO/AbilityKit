using System;

namespace AbilityKit.Ability
{
    /// <summary>
    /// 管线追踪事件记录
    /// </summary>
    public readonly struct PipelineTraceEvent
    {
        public readonly int Seq;
        public readonly PipelineTraceEventType Type;
        public readonly AbilityPipelinePhaseId PhaseId;
        public readonly EAbilityPipelineState State;
        public readonly string Message;
        public readonly DateTime UtcTime;

        public PipelineTraceEvent(int seq, PipelineTraceEventType type, AbilityPipelinePhaseId phaseId, EAbilityPipelineState state, string message)
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
