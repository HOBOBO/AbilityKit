using System;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config
{
    public static class MobaRuntimeConfigTableRegistry
    {
        public sealed class Entry
        {
            public readonly string FileWithoutExt;
            public readonly Type DtoType;
            public readonly Type MoType;

            public Entry(string fileWithoutExt, Type dtoType, Type moType)
            {
                FileWithoutExt = fileWithoutExt;
                DtoType = dtoType;
                MoType = moType;
            }
        }

        public static readonly Entry[] Tables =
        {
            new Entry(MobaConfigPaths.CharactersFile, typeof(CharacterDTO), typeof(MO.CharacterMO)),
            new Entry(MobaConfigPaths.SkillsFile, typeof(SkillDTO), typeof(MO.SkillMO)),
            new Entry(MobaConfigPaths.PassiveSkillsFile, typeof(PassiveSkillDTO), typeof(MO.PassiveSkillMO)),
            new Entry(MobaConfigPaths.SkillFlowsFile, typeof(SkillFlowDTO), typeof(MO.SkillFlowMO)),
            new Entry(MobaConfigPaths.SkillLevelTablesFile, typeof(SkillLevelTableDTO), typeof(MO.SkillLevelTableMO)),
            new Entry(MobaConfigPaths.AttributeTypesFile, typeof(AttrTypeDTO), typeof(MO.AttrTypeMO)),
            new Entry(MobaConfigPaths.AttributeTemplatesFile, typeof(BattleAttributeTemplateDTO), typeof(MO.BattleAttributeTemplateMO)),
            new Entry(MobaConfigPaths.ModelsFile, typeof(ModelDTO), typeof(MO.ModelMO)),
            new Entry(MobaConfigPaths.BuffsFile, typeof(BuffDTO), typeof(MO.BuffMO)),
            new Entry(MobaConfigPaths.ProjectileLaunchersFile, typeof(ProjectileLauncherDTO), typeof(MO.ProjectileLauncherMO)),
            new Entry(MobaConfigPaths.ProjectilesFile, typeof(ProjectileDTO), typeof(MO.ProjectileMO)),
            new Entry(MobaConfigPaths.SummonsFile, typeof(SummonDTO), typeof(MO.SummonMO)),
            new Entry(MobaConfigPaths.ComponentTemplatesFile, typeof(ComponentTemplateDTO), typeof(MO.ComponentTemplateMO)),
        };
    }
}
