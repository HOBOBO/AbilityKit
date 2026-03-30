using System.Collections.Generic;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo
{
    /// <summary>
    /// MOBA 配置表注册表
    /// </summary>
    public sealed class MobaConfigRegistry : IMobaConfigTableRegistry
    {
        public static readonly MobaConfigRegistry Instance = new MobaConfigRegistry();

        private MobaConfigRegistry() { }

        public MobaRuntimeConfigTableRegistry.Entry[] Tables => MobaRuntimeConfigTableRegistry.Tables;

        public MobaRuntimeConfigTableRegistry.Entry[] MobaTables => MobaRuntimeConfigTableRegistry.Tables;
    }

    /// <summary>
    /// MOBA 配置表注册表条目（保留原名以保持兼容性）
    /// </summary>
    public static class MobaRuntimeConfigTableRegistry
    {
        public sealed class Entry
        {
            public readonly string FileWithoutExt;
            public readonly System.Type DtoType;
            public readonly System.Type MoType;

            public Entry(string fileWithoutExt, System.Type dtoType, System.Type moType)
            {
                FileWithoutExt = fileWithoutExt;
                DtoType = dtoType;
                MoType = moType;
            }
        }

        public static readonly Entry[] Tables =
        {
            // 角色相关
            new Entry(MobaConfigPaths.CharactersFile, typeof(MO.CharacterDTO), typeof(MO.CharacterMO)),

            // 属性相关
            new Entry(MobaConfigPaths.AttributeTemplatesFile, typeof(MO.BattleAttributeTemplateDTO), typeof(MO.BattleAttributeTemplateMO)),
            new Entry(MobaConfigPaths.AttributeTypesFile, typeof(AttrTypeDTO), typeof(MO.AttrTypeMO)),

            // 技能相关
            new Entry(MobaConfigPaths.SkillsFile, typeof(MO.SkillDTO), typeof(MO.SkillMO)),
            new Entry(MobaConfigPaths.PassiveSkillsFile, typeof(MO.PassiveSkillDTO), typeof(MO.PassiveSkillMO)),
            new Entry(MobaConfigPaths.SkillFlowsFile, typeof(MO.SkillFlowDTO), typeof(MO.SkillFlowMO)),
            new Entry(MobaConfigPaths.SkillLevelTablesFile, typeof(MO.SkillLevelTableDTO), typeof(MO.SkillLevelTableMO)),

            // 视觉效果相关
            new Entry(MobaConfigPaths.ModelsFile, typeof(MO.ModelDTO), typeof(MO.ModelMO)),

            // Buff 相关
            new Entry(MobaConfigPaths.BuffsFile, typeof(MO.BuffDTO), typeof(MO.BuffMO)),

            // 弹道相关
            new Entry(MobaConfigPaths.ProjectileLaunchersFile, typeof(MO.ProjectileLauncherDTO), typeof(MO.ProjectileLauncherMO)),
            new Entry(MobaConfigPaths.ProjectilesFile, typeof(MO.ProjectileDTO), typeof(MO.ProjectileMO)),

            // AOE 和发射器
            new Entry(MobaConfigPaths.AoesFile, typeof(MO.AoeDTO), typeof(MO.AoeMO)),
            new Entry(MobaConfigPaths.EmittersFile, typeof(MO.EmitterDTO), typeof(MO.EmitterMO)),

            // 召唤物
            new Entry(MobaConfigPaths.SummonsFile, typeof(MO.SummonDTO), typeof(MO.SummonMO)),

            // 组件模板
            new Entry(MobaConfigPaths.ComponentTemplatesFile, typeof(MO.ComponentTemplateDTO), typeof(MO.ComponentTemplateMO)),

            // 按钮模板
            new Entry(MobaConfigPaths.SkillButtonTemplatesFile, typeof(MO.SkillButtonTemplateDTO), typeof(MO.SkillButtonTemplateMO)),

            // 标签模板
            new Entry(MobaConfigPaths.TagTemplatesFile, typeof(MO.TagTemplateDTO), typeof(MO.TagTemplateMO)),

            // 搜索查询模板
            new Entry(MobaConfigPaths.SearchQueryTemplatesFile, typeof(MO.SearchQueryTemplateDTO), typeof(MO.SearchQueryTemplateMO)),

            // 召唤动作模板
            new Entry(MobaConfigPaths.SpawnSummonActionTemplatesFile, typeof(MO.SpawnSummonActionTemplateDTO), typeof(MO.SpawnSummonActionTemplateMO)),

            // 表现模板
            new Entry(MobaConfigPaths.PresentationTemplatesFile, typeof(MO.PresentationTemplateDTO), typeof(MO.PresentationTemplateMO)),

            // 持续效果
            new Entry(MobaConfigPaths.OngoingEffectsFile, typeof(MO.OngoingEffectDTO), typeof(MO.OngoingEffectMO)),
        };
    }
}
