using System;

namespace AbilityKit.Demo.Moba.Share.Config
{
    [Serializable]
    public sealed class BattleAttributeTemplateDTO
    {
        public int Id;
        public int BasicAttackSkillId;
        public int[] ActiveSkills;
        public int[] PassiveSkills;
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
        public float MoveSpeed;
        public int PhysicsBloodsuckingR;
        public int MagicBloodsuckingR;
        public int AttackRange;
        public int PerSecondBloodR;
        public int PerSecondManaR;
        public int ResilienceR;
        public int HpGrowth;
        public int MaxHpGrowth;
        public int ExtraHpGrowth;
        public int PhysicsAttackGrowth;
        public int MagicAttackGrowth;
        public int ExtraPhysicsAttackGrowth;
        public int ExtraMagicAttackGrowth;
        public int PhysicsDefenseGrowth;
        public int MagicDefenseGrowth;
        public int ManaGrowth;
        public int MaxManaGrowth;
        public int CriticalRGrowth;
        public int AttackSpeedRGrowth;
        public int CooldownReduceRGrowth;
        public int PhysicsPenetrationRGrowth;
        public int MagicPenetrationRGrowth;
        public float MoveSpeedGrowth;
        public int PhysicsBloodsuckingRGrowth;
        public int MagicBloodsuckingRGrowth;
        public int AttackRangeGrowth;
        public int PerSecondBloodRGrowth;
        public int PerSecondManaRGrowth;
        public int ResilienceRGrowth;
    }

    [Serializable]
    public sealed class AttrTypeDTO
    {
        public int Id;
        public string Key;
        public int ValueKind;
        public float DefaultValue;
    }

    [Serializable]
    public sealed class ModelDTO
    {
        public int Id;
        public string PrefabPath;
        public float Scale;
    }
}
