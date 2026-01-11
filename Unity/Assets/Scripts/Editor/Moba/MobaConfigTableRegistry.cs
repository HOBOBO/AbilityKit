#if UNITY_EDITOR
using System;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    public static class MobaConfigTableRegistry
    {
        public static readonly Type[] TableAssetTypes =
        {
            typeof(CharacterSO),
            typeof(SkillSO),
            typeof(SkillLevelTableSO),
            typeof(AttrTypeSO),
            typeof(BattleAttributeTemplateSO),
            typeof(ModelSO),
            typeof(BuffSO),
        };
    }
}
#endif
