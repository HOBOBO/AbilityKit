using System;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config
{
    [Serializable]
    public sealed class CharacterDTO
    {
        public int Id;
        public string Name;
        public int ModelId;
        public int AttributeTemplateId;
        public int[] SkillIds;
    }

    [Serializable]
    public sealed class SkillDTO
    {
        public int Id;
        public string Name;
        public int CooldownMs;
        public int Range;
        public int IconId;
        public int Category;
        public int[] Tags;

        public int LevelTableId;
        public SkillEffectDTO[] Effects;
    }

    [Serializable]
    public sealed class BattleAttributeTemplateDTO
    {
        public int Id;
        public int MaxHp;
        public int Attack;
        public int Defense;
        public int MoveSpeed;
    }

    [Serializable]
    public sealed class ModelDTO
    {
        public int Id;
        public string PrefabPath;
        public float Scale;
    }

    [Serializable]
    public sealed class BuffDTO
    {
        public int Id;
        public string Name;
        public int DurationMs;
        public int[] Tags;
    }
}
