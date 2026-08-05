using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudSkillButtonTemplateResolver
    {
        private readonly BattleHudSkillButtonTemplateConfigLookup _templates;
        private readonly BattleHudPlayerLoadoutResolver _loadouts;

        public BattleHudSkillButtonTemplateResolver(BattleViewResourceProvider resources = null)
            : this(
                new BattleHudSkillButtonTemplateConfigLookup(resources),
                new BattleHudPlayerLoadoutResolver())
        {
        }

        internal BattleHudSkillButtonTemplateResolver(
            BattleHudSkillButtonTemplateConfigLookup templates,
            BattleHudPlayerLoadoutResolver loadouts)
        {
            _templates = templates ?? new BattleHudSkillButtonTemplateConfigLookup();
            _loadouts = loadouts ?? new BattleHudPlayerLoadoutResolver();
        }

        public bool TryFindLoadout(
            EnterMobaGameRes res,
            string playerId,
            out MobaPlayerLoadout loadout)
        {
            return _loadouts.TryFind(res, playerId, out loadout);
        }

        public int ResolveSkillButtonCount(in MobaPlayerLoadout loadout)
        {
            var activeCount = loadout.SkillIds != null ? loadout.SkillIds.Length : 0;
            return loadout.BasicAttackSkillId > 0 ? activeCount + 1 : activeCount;
        }

        public bool TryResolveSkill(
            in MobaPlayerLoadout loadout,
            int slot,
            out SkillMO skill,
            out SkillButtonTemplateMO template,
            out BattleHudSkillPresentationSpec spec)
        {
            skill = null;
            template = null;
            spec = default;
            if (slot <= 0) return false;

            if (!TryResolveSkillId(loadout, slot, out var skillId)) return false;
            return _templates.TryResolve(skillId, out skill, out template, out spec);
        }

        public static bool TryResolveSkillId(in MobaPlayerLoadout loadout, int slot, out int skillId)
        {
            skillId = 0;
            if (slot <= 0) return false;

            var skills = loadout.SkillIds;
            var activeCount = skills != null ? skills.Length : 0;
            if (slot <= activeCount)
            {
                skillId = skills[slot - 1];
                return skillId > 0;
            }

            if (slot == activeCount + 1 && loadout.BasicAttackSkillId > 0)
            {
                skillId = loadout.BasicAttackSkillId;
                return true;
            }

            return false;
        }
    }
}
