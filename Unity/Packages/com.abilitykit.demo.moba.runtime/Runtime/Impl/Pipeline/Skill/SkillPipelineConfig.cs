using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Impl.Pipeline.Skill
{
    public sealed class SkillPipelineConfig : IAbilityPipelineConfig
    {
        public int ConfigId { get; }
        public string ConfigName { get; }
        public IReadOnlyList<IAbilityPhaseConfig> PhaseConfigs => _phases;
        public bool AllowInterrupt { get; }
        public bool AllowPause { get; }

        private readonly List<IAbilityPhaseConfig> _phases = new List<IAbilityPhaseConfig>();

        public SkillPipelineConfig(int configId, string configName, bool allowInterrupt = true, bool allowPause = true)
        {
            ConfigId = configId;
            ConfigName = configName ?? string.Empty;
            AllowInterrupt = allowInterrupt;
            AllowPause = allowPause;
        }

        public SkillPipelineConfig AddPhase(IAbilityPhaseConfig phase)
        {
            if (phase == null) throw new ArgumentNullException(nameof(phase));
            _phases.Add(phase);
            return this;
        }
    }
}
