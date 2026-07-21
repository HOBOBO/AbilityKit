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
        public int MoveSpeed;
        public int PhysicsBloodsuckingR;
        public int MagicBloodsuckingR;
        public int AttackRange;
        public int PerSecondBloodR;
        public int PerSecondManaR;
        public int ResilienceR;
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
