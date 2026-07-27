using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Config.BattleDemo
{
    /// <summary>
    /// MOBA config table registry.
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
    /// MOBA runtime config table registry entries.
    /// </summary>
    public static class MobaRuntimeConfigTableRegistry
    {
        public sealed class Entry : ConfigTableDefinition
        {
            /// <summary>
            /// Alias for EntryType used by MOBA runtime code.
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
            // Characters
            new Entry(MobaConfigPaths.CharactersFile, typeof(CharacterDTO), typeof(MO.CharacterMO)),

            // Attributes
            new Entry(MobaConfigPaths.AttributeTemplatesFile, typeof(BattleAttributeTemplateDTO), typeof(MO.BattleAttributeTemplateMO)),
            new Entry(MobaConfigPaths.AttributeTypesFile, typeof(AttrTypeDTO), typeof(MO.AttrTypeMO)),

            // Skills
            new Entry(MobaConfigPaths.SkillsFile, typeof(SkillDTO), typeof(MO.SkillMO)),
            new Entry(MobaConfigPaths.PassiveSkillsFile, typeof(PassiveSkillDTO), typeof(MO.PassiveSkillMO)),
            new Entry(MobaConfigPaths.SkillFlowsFile, typeof(SkillFlowDTO), typeof(MO.SkillFlowMO)),
            new Entry(MobaConfigPaths.SkillLevelTablesFile, typeof(SkillLevelTableDTO), typeof(MO.SkillLevelTableMO)),

            // Presentation models
            new Entry(MobaConfigPaths.ModelsFile, typeof(ModelDTO), typeof(MO.ModelMO)),

            // Buffs and generic continuous processes
            new Entry(MobaConfigPaths.BuffsFile, typeof(BuffDTO), typeof(MO.BuffMO)),
            new Entry(MobaConfigPaths.ContinuousProcessesFile, typeof(ContinuousProcessDTO), typeof(MO.ContinuousProcessMO)),

            // Projectiles
            new Entry(MobaConfigPaths.ProjectileLaunchersFile, typeof(ProjectileLauncherDTO), typeof(MO.ProjectileLauncherMO)),
            new Entry(MobaConfigPaths.ProjectilesFile, typeof(ProjectileDTO), typeof(MO.ProjectileMO)),

            // Areas and emitters
            new Entry(MobaConfigPaths.AoesFile, typeof(AoeDTO), typeof(MO.AoeMO)),
            new Entry(MobaConfigPaths.EmittersFile, typeof(EmitterDTO), typeof(MO.EmitterMO)),

            // Summons
            new Entry(MobaConfigPaths.SummonsFile, typeof(SummonDTO), typeof(MO.SummonMO)),
            new Entry(MobaConfigPaths.SummonAttrInheritsFile, typeof(SummonAttrInheritDTO), typeof(MO.SummonAttrInheritMO)),

            // Component templates
            new Entry(MobaConfigPaths.ComponentTemplatesFile, typeof(ComponentTemplateDTO), typeof(MO.ComponentTemplateMO)),

            // Skill button templates
            new Entry(MobaConfigPaths.SkillButtonTemplatesFile, typeof(SkillButtonTemplateDTO), typeof(MO.SkillButtonTemplateMO)),

            // Tag templates
            new Entry(MobaConfigPaths.TagTemplatesFile, typeof(TagTemplateDTO), typeof(MO.TagTemplateMO)),
            new Entry(MobaConfigPaths.ContinuousTagTemplatesFile, typeof(ContinuousTagTemplateDTO), typeof(MO.ContinuousTagTemplateMO)),

            // Search query templates
            new Entry(MobaConfigPaths.SearchQueryTemplatesFile, typeof(SearchQueryTemplateDTO), typeof(MO.SearchQueryTemplateMO)),

            // Spawn summon action templates
            new Entry(MobaConfigPaths.SpawnSummonActionTemplatesFile, typeof(SpawnSummonActionTemplateDTO), typeof(MO.SpawnSummonActionTemplateMO)),

            // Presentation templates
            new Entry(MobaConfigPaths.PresentationTemplatesFile, typeof(PresentationTemplateDTO), typeof(MO.PresentationTemplateMO)),

            // Gameplay modes
            new Entry(MobaConfigPaths.GameplaysFile, typeof(GameplayDTO), typeof(MO.GameplayMO)),

            // Motion groups
            new Entry(MobaConfigPaths.MotionGroupsFile, typeof(MotionGroupDTO), typeof(MO.MotionGroupMO)),
        };
    }
}
