using System.Collections.Generic;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core
{
    /// <summary>
    /// MOBA 配置组集合，定义所有配置表及其所属组
    /// </summary>
    public static class MobaConfigGroups
    {
        /// <summary>
        /// 传统 JSON 组 - 所有配置表统一使用普通 JSON 模式
        /// </summary>
        public static readonly LegacyJsonConfigGroup LegacyJson = new(
            new ConfigTableEntry(MobaConfigPaths.CharactersFile, typeof(BattleDemo.MO.CharacterDTO), typeof(BattleDemo.MO.CharacterMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.AttributeTemplatesFile, typeof(BattleDemo.MO.BattleAttributeTemplateDTO), typeof(BattleDemo.MO.BattleAttributeTemplateMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.BuffsFile, typeof(BattleDemo.MO.BuffDTO), typeof(BattleDemo.MO.BuffMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.SkillsFile, typeof(BattleDemo.MO.SkillDTO), typeof(BattleDemo.MO.SkillMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.PassiveSkillsFile, typeof(BattleDemo.MO.PassiveSkillDTO), typeof(BattleDemo.MO.PassiveSkillMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.SkillFlowsFile, typeof(BattleDemo.MO.SkillFlowDTO), typeof(BattleDemo.MO.SkillFlowMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.SkillLevelTablesFile, typeof(BattleDemo.MO.SkillLevelTableDTO), typeof(BattleDemo.MO.SkillLevelTableMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.AttributeTypesFile, typeof(AttrTypeDTO), typeof(BattleDemo.MO.AttrTypeMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.ModelsFile, typeof(BattleDemo.MO.ModelDTO), typeof(BattleDemo.MO.ModelMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.ProjectileLaunchersFile, typeof(BattleDemo.MO.ProjectileLauncherDTO), typeof(BattleDemo.MO.ProjectileLauncherMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.ProjectilesFile, typeof(BattleDemo.MO.ProjectileDTO), typeof(BattleDemo.MO.ProjectileMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.AoesFile, typeof(BattleDemo.MO.AoeDTO), typeof(BattleDemo.MO.AoeMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.EmittersFile, typeof(BattleDemo.MO.EmitterDTO), typeof(BattleDemo.MO.EmitterMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.SummonsFile, typeof(BattleDemo.MO.SummonDTO), typeof(BattleDemo.MO.SummonMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.ComponentTemplatesFile, typeof(BattleDemo.MO.ComponentTemplateDTO), typeof(BattleDemo.MO.ComponentTemplateMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.SkillButtonTemplatesFile, typeof(BattleDemo.MO.SkillButtonTemplateDTO), typeof(BattleDemo.MO.SkillButtonTemplateMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.TagTemplatesFile, typeof(BattleDemo.MO.TagTemplateDTO), typeof(BattleDemo.MO.TagTemplateMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.SearchQueryTemplatesFile, typeof(BattleDemo.MO.SearchQueryTemplateDTO), typeof(BattleDemo.MO.SearchQueryTemplateMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.SpawnSummonActionTemplatesFile, typeof(BattleDemo.MO.SpawnSummonActionTemplateDTO), typeof(BattleDemo.MO.SpawnSummonActionTemplateMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.PresentationTemplatesFile, typeof(BattleDemo.MO.PresentationTemplateDTO), typeof(BattleDemo.MO.PresentationTemplateMO), ConfigGroupNames.LegacyJson),
            new ConfigTableEntry(MobaConfigPaths.OngoingEffectsFile, typeof(BattleDemo.MO.OngoingEffectDTO), typeof(BattleDemo.MO.OngoingEffectMO), ConfigGroupNames.LegacyJson)
        );

        /// <summary>
        /// 获取所有配置组
        /// </summary>
        public static IReadOnlyList<IConfigGroup> All => new IConfigGroup[] { LegacyJson };

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
