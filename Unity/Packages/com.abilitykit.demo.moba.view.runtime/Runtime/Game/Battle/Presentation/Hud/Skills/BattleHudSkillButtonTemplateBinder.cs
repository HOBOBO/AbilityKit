using System.Collections.Generic;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Battle.View.Lib.Skill;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudSkillButtonTemplateBinder
    {
        private readonly BattleHudSkillButtonTemplateResolver _resolver;
        private readonly BattleHudSkillButtonTemplateApplier _applier;
        private readonly Dictionary<int, BattleHudSkillPresentationSpec> _skillSpecs = new Dictionary<int, BattleHudSkillPresentationSpec>();

        public IReadOnlyDictionary<int, BattleHudSkillPresentationSpec> SkillSpecs => _skillSpecs;

        public BattleHudSkillButtonTemplateBinder(BattleViewResourceProvider resources = null)
            : this(new BattleHudSkillButtonTemplateResolver(resources), null)
        {
        }

        internal BattleHudSkillButtonTemplateBinder(
            BattleHudSkillButtonTemplateResolver resolver,
            BattleHudSkillButtonTemplateApplier applier = null)
        {
            _resolver = resolver ?? new BattleHudSkillButtonTemplateResolver();
            _applier = applier ?? new BattleHudSkillButtonTemplateApplier();
        }

        public bool TryResolveLoadout(
            EnterMobaGameRes res,
            string playerId,
            out MobaPlayerLoadout loadout)
        {
            return _resolver.TryFindLoadout(res, playerId, out loadout);
        }

        public int ResolveSkillButtonCount(in MobaPlayerLoadout loadout)
        {
            return _resolver.ResolveSkillButtonCount(loadout);
        }

        public bool TryApply(
            in MobaPlayerLoadout loadout,
            IReadOnlyList<SkillButtonView> skillViews)
        {
            if (skillViews == null || skillViews.Count == 0)
            {
                Log.Warning("[BattleHudSkillButtonTemplateBinder] skip apply: no skill views.");
                return false;
            }

            var pending = new ResolvedSkillButtonTemplate[skillViews.Count];
            for (var i = 0; i < skillViews.Count; i++)
            {
                var slot = i + 1;
                var view = skillViews[i];
                if (view == null)
                {
                    Log.Warning($"[BattleHudSkillButtonTemplateBinder] skip apply: slot={slot} view is null.");
                    return false;
                }

                if (!BattleHudSkillButtonTemplateResolver.TryResolveSkillId(loadout, slot, out var skillId))
                {
                    Log.Warning($"[BattleHudSkillButtonTemplateBinder] skip apply: slot={slot} skill id resolve failed. playerId={loadout.PlayerId.Value}, basicAttackSkillId={loadout.BasicAttackSkillId}, skillCount={(loadout.SkillIds != null ? loadout.SkillIds.Length : 0)}");
                    return false;
                }

                if (!_resolver.TryResolveSkill(loadout, slot, out var skill, out var template, out var spec))
                {
                    spec = BattleHudSkillPresentationSpec.Hidden(skillId, string.Empty);
                    Log.Warning($"[BattleHudSkillButtonTemplateBinder] use hidden fallback: slot={slot}, skillId={skillId}, playerId={loadout.PlayerId.Value}.");
                }

                pending[i] = new ResolvedSkillButtonTemplate(view, skill, template, spec);
            }

            _skillSpecs.Clear();
            Log.Info($"[BattleHudSkillButtonTemplateBinder] apply loadout. playerId={loadout.PlayerId.Value}, basicAttackSkillId={loadout.BasicAttackSkillId}, skillCount={(loadout.SkillIds != null ? loadout.SkillIds.Length : 0)}, viewCount={skillViews.Count}");

            for (var i = 0; i < pending.Length; i++)
            {
                var resolved = pending[i];
                var slot = i + 1;
                _skillSpecs[slot] = resolved.Spec;
                Log.Info($"[BattleHudSkillButtonTemplateBinder] slot={slot} skillId={(resolved.Skill != null ? resolved.Skill.Id : resolved.Spec.SkillId)}, templateId={(resolved.Template != null ? resolved.Template.Id : 0)}, enableAim={resolved.Spec.EnableAim}");
                _applier.Apply(resolved.View, resolved.Template, resolved.Spec);
            }

            return true;
        }

        public void ClearSpecs()
        {
            _skillSpecs.Clear();
        }

        private readonly struct ResolvedSkillButtonTemplate
        {
            public readonly SkillButtonView View;
            public readonly AbilityKit.Demo.Moba.Config.BattleDemo.MO.SkillMO Skill;
            public readonly AbilityKit.Demo.Moba.Config.BattleDemo.MO.SkillButtonTemplateMO Template;
            public readonly BattleHudSkillPresentationSpec Spec;

            public ResolvedSkillButtonTemplate(
                SkillButtonView view,
                AbilityKit.Demo.Moba.Config.BattleDemo.MO.SkillMO skill,
                AbilityKit.Demo.Moba.Config.BattleDemo.MO.SkillButtonTemplateMO template,
                BattleHudSkillPresentationSpec spec)
            {
                View = view;
                Skill = skill;
                Template = template;
                Spec = spec;
            }
        }
    }
}
