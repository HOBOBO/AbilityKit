using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Demo.Moba.Config.Core;

namespace AbilityKit.Demo.Moba.Config.BattleDemo
{
    /// <summary>
    /// MOBA 閰嶇疆琛ㄦ敞鍐岃〃
    /// </summary>
    public sealed class MobaConfigRegistry : IMobaConfigTableRegistry
    {
        public static readonly MobaConfigRegistry Instance = new MobaConfigRegistry();

        private MobaConfigRegistry() { }

        // IConfigTableRegistry (generic)
        public IReadOnlyList<ConfigTableDefinition> Tables => MobaRuntimeConfigTableRegistry.Tables;

        public ConfigTableDefinition GetTable(string filePath)
        {
            foreach (var t in MobaRuntimeConfigTableRegistry.Tables)
            {
                if (t.FilePath == filePath) return t;
            }
            return null;
        }

        public bool TryGetTable(string filePath, out ConfigTableDefinition definition)
        {
            definition = GetTable(filePath);
            return definition != null;
        }

        // IMobaConfigTableRegistry (MOBA-specific)
        public MobaRuntimeConfigTableRegistry.Entry[] MobaTables => MobaRuntimeConfigTableRegistry.Tables;
    }

    /// <summary>
    /// MOBA 閰嶇疆琛ㄦ敞鍐岃〃鏉＄洰锛堜繚鐣欏師鍚嶄互淇濇寔鍏煎鎬э級
    /// </summary>
    public static class MobaRuntimeConfigTableRegistry
    {
        public sealed class Entry : ConfigTableDefinition
        {
            /// <summary>
            /// MO 绫诲瀷鐨勫埆鍚嶏紙涓?EntryType 鐩稿悓锛屾柟渚?MOBA 灞備娇鐢級
            /// </summary>
            public Type MoType => EntryType;

            public Entry(string fileWithoutExt, Type dtoType, Type moType)
                : base(fileWithoutExt, dtoType, moType, groupName: null)
            {
            }

            public Entry(string fileWithoutExt, Type dtoType, Type moType, string groupName)
                : base(fileWithoutExt, dtoType, moType, groupName)
            {
            }
        }

        public static readonly Entry[] Tables =
        {
            // 瑙掕壊鐩稿叧
            new Entry(MobaConfigPaths.CharactersFile, typeof(MO.CharacterDTO), typeof(MO.CharacterMO)),

            // 灞炴€х浉鍏?
            new Entry(MobaConfigPaths.AttributeTemplatesFile, typeof(MO.BattleAttributeTemplateDTO), typeof(MO.BattleAttributeTemplateMO)),
            new Entry(MobaConfigPaths.AttributeTypesFile, typeof(AttrTypeDTO), typeof(MO.AttrTypeMO)),

            // 鎶€鑳界浉鍏?
            new Entry(MobaConfigPaths.SkillsFile, typeof(MO.SkillDTO), typeof(MO.SkillMO)),
            new Entry(MobaConfigPaths.PassiveSkillsFile, typeof(MO.PassiveSkillDTO), typeof(MO.PassiveSkillMO)),
            new Entry(MobaConfigPaths.SkillFlowsFile, typeof(MO.SkillFlowDTO), typeof(MO.SkillFlowMO)),
            new Entry(MobaConfigPaths.SkillLevelTablesFile, typeof(MO.SkillLevelTableDTO), typeof(MO.SkillLevelTableMO)),

            // 瑙嗚鏁堟灉鐩稿叧
            new Entry(MobaConfigPaths.ModelsFile, typeof(MO.ModelDTO), typeof(MO.ModelMO)),

            // Buff 鐩稿叧
            new Entry(MobaConfigPaths.BuffsFile, typeof(MO.BuffDTO), typeof(MO.BuffMO)),

            // 寮归亾鐩稿叧
            new Entry(MobaConfigPaths.ProjectileLaunchersFile, typeof(MO.ProjectileLauncherDTO), typeof(MO.ProjectileLauncherMO)),
            new Entry(MobaConfigPaths.ProjectilesFile, typeof(MO.ProjectileDTO), typeof(MO.ProjectileMO)),

            // AOE 鍜屽彂灏勫櫒
            new Entry(MobaConfigPaths.AoesFile, typeof(MO.AoeDTO), typeof(MO.AoeMO)),
            new Entry(MobaConfigPaths.EmittersFile, typeof(MO.EmitterDTO), typeof(MO.EmitterMO)),

            // 鍙敜鐗?
            new Entry(MobaConfigPaths.SummonsFile, typeof(MO.SummonDTO), typeof(MO.SummonMO)),

            // 缁勪欢妯℃澘
            new Entry(MobaConfigPaths.ComponentTemplatesFile, typeof(MO.ComponentTemplateDTO), typeof(MO.ComponentTemplateMO)),

            // 鎸夐挳妯℃澘
            new Entry(MobaConfigPaths.SkillButtonTemplatesFile, typeof(MO.SkillButtonTemplateDTO), typeof(MO.SkillButtonTemplateMO)),

            // 鏍囩妯℃澘
            new Entry(MobaConfigPaths.TagTemplatesFile, typeof(MO.TagTemplateDTO), typeof(MO.TagTemplateMO)),

            // 鎼滅储鏌ヨ妯℃澘
            new Entry(MobaConfigPaths.SearchQueryTemplatesFile, typeof(MO.SearchQueryTemplateDTO), typeof(MO.SearchQueryTemplateMO)),

            // 鍙敜鍔ㄤ綔妯℃澘
            new Entry(MobaConfigPaths.SpawnSummonActionTemplatesFile, typeof(MO.SpawnSummonActionTemplateDTO), typeof(MO.SpawnSummonActionTemplateMO)),

            // 琛ㄧ幇妯℃澘
            new Entry(MobaConfigPaths.PresentationTemplatesFile, typeof(MO.PresentationTemplateDTO), typeof(MO.PresentationTemplateMO)),

            // 鎸佺画鏁堟灉
            new Entry(MobaConfigPaths.OngoingEffectsFile, typeof(MO.OngoingEffectDTO), typeof(MO.OngoingEffectMO)),
        };
    }
}
