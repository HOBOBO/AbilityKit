using System;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO
{
    public enum SkillEffectType
    {
        Damage = 1,
        AddBuff = 2,
    }

    [Serializable]
    public sealed class SkillEffectDTO
    {
        public int Type;
        public DamageEffectDTO Damage;
        public AddBuffEffectDTO AddBuff;
    }

    [Serializable]
    public sealed class DamageEffectDTO
    {
        public int FormulaType;
        public float Value;
        public float Scale;
        public int AttrTypeId;
        public int DamageType;
    }

    [Serializable]
    public sealed class AddBuffEffectDTO
    {
        public int BuffId;
        public int DurationMsOverride;
    }
}
