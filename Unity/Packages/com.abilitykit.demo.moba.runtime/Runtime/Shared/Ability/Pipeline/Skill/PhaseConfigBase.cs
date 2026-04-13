using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Impl.Pipeline.Skill
{
    public abstract class PhaseConfigBase : IAbilityPhaseConfig
    {
        public AbilityPipelinePhaseId PhaseId { get; }
        public string PhaseType { get; }
        public float Duration { get; }
        public Dictionary<string, object> Parameters { get; } = new Dictionary<string, object>();

        protected PhaseConfigBase(AbilityPipelinePhaseId phaseId, string phaseType, float duration = 0)
        {
            PhaseId = phaseId;
            PhaseType = phaseType;
            Duration = duration;
        }
    }
}
