using AbilityKit.Ability;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.Share.ECS;
using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class DefaultMobaSkillPipelineLibrary : IMobaSkillPipelineLibrary
    {
        private readonly IWorldServices _services;
        private readonly IFrameTime _time;
        private readonly IEventBus _eventBus;
        private readonly IUnitResolver _units;

        public DefaultMobaSkillPipelineLibrary(IWorldServices services, IFrameTime time, IEventBus eventBus, IUnitResolver units)
        {
            _services = services;
            _time = time;
            _eventBus = eventBus;
            _units = units;
        }

        public bool TryGet(
            int skillId,
            out IAbilityPipelineConfig preCastConfig,
            out IReadOnlyList<IAbilityPipelinePhase> preCastPhases,
            out IAbilityPipelineConfig castConfig,
            out IReadOnlyList<IAbilityPipelinePhase> castPhases)
        {
            if (skillId <= 0)
            {
                preCastConfig = null;
                preCastPhases = null;
                castConfig = null;
                castPhases = null;
                return false;
            }

            preCastConfig = new AbilityKit.Ability.Share.Impl.Pipeline.Skill.SkillPipelineConfig(skillId * 10 + 1, $"Skill_{skillId}_PreCast");
            var prePhaseId = AbilityPipelinePhaseIdManager.Instance.Register("precast.check");
            preCastPhases = new IAbilityPipelinePhase[]
            {
                new SkillPreCastCheckPhase(prePhaseId, _ => true),
            };

            castConfig = new AbilityKit.Ability.Share.Impl.Pipeline.Skill.SkillPipelineConfig(skillId * 10 + 2, $"Skill_{skillId}_Cast");
            var castPhaseId = AbilityPipelinePhaseIdManager.Instance.Register("skill.cast");
            castPhases = new IAbilityPipelinePhase[]
            {
                new SkillCastApplyEffectPhase(castPhaseId, _services, _time, _eventBus, _units),
            };
            return true;
        }

        public void Dispose()
        {
        }

    }
}
