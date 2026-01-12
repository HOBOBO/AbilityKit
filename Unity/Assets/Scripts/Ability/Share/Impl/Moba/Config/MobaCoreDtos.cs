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
        public int Hp;
        public int MaxHp;
        public int ExtraHp;
        public int PhysicsAttack;
        public int MagicAttack;
        public int ExtraPhysicsAttack;
        public int ExtraMagicAttack;
        public int PhysicsDefense;
        public int MagicDefense;
        public int Mana;
        public int MaxMana;
        public int CriticalR;
        public int AttackSpeedR;
        public int CooldownReduceR;
        public int PhysicsPenetrationR;
        public int MagicPenetrationR;
        public int MoveSpeed;
        public int PhysicsBloodsuckingR;
        public int MagicBloodsuckingR;
        public int AttackRange;
        public int PerSecondBloodR;
        public int PerSecondManaR;
        public int ResilienceR;
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
        public int EffectId;
        public int StackingPolicy;
        public int RefreshPolicy;
        public int MaxStacks;
        public int[] Tags;
    }
}
