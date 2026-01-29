#if UNITY_EDITOR

namespace AbilityKit.Ability
{
    public enum PipelineTraceEventType
    {
        RunStart = 0,
        RunEnd = 1,
        PhaseStart = 2,
        PhaseComplete = 3,
        PhaseError = 4,
        Tick = 5,
    }
}

#endif
