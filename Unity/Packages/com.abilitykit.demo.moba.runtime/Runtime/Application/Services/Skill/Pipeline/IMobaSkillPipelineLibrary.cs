using System.Collections.Generic;
using AbilityKit.Ability.World.Services;
using AbilityKit.Pipeline;

namespace AbilityKit.Demo.Moba.Services
{
    using AbilityKit.Ability;

    public readonly struct MobaSkillPipelinePrewarmResult
    {
        public MobaSkillPipelinePrewarmResult(int requestedCount, int warmedCount, int failedCount)
        {
            RequestedCount = requestedCount;
            WarmedCount = warmedCount;
            FailedCount = failedCount;
        }

        public int RequestedCount { get; }
        public int WarmedCount { get; }
        public int FailedCount { get; }
        public bool Succeeded => FailedCount == 0;
    }

    public interface IMobaSkillPipelineLibrary : IService
    {
        int CachedSkillCount { get; }

        bool IsCached(int skillId);

        MobaSkillPipelinePrewarmResult PrewarmAll();

        MobaSkillPipelinePrewarmResult Prewarm(IReadOnlyList<int> skillIds);

        bool TryGet(
            int skillId,
            out IAbilityPipelineConfig preCastConfig,
            out IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> preCastPhases,
            out IAbilityPipelineConfig castConfig,
            out IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> castPhases);
    }
}
