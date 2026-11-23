using System;
using AbilityKit.Pipeline;

namespace AbilityKit.Demo.Moba.Services
{
    public abstract class SkillRollbackPhaseBase : AbilityPipelinePhaseBase<SkillPipelineContext>,
        IAbilityPhaseRollbackState
    {
        protected SkillRollbackPhaseBase(AbilityPipelinePhaseId phaseId) : base(phaseId) { }

        public virtual PipelinePhaseRollbackState CaptureRollbackState()
        {
            return new PipelinePhaseRollbackState
            {
                PhaseId = PhaseId.Value,
                PhaseType = GetType().FullName ?? GetType().Name,
                IsComplete = IsComplete
            };
        }

        public virtual void ValidateRollbackState(PipelinePhaseRollbackState state)
        {
            if (state == null || state.PhaseId != PhaseId.Value ||
                state.PhaseType != (GetType().FullName ?? GetType().Name))
                throw new InvalidOperationException("Skill phase definition changed.");
        }

        public virtual void RestoreRollbackState(PipelinePhaseRollbackState state)
        {
            ValidateRollbackState(state);
            SetComplete(state.IsComplete);
        }
    }
}
