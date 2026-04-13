using System.Collections.Generic;
using AbilityKit.Core.Generic;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Demo.Moba.Services
{
    using AbilityKit.Ability;
    public interface IMobaSkillPipelineLibrary : IService
    {
        bool TryGet(
            int skillId,
            out IAbilityPipelineConfig preCastConfig,
            out IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> preCastPhases,
            out IAbilityPipelineConfig castConfig,
            out IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> castPhases);
    }
}
