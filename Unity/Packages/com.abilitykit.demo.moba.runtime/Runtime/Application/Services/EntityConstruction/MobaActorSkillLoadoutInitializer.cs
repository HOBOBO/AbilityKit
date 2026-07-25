using System;
using System.Collections.Generic;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Demo.Moba.Services.EntityConstruction
{
    public readonly struct MobaResolvedHeroLoadout
    {
        public readonly CharacterMO Character;
        public readonly BattleAttributeTemplateMO AttributeTemplate;
        public readonly int BasicAttackSkillId;
        public readonly int[] ActiveSkillIds;
        public readonly int[] PassiveSkillIds;

        public MobaResolvedHeroLoadout(
            CharacterMO character,
            BattleAttributeTemplateMO attributeTemplate,
            int basicAttackSkillId,
            int[] activeSkillIds,
            int[] passiveSkillIds)
        {
            Character = character;
            AttributeTemplate = attributeTemplate;
            BasicAttackSkillId = basicAttackSkillId;
            ActiveSkillIds = activeSkillIds ?? Array.Empty<int>();
            PassiveSkillIds = passiveSkillIds ?? Array.Empty<int>();
        }
    }

    public static class MobaResolvedHeroLoadoutResolver
    {
        public static bool TryResolve(
            MobaConfigDatabase config,
            int heroId,
            out MobaResolvedHeroLoadout resolved,
            out string error)
        {
            resolved = default;
            error = null;
            if (config == null)
            {
                error = "config database is unavailable";
                return false;
            }

            if (!config.TryGetCharacter(heroId, out var character) || character == null)
            {
                error = $"character config is missing: {heroId}";
                return false;
            }

            var templateId = character.AttributeTemplateId;
            if (templateId <= 0 ||
                !config.TryGetAttributeTemplate(templateId, out var template) ||
                template == null)
            {
                error = $"attribute template is missing: {templateId}";
                return false;
            }

            var basicAttackSkillId = template.BasicAttackSkillId;
            if (basicAttackSkillId <= 0 ||
                !config.TryGetSkill(basicAttackSkillId, out var basicAttack) ||
                basicAttack == null)
            {
                error = $"basic attack skill is missing in attribute template: {templateId}";
                return false;
            }

            if (basicAttack.SkillType != SkillType.NormalAttack)
            {
                error = $"configured basic attack is not a normal attack: {basicAttackSkillId}";
                return false;
            }

            if (!TryResolveActiveSkills(config, template, out var activeSkillIds, out error) ||
                !TryResolvePassiveSkills(config, template, out var passiveSkillIds, out error))
            {
                return false;
            }

            resolved = new MobaResolvedHeroLoadout(
                character,
                template,
                basicAttackSkillId,
                activeSkillIds,
                passiveSkillIds);
            return true;
        }

        private static bool TryResolveActiveSkills(
            MobaConfigDatabase config,
            BattleAttributeTemplateMO template,
            out int[] skillIds,
            out string error)
        {
            skillIds = Array.Empty<int>();
            error = null;
            var configured = template.ActiveSkills;
            if (configured == null || configured.Count == 0)
            {
                error = $"active skills are missing in attribute template: {template.Id}";
                return false;
            }

            skillIds = new int[configured.Count];
            for (var i = 0; i < configured.Count; i++)
            {
                var skillId = configured[i];
                if (!config.TryGetSkill(skillId, out var skill) || skill == null)
                {
                    error = $"skill config is missing: {skillId}";
                    return false;
                }

                if (skill.SkillType == SkillType.NormalAttack || skill.SkillType == SkillType.Passive)
                {
                    error = $"attribute template active skill has invalid type: {skillId}";
                    return false;
                }

                skillIds[i] = skillId;
            }

            return true;
        }

        private static bool TryResolvePassiveSkills(
            MobaConfigDatabase config,
            BattleAttributeTemplateMO template,
            out int[] skillIds,
            out string error)
        {
            skillIds = ToArray(template.PassiveSkills);
            error = null;
            for (var i = 0; i < skillIds.Length; i++)
            {
                if (!config.TryGetPassiveSkill(skillIds[i], out var passive) || passive == null)
                {
                    error = $"passive skill config is missing: {skillIds[i]}";
                    return false;
                }
            }

            return true;
        }

        private static int[] ToArray(IReadOnlyList<int> list)
        {
            if (list == null || list.Count == 0) return Array.Empty<int>();
            var result = new int[list.Count];
            for (var i = 0; i < list.Count; i++) result[i] = list[i];
            return result;
        }
    }

    public readonly struct MobaPreparedActorSkillLoadout
    {
        public readonly ActiveSkillRuntime[] ActiveSkills;
        public readonly PassiveSkillRuntime[] PassiveSkills;

        public MobaPreparedActorSkillLoadout(
            ActiveSkillRuntime[] activeSkills,
            PassiveSkillRuntime[] passiveSkills)
        {
            ActiveSkills = activeSkills;
            PassiveSkills = passiveSkills;
        }
    }

    public sealed class MobaActorSkillLoadoutInitializer
    {
        public bool TryInitialize(
            global::ActorEntity entity,
            in MobaPlayerLoadout loadout,
            MobaConfigDatabase config,
            out string error)
        {
            if (!MobaResolvedHeroLoadoutResolver.TryResolve(config, loadout.HeroId, out var resolved, out error))
            {
                return false;
            }

            return TryInitialize(entity, in resolved, out error);
        }

        public bool TryInitialize(
            global::ActorEntity entity,
            in MobaResolvedHeroLoadout resolved,
            out string error)
        {
            if (entity == null)
            {
                error = "actor entity is required";
                return false;
            }

            if (!TryPrepare(in resolved, out var prepared, out error))
            {
                return false;
            }

            Apply(entity, in prepared);
            return true;
        }

        public bool TryPrepare(
            in MobaResolvedHeroLoadout resolved,
            out MobaPreparedActorSkillLoadout prepared,
            out string error)
        {
            prepared = default;
            error = null;
            try
            {
                var activeSkills = CreateActiveSkillRuntimes(
                    CombineBasicAttackAndActiveSkills(resolved.BasicAttackSkillId, resolved.ActiveSkillIds));
                var passiveSkills = CreatePassiveSkillRuntimes(resolved.PassiveSkillIds);
                prepared = new MobaPreparedActorSkillLoadout(activeSkills, passiveSkills);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Log.Exception(ex, "[ActorEntityInitPipeline] PrepareSkillLoadout failed");
                return false;
            }
        }

        public void Apply(
            global::ActorEntity entity,
            in MobaPreparedActorSkillLoadout prepared)
        {
            if (entity == null) return;

            if (entity.hasSkillLoadout)
            {
                entity.ReplaceSkillLoadout(prepared.ActiveSkills, prepared.PassiveSkills);
            }
            else
            {
                entity.AddSkillLoadout(prepared.ActiveSkills, prepared.PassiveSkills);
            }
        }

        private static int[] CombineBasicAttackAndActiveSkills(int basicAttackSkillId, int[] activeSkillIds)
        {
            var hasBasicAttack = basicAttackSkillId > 0;
            var activeCount = activeSkillIds != null ? activeSkillIds.Length : 0;
            if (!hasBasicAttack) return activeSkillIds ?? Array.Empty<int>();

            // Active skills occupy slots 1..N (indices 0..N-1); basic attack is appended
            // at the end so it does not shift active skill slot indices.
            var result = new int[activeCount + 1];
            if (activeCount > 0) Array.Copy(activeSkillIds, 0, result, 0, activeCount);
            result[activeCount] = basicAttackSkillId;
            return result;
        }

        private static ActiveSkillRuntime[] CreateActiveSkillRuntimes(int[] skillIds)
        {
            if (skillIds == null || skillIds.Length == 0) return Array.Empty<ActiveSkillRuntime>();
            var list = new List<ActiveSkillRuntime>(skillIds.Length);
            for (int i = 0; i < skillIds.Length; i++)
            {
                var id = skillIds[i];
                if (id <= 0) continue;
                list.Add(new ActiveSkillRuntime { SkillId = id, Level = 1, CooldownDurationMs = 0, CooldownEndTimeMs = 0L });
            }

            return list.Count == 0 ? Array.Empty<ActiveSkillRuntime>() : list.ToArray();
        }

        private static PassiveSkillRuntime[] CreatePassiveSkillRuntimes(int[] passiveSkillIds)
        {
            if (passiveSkillIds == null || passiveSkillIds.Length == 0) return Array.Empty<PassiveSkillRuntime>();
            var list = new List<PassiveSkillRuntime>(passiveSkillIds.Length);
            for (int i = 0; i < passiveSkillIds.Length; i++)
            {
                var id = passiveSkillIds[i];
                if (id <= 0) continue;
                list.Add(new PassiveSkillRuntime { PassiveSkillId = id, Level = 1, CooldownDurationMs = 0, CooldownEndTimeMs = 0L });
            }

            return list.Count == 0 ? Array.Empty<PassiveSkillRuntime>() : list.ToArray();
        }

    }
}
