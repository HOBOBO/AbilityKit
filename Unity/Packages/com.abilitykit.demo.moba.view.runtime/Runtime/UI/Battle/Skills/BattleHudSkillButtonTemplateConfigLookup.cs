using System.Collections.Generic;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudSkillButtonTemplateConfigLookup
    {
        private readonly BattleViewResourceProvider _resources;
        private readonly BattleHudSkillPresentationSpecResolver _specs = new BattleHudSkillPresentationSpecResolver();

        public BattleHudSkillButtonTemplateConfigLookup(BattleViewResourceProvider resources = null)
        {
            _resources = BattleViewResourceProvider.OrDefault(resources);
        }

        public bool TryResolve(
            int skillId,
            out SkillMO skill,
            out SkillButtonTemplateMO template,
            out BattleHudSkillPresentationSpec spec,
            IReadOnlyDictionary<int, int> skillLevels = null)
        {
            skill = null;
            template = null;
            spec = default;
            if (skillId <= 0) return false;
            if (!TryGetConfigs(out var configs)) return false;
            if (!TryGetSkill(configs, skillId, out skill)) return false;

            if (skill.SkillButtonTemplateId > 0)
            {
                TryGetTemplate(configs, skill.SkillButtonTemplateId, out template);
            }

            spec = TryResolveBasicAttackRange(configs, skill, skillLevels, out var range)
                ? _specs.ResolveBasicAttack(skill, range)
                : _specs.Resolve(skill, template);
            return true;
        }

        public bool TryResolveBasicAttackRange(int skillId, IReadOnlyDictionary<int, int> skillLevels, out float range)
        {
            range = 0f;
            return TryGetConfigs(out var configs) && TryGetSkill(configs, skillId, out var skill) &&
                   TryResolveBasicAttackRange(configs, skill, skillLevels, out range);
        }

        private static bool TryResolveBasicAttackRange(MobaConfigDatabase configs, SkillMO skill,
            IReadOnlyDictionary<int, int> skillLevels, out float range)
        {
            range = 0f;
            if (!configs.TryGetSkillFlow(skill.CastFlowId, out var flow) || flow.BasicAttack == null) return false;
            var level = 0;
            skillLevels?.TryGetValue(flow.BasicAttack.RangeLevelSlot, out level);
            range = flow.BasicAttack.ResolveRange(skill.Range, level);
            return true;
        }

        private bool TryGetConfigs(out MobaConfigDatabase configs)
        {
            configs = _resources.GetOrLoadConfigs();
            return configs != null;
        }

        private bool TryGetSkill(MobaConfigDatabase configs, int skillId, out SkillMO skill)
        {
            skill = null;
            try
            {
                skill = configs.GetSkill(skillId);
            }
            catch
            {
                return false;
            }

            return skill != null;
        }

        private bool TryGetTemplate(
            MobaConfigDatabase configs,
            int templateId,
            out SkillButtonTemplateMO template)
        {
            template = null;
            try
            {
                template = configs.GetSkillButtonTemplate(templateId);
            }
            catch
            {
                return false;
            }

            return template != null;
        }
    }
}
