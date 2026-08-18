using System;

namespace AbilityKit.Demo.Moba.Share.Config
{
    [Serializable]
    public sealed class ContinuousModifierDTO
    {
        public int TargetKind;
        public int TargetId;
        public int AttrTypeId;
        public int Op;
        public float Value;
        public int Priority;

        // 0/Fixed 保持与现有配置兼容。其他值映射到 AbilityKit.Modifiers.MagnitudeSourceType。
        public int MagnitudeSourceType;
        public float MagnitudeBaseValue;
        public float MagnitudeCoefficient;
        public float MagnitudeDuration;
        public int MagnitudeDecayType;
        public int MagnitudeAttributeTypeId;
        public string MagnitudeContextKey;
        public float[] MagnitudeCurve;

        // 0/Realtime 在目标值每次重算时从来源重新计算。
        // 1/OnApplySnapshot 在 continuous modifier 投影时计算一次，然后存储固定值。
        public int EvaluationPolicy;
    }

    [Serializable]
    public sealed class ContinuousProcessDTO
    {
        public int Id;
        public string Name;
        public int DurationMs;
        public int IntervalMs;
        public int[] IntervalTriggerIds;
        public int[] TriggerIds;
        public int ContinuousTagTemplateId;
        public string[] TagNames;
        public ContinuousModifierDTO[] Modifiers;
        public bool RequireOutOfCombat;
        public int OutOfCombatSeconds;
    }

    [Serializable]
    public sealed class BuffDTO
    {
        public int Id;
        public string Name;
        public int DurationMs;

        public int[] OnAddEffects;
        public int[] OnRemoveEffects;
        public int[] OnIntervalEffects;
        public int IntervalMs;
        public int PresentationTemplateId;
        public int StackingPolicy;
        public int RefreshPolicy;
        public int MaxStacks;
        public int[] TriggerIds;
        public int ContinuousTagTemplateId;
        public string[] TagNames;
        public ContinuousModifierDTO[] Modifiers;

        // 0 keeps legacy tag-based dispel behavior; 1 is explicitly dispellable; 2 is undispellable.
        public int DispelPolicy;
        // 0 means any category. Positive values are project-defined dispel categories.
        public int DispelCategory;
        // Effective target tags that block a dispel attempt for this buff.
        public string[] DispelBlockedByTagNames;
    }

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
