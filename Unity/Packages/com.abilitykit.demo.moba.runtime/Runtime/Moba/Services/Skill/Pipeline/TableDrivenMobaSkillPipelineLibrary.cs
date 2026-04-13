using System;
using System.Collections.Generic;
using AbilityKit.Core.Generic;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;

namespace AbilityKit.Demo.Moba.Services
{
    using AbilityKit.Ability;
    public sealed class TableDrivenMobaSkillPipelineLibrary : IMobaSkillPipelineLibrary
    {
        private static readonly AbilityPipelinePhaseId PreCastChecksPhaseId = new AbilityPipelinePhaseId("skill.checks.precast");
        private static readonly AbilityPipelinePhaseId PreCastTimelinePhaseId = new AbilityPipelinePhaseId("skill.timeline.precast");
        private static readonly AbilityPipelinePhaseId CastChecksPhaseId = new AbilityPipelinePhaseId("skill.checks.cast");
        private static readonly AbilityPipelinePhaseId CastTimelinePhaseId = new AbilityPipelinePhaseId("skill.timeline.cast");

        private readonly MobaConfigDatabase _configs;
        private readonly MobaEffectInvokerService _effects;
        private readonly SkillConditionRegistry _conditionRegistry;

        public TableDrivenMobaSkillPipelineLibrary(
            MobaConfigDatabase configs,
            MobaEffectInvokerService effects,
            SkillConditionRegistry conditionRegistry = null)
        {
            _configs = configs;
            _effects = effects;
            _conditionRegistry = conditionRegistry;
        }

        public bool TryGet(
            int skillId,
            out IAbilityPipelineConfig preCastConfig,
            out IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> preCastPhases,
            out IAbilityPipelineConfig castConfig,
            out IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> castPhases)
        {
            preCastConfig = null;
            preCastPhases = null;
            castConfig = null;
            castPhases = null;

            if (skillId <= 0) return false;
            if (_configs == null) return false;

            if (!_configs.TryGetSkill(skillId, out var skill) || skill == null) return false;

            if (skill.PreCastFlowId > 0 && _configs.TryGetSkillFlow(skill.PreCastFlowId, out var preFlow) && preFlow != null)
            {
                preCastConfig = new AbilityKit.Ability.Share.Impl.Pipeline.Skill.SkillPipelineConfig((skillId << 2) | 0, $"Skill_{skillId}_PreCast");
                preCastPhases = BuildFlowPhases(preFlow, checksPhaseId: PreCastChecksPhaseId, timelinePhaseId: PreCastTimelinePhaseId);
            }

            if (skill.CastFlowId <= 0) return false;
            if (!_configs.TryGetSkillFlow(skill.CastFlowId, out var castFlow) || castFlow == null) return false;

            castConfig = new AbilityKit.Ability.Share.Impl.Pipeline.Skill.SkillPipelineConfig((skillId << 2) | 1, $"Skill_{skillId}_Cast");
            castPhases = BuildFlowPhases(castFlow, checksPhaseId: CastChecksPhaseId, timelinePhaseId: CastTimelinePhaseId);

            return true;
        }

        private IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> BuildFlowPhases(
            SkillFlowMO flow,
            AbilityPipelinePhaseId checksPhaseId,
            AbilityPipelinePhaseId timelinePhaseId)
        {
            if (flow == null || flow.Phases == null || flow.Phases.Count == 0)
            {
                return System.Array.Empty<IAbilityPipelinePhase<SkillPipelineContext>>();
            }

            var list = new List<IAbilityPipelinePhase<SkillPipelineContext>>(flow.Phases.Count);
            for (int i = 0; i < flow.Phases.Count; i++)
            {
                var p = flow.Phases[i];
                if (p == null) continue;

                var type = (SkillPhaseType)p.Type;
                switch (type)
                {
                    case SkillPhaseType.Checks:
                        list.Add(new SkillFlowChecksPhase(checksPhaseId, p.Checks, _conditionRegistry));
                        break;
                    case SkillPhaseType.Timeline:
                        if (p.Timeline == null) break;
                        list.Add(new SkillTimelinePhase(timelinePhaseId, p.Timeline.DurationMs, ToArray(p.Timeline.Events), _effects));
                        break;
                }
            }

            return list;
        }

        private static SkillTimelineEventDTO[] ToArray(IReadOnlyList<SkillTimelineEventDTO> list)
        {
            if (list == null || list.Count == 0) return System.Array.Empty<SkillTimelineEventDTO>();
            var arr = new SkillTimelineEventDTO[list.Count];
            for (int i = 0; i < list.Count; i++) arr[i] = list[i];
            return arr;
        }

        public void Dispose()
        {
        }
    }
}
