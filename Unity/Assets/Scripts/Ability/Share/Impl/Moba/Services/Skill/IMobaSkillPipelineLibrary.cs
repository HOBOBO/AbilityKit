using System.Collections.Generic;
using AbilityKit.Ability;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public interface IMobaSkillPipelineLibrary
    {
        bool TryGet(
            int skillId,
            out IAbilityPipelineConfig preCastConfig,
            out IReadOnlyList<IAbilityPipelinePhase> preCastPhases,
            out IAbilityPipelineConfig castConfig,
            out IReadOnlyList<IAbilityPipelinePhase> castPhases);
    }
}
