using System;

using AbilityKit.Pipeline;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>Value-only state for one skill phase instance.</summary>
    public sealed class PipelinePhaseRollbackState : PipelinePhaseProgress<PipelinePhaseRollbackState>
    {
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

        public PipelineRunProgress<PipelinePhaseRollbackState> ToProgress()
        {
            if (Version != CurrentVersion) throw new InvalidOperationException("Invalid skill run state version.");
            return new PipelineRunProgress<PipelinePhaseRollbackState>(ConfigId, ConfigName,
                CurrentPhaseIndex, HasCurrentPhase, IsPaused, IsCancelled, Phases);
        }
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
