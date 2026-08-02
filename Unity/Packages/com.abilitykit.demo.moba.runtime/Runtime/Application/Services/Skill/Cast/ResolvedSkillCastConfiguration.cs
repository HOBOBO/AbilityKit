using System;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Services
{
    public readonly struct ResolvedSkillCastConfiguration
    {
        public ResolvedSkillCastConfiguration(
            int skillId,
            int skillLevel,
            ResourceType resourceType,
            int resourceCost,
            int cooldownMs,
            bool hasLevelConfiguration)
        {
            SkillId = skillId;
            SkillLevel = skillLevel;
            ResourceType = resourceType;
            ResourceCost = resourceCost;
            CooldownMs = cooldownMs;
            HasLevelConfiguration = hasLevelConfiguration;
        }

        public int SkillId { get; }
        public int SkillLevel { get; }
        public ResourceType ResourceType { get; }
        public int ResourceCost { get; }
        public int CooldownMs { get; }
        public bool HasLevelConfiguration { get; }
        public bool IsValid => SkillId > 0 && SkillLevel > 0;

        public static bool TryResolve(
            MobaConfigDatabase configs,
            int skillId,
            int requestedLevel,
            out ResolvedSkillCastConfiguration resolved,
            out string error)
        {
            resolved = default;
            error = null;
            if (configs == null)
            {
                error = "MobaConfigDatabase is unavailable.";
                return false;
            }

            if (skillId <= 0 ||
                !configs.TryGetSkill(skillId, out var skill) ||
                skill == null)
            {
                error = $"Skill configuration is missing. skillId={skillId}.";
                return false;
            }

            return TryResolve(configs, skill, requestedLevel, out resolved, out error);
        }

        public static bool TryResolve(
            MobaConfigDatabase configs,
            SkillMO skill,
            int requestedLevel,
            out ResolvedSkillCastConfiguration resolved,
            out string error)
        {
            resolved = default;
            error = null;
            if (configs == null || skill == null)
            {
                error = "Skill configuration resolver received an empty dependency.";
                return false;
            }

            var skillLevel = Math.Max(1, requestedLevel);
            if (skill.CooldownMs < 0)
            {
                error = $"Skill base cooldown is negative. skillId={skill.Id}, cooldownMs={skill.CooldownMs}.";
                return false;
            }

            var resourceCost = 0;
            var cooldownMs = skill.CooldownMs;
            var hasLevelConfiguration = false;
            if (skill.LevelTableId > 0)
            {
                if (!configs.TryGetSkillLevelTable(skill.LevelTableId, out var table) ||
                    table == null)
                {
                    error = $"Skill level table is missing. skillId={skill.Id}, levelTableId={skill.LevelTableId}.";
                    return false;
                }

                var levels = table.Levels;
                var levelIndex = skillLevel - 1;
                if (levels == null ||
                    levelIndex < 0 ||
                    levelIndex >= levels.Count ||
                    levels[levelIndex] == null)
                {
                    error = $"Skill level configuration is missing. skillId={skill.Id}, levelTableId={skill.LevelTableId}, level={skillLevel}.";
                    return false;
                }

                var level = levels[levelIndex];
                if (level.Cost < 0)
                {
                    error = $"Skill resource cost is negative. skillId={skill.Id}, level={skillLevel}, cost={level.Cost}.";
                    return false;
                }

                if (level.CooldownMs < 0)
                {
                    error = $"Skill level cooldown is negative. skillId={skill.Id}, level={skillLevel}, cooldownMs={level.CooldownMs}.";
                    return false;
                }

                resourceCost = level.Cost;
                cooldownMs = level.CooldownMs > 0
                    ? level.CooldownMs
                    : skill.CooldownMs;
                hasLevelConfiguration = true;
            }

            resolved = new ResolvedSkillCastConfiguration(
                skill.Id,
                skillLevel,
                ResourceType.Mana,
                resourceCost,
                cooldownMs,
                hasLevelConfiguration);
            return true;
        }
    }
}
