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
        private static readonly AbilityPipelinePhaseId PreCastPhaseId = new AbilityPipelinePhaseId("precast.check");
        private static readonly AbilityPipelinePhaseId CastPhaseId = new AbilityPipelinePhaseId("skill.cast");

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
            out IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> preCastPhases,
            out IAbilityPipelineConfig castConfig,
            out IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> castPhases)
        {
            if (skillId <= 0)
            {
                preCastConfig = null;
                preCastPhases = null;
                castConfig = null;
                castPhases = null;
                return false;
            }

            preCastConfig = new AbilityKit.Ability.Share.Impl.Pipeline.Skill.SkillPipelineConfig((skillId << 1) | 0, $"Skill_{skillId}_PreCast");
            preCastPhases = new IAbilityPipelinePhase<SkillPipelineContext>[]
            {
                new SkillPreCastCheckPhase(PreCastPhaseId, _ => true),
            };

            castConfig = new AbilityKit.Ability.Share.Impl.Pipeline.Skill.SkillPipelineConfig((skillId << 1) | 1, $"Skill_{skillId}_Cast");
            castPhases = new IAbilityPipelinePhase<SkillPipelineContext>[]
            {
                new SkillCastApplyEffectPhase(CastPhaseId, _services, _time, _eventBus, _units),
            };
            return true;
        }

        public void Dispose()
        {
        }

    }
}
