using System;

using AbilityKit.Pipeline;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>Value-only state for one skill phase instance.</summary>
    public sealed class PipelinePhaseRollbackState
    {
        public string PhaseId { get; set; } = string.Empty;
        public string PhaseType { get; set; } = string.Empty;
        public bool IsComplete { get; set; }
        public int[] Integers { get; set; } = Array.Empty<int>();
        public long[] Longs { get; set; } = Array.Empty<long>();
        public double[] Numbers { get; set; } = Array.Empty<double>();
        public PipelinePhaseRollbackState[] Children { get; set; } = Array.Empty<PipelinePhaseRollbackState>();
    }

    internal sealed class PipelineRunRollbackState
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;
        public int ConfigId { get; set; }
        public string ConfigName { get; set; } = string.Empty;
        public int CurrentPhaseIndex { get; set; }
        public bool HasCurrentPhase { get; set; }
        public bool IsPaused { get; set; }
        public bool IsCancelled { get; set; }
        public PipelinePhaseRollbackState[] Phases { get; set; } = Array.Empty<PipelinePhaseRollbackState>();
    }

    /// <summary>Implemented by phase instances whose progress can survive a rollback.</summary>
    public interface IAbilityPhaseRollbackState
    {
        PipelinePhaseRollbackState CaptureRollbackState();
        void ValidateRollbackState(PipelinePhaseRollbackState state);
        void RestoreRollbackState(PipelinePhaseRollbackState state);
    }

    /// <summary>Rebuilds ephemeral bindings after a run is restored from detached state.</summary>
    internal interface IAbilityPhaseRollbackRebind<TCtx> where TCtx : IAbilityPipelineContext
    {
        void RebindAfterRollback(TCtx context);
    }

    /// <summary>Releases transient bindings without publishing gameplay completion events.</summary>
    internal interface IAbilityPhaseRollbackDetach
    {
        void DetachForRollback();
    }

}
