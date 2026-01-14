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
        public int[] PassiveSkillIds;
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
        public int PreCastFlowId;
        public int CastFlowId;
    }

    [Serializable]
    public sealed class PassiveSkillDTO
    {
        public int Id;
        public string Name;
        public int CooldownMs;
        public int[] TriggerIds;
    }

    [Serializable]
    public sealed class SkillFlowDTO
    {
        public int Id;
        public string Name;
        public SkillPhaseDTO[] Phases;
    }

    public enum SkillPhaseType
    {
        Checks = 1,
        Timeline = 2,
    }

    [Serializable]
    public sealed class SkillPhaseDTO
    {
        public int Type;
        public SkillChecksPhaseDTO Checks;
        public SkillTimelinePhaseDTO Timeline;
    }

    [Serializable]
    public sealed class SkillChecksPhaseDTO
    {
        public bool CheckCooldown;
        public bool CheckCastingState;
        public int[] RequiredTags;
        public int[] BlockedTags;
    }

    [Serializable]
    public sealed class SkillTimelinePhaseDTO
    {
        public int DurationMs;
        public SkillTimelineEventDTO[] Events;
    }

    [Serializable]
    public sealed class SkillTimelineEventDTO
    {
        public int AtMs;
        public int EffectId;
        public int ExecuteMode;
        public string EventTag;
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
