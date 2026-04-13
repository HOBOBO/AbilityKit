using System.Collections.Generic;
using AbilityKit.Ability.Config;

namespace AbilityKit.Demo.Moba.Config.Core
{
    /// <summary>
    /// MOBA 閰嶇疆缁勯泦鍚堬紝瀹氫箟鎵€鏈夐厤缃〃鍙婂叾鎵€灞炵粍
    /// </summary>
    public static class MobaConfigGroups
    {
        private static readonly LegacyJsonConfigGroupDeserializer _legacyJsonDeserializer = LegacyJsonConfigGroupDeserializer.Instance;

        /// <summary>
        /// 浼犵粺 JSON 缁?- 鎵€鏈夐厤缃〃缁熶竴浣跨敤鏅€?JSON 妯″紡
        /// </summary>
        public static readonly ConfigGroup LegacyJson = new ConfigGroup(
            ConfigGroupNames.LegacyJson,
            MobaConfigPaths.DefaultResourcesDir,
            _legacyJsonDeserializer,
            new ConfigTableDefinition(MobaConfigPaths.CharactersFile, typeof(BattleDemo.MO.CharacterDTO), typeof(BattleDemo.MO.CharacterMO), ConfigGroupNames.LegacyJson),
            new ConfigTableDefinition(MobaConfigPaths.AttributeTemplatesFile, typeof(BattleDemo.MO.BattleAttributeTemplateDTO), typeof(BattleDemo.MO.BattleAttributeTemplateMO), ConfigGroupNames.LegacyJson),
            new ConfigTableDefinition(MobaConfigPaths.BuffsFile, typeof(BattleDemo.MO.BuffDTO), typeof(BattleDemo.MO.BuffMO), ConfigGroupNames.LegacyJson),
            new ConfigTableDefinition(MobaConfigPaths.SkillsFile, typeof(BattleDemo.MO.SkillDTO), typeof(BattleDemo.MO.SkillMO), ConfigGroupNames.LegacyJson),
            new ConfigTableDefinition(MobaConfigPaths.PassiveSkillsFile, typeof(BattleDemo.MO.PassiveSkillDTO), typeof(BattleDemo.MO.PassiveSkillMO), ConfigGroupNames.LegacyJson),
            new ConfigTableDefinition(MobaConfigPaths.SkillFlowsFile, typeof(BattleDemo.MO.SkillFlowDTO), typeof(BattleDemo.MO.SkillFlowMO), ConfigGroupNames.LegacyJson),
            new ConfigTableDefinition(MobaConfigPaths.SkillLevelTablesFile, typeof(BattleDemo.MO.SkillLevelTableDTO), typeof(BattleDemo.MO.SkillLevelTableMO), ConfigGroupNames.LegacyJson),
            new ConfigTableDefinition(MobaConfigPaths.AttributeTypesFile, typeof(AttrTypeDTO), typeof(BattleDemo.MO.AttrTypeMO), ConfigGroupNames.LegacyJson),
            new ConfigTableDefinition(MobaConfigPaths.ModelsFile, typeof(BattleDemo.MO.ModelDTO), typeof(BattleDemo.MO.ModelMO), ConfigGroupNames.LegacyJson),
            new ConfigTableDefinition(MobaConfigPaths.ProjectileLaunchersFile, typeof(BattleDemo.MO.ProjectileLauncherDTO), typeof(BattleDemo.MO.ProjectileLauncherMO), ConfigGroupNames.LegacyJson),
            new ConfigTableDefinition(MobaConfigPaths.ProjectilesFile, typeof(BattleDemo.MO.ProjectileDTO), typeof(BattleDemo.MO.ProjectileMO), ConfigGroupNames.LegacyJson),
            new ConfigTableDefinition(MobaConfigPaths.AoesFile, typeof(BattleDemo.MO.AoeDTO), typeof(BattleDemo.MO.AoeMO), ConfigGroupNames.LegacyJson),
            new ConfigTableDefinition(MobaConfigPaths.EmittersFile, typeof(BattleDemo.MO.EmitterDTO), typeof(BattleDemo.MO.EmitterMO), ConfigGroupNames.LegacyJson),
            new ConfigTableDefinition(MobaConfigPaths.SummonsFile, typeof(BattleDemo.MO.SummonDTO), typeof(BattleDemo.MO.SummonMO), ConfigGroupNames.LegacyJson),
            new ConfigTableDefinition(MobaConfigPaths.ComponentTemplatesFile, typeof(BattleDemo.MO.ComponentTemplateDTO), typeof(BattleDemo.MO.ComponentTemplateMO), ConfigGroupNames.LegacyJson),
            new ConfigTableDefinition(MobaConfigPaths.SkillButtonTemplatesFile, typeof(BattleDemo.MO.SkillButtonTemplateDTO), typeof(BattleDemo.MO.SkillButtonTemplateMO), ConfigGroupNames.LegacyJson),
            new ConfigTableDefinition(MobaConfigPaths.TagTemplatesFile, typeof(BattleDemo.MO.TagTemplateDTO), typeof(BattleDemo.MO.TagTemplateMO), ConfigGroupNames.LegacyJson),
            new ConfigTableDefinition(MobaConfigPaths.SearchQueryTemplatesFile, typeof(BattleDemo.MO.SearchQueryTemplateDTO), typeof(BattleDemo.MO.SearchQueryTemplateMO), ConfigGroupNames.LegacyJson),
            new ConfigTableDefinition(MobaConfigPaths.SpawnSummonActionTemplatesFile, typeof(BattleDemo.MO.SpawnSummonActionTemplateDTO), typeof(BattleDemo.MO.SpawnSummonActionTemplateMO), ConfigGroupNames.LegacyJson),
            new ConfigTableDefinition(MobaConfigPaths.PresentationTemplatesFile, typeof(BattleDemo.MO.PresentationTemplateDTO), typeof(BattleDemo.MO.PresentationTemplateMO), ConfigGroupNames.LegacyJson),
            new ConfigTableDefinition(MobaConfigPaths.OngoingEffectsFile, typeof(BattleDemo.MO.OngoingEffectDTO), typeof(BattleDemo.MO.OngoingEffectMO), ConfigGroupNames.LegacyJson)
        );

        /// <summary>
        /// 鑾峰彇鎵€鏈夐厤缃粍
        /// </summary>
        public static IReadOnlyList<IConfigGroup> All => new IConfigGroup[] { LegacyJson };

        /// <summary>
        /// 鏍规嵁缁勫悕鑾峰彇閰嶇疆缁?
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
        /// 鑾峰彇鎸囧畾琛ㄥ悕鐨勯厤缃〃鏉＄洰
        /// </summary>
        public static ConfigTableDefinition GetTableEntry(string tableName)
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
