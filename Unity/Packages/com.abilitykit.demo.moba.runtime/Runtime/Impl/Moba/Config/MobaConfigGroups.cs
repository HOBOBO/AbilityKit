using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config
{
    /// <summary>
    /// MOBA 配置组集合，定义所有配置表及其所属组
    /// </summary>
    public static class MobaConfigGroups
    {
        /// <summary>
        /// Luban 二进制组 - 目前仅支持 Buffs 表
        /// </summary>
        public static readonly LubanBinaryConfigGroup LubanBinary = new(
            new ConfigTableEntry(MobaConfigPaths.BuffsFile, typeof(global::cfg.DRBuff), typeof(BuffMO), ConfigGroupNames.LubanBinary)
        );

        /// <summary>
        /// 传统 JSON 组 - 所有其他配置表
        /// </summary>
        public static readonly LegacyJsonConfigGroup LegacyJson = new(
            new ConfigTableEntry(MobaConfigPaths.CharactersFile, typeof(CharacterDTO), typeof(CharacterMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.SkillsFile, typeof(SkillDTO), typeof(SkillMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.PassiveSkillsFile, typeof(PassiveSkillDTO), typeof(PassiveSkillMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.SkillFlowsFile, typeof(SkillFlowDTO), typeof(SkillFlowMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.SkillLevelTablesFile, typeof(SkillLevelTableDTO), typeof(SkillLevelTableMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.AttributeTypesFile, typeof(AttrTypeDTO), typeof(AttrTypeMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.AttributeTemplatesFile, typeof(BattleAttributeTemplateDTO), typeof(BattleAttributeTemplateMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.ModelsFile, typeof(ModelDTO), typeof(ModelMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.BuffsFile, typeof(BuffDTO), typeof(BuffMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.ProjectileLaunchersFile, typeof(ProjectileLauncherDTO), typeof(ProjectileLauncherMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.ProjectilesFile, typeof(ProjectileDTO), typeof(ProjectileMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.AoesFile, typeof(AoeDTO), typeof(AoeMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.EmittersFile, typeof(EmitterDTO), typeof(EmitterMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.SummonsFile, typeof(SummonDTO), typeof(SummonMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.ComponentTemplatesFile, typeof(ComponentTemplateDTO), typeof(ComponentTemplateMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.SkillButtonTemplatesFile, typeof(SkillButtonTemplateDTO), typeof(SkillButtonTemplateMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.TagTemplatesFile, typeof(TagTemplateDTO), typeof(TagTemplateMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.SearchQueryTemplatesFile, typeof(SearchQueryTemplateDTO), typeof(SearchQueryTemplateMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.SpawnSummonActionTemplatesFile, typeof(SpawnSummonActionTemplateDTO), typeof(SpawnSummonActionTemplateMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.PresentationTemplatesFile, typeof(PresentationTemplateDTO), typeof(PresentationTemplateMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.OngoingEffectsFile, typeof(OngoingEffectDTO), typeof(OngoingEffectMO), ConfigGroupNames.LegacyJson)
        );

        /// <summary>
        /// 获取所有配置组
        /// </summary>
        public static IReadOnlyList<IConfigGroup> All => new IConfigGroup[] { LubanBinary, LegacyJson };

        /// <summary>
        /// 根据组名获取配置组
        /// </summary>
        public static IConfigGroup GetByName(string name)
        {
            foreach (var group in All)
            {
                if (group.Name == name)
                    return group;
            }
            return null;
        }

        /// <summary>
        /// 获取指定表名的配置表条目
        /// </summary>
        public static ConfigTableEntry GetTableEntry(string tableName)
        {
            foreach (var group in All)
            {
                foreach (var entry in group.Tables)
                {
                    if (entry.FileWithoutExt == tableName)
                        return entry;
                }
            }
            return null;
        }
    }
}
