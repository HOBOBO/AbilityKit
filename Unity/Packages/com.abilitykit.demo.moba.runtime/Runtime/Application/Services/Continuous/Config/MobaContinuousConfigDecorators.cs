using System.Collections.Generic;
using AbilityKit.Continuous;
using AbilityKit.GameplayTags;
using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 用于投影玩法标签的 MOBA 持续运行配置装饰接口。
    /// </summary>
    public interface IMobaContinuousTagConfig
    {
        ContinuousTagRequirements TagRequirements { get; }
    }

    /// <summary>
    /// 用于投影运行时状态修饰器的 MOBA 持续运行配置装饰接口。
    /// </summary>
    public interface IMobaContinuousModifierConfig
    {
        IReadOnlyList<IMobaContinuousModifierSpec> Modifiers { get; }
    }

    /// <summary>
    /// 明确本次 Modifier 投影使用的层数。
    /// IStackConfig.Stack 表示持续体自身的生命周期/业务层数，不能再由投影器隐式读取。
    /// </summary>
    public interface IMobaContinuousModifierStackConfig
    {
        int ModifierStack { get; }
    }

    public static class MobaContinuousModifierStackResolver
    {
        public static int Resolve(IContinuousConfig config)
        {
            if (config is IMobaContinuousModifierStackConfig modifierStack && modifierStack.ModifierStack > 0)
                return modifierStack.ModifierStack;
            return 1;
        }
    }

    /// <summary>
    /// 用于投影周期行为的 MOBA 持续运行配置装饰接口。
    /// </summary>
    public interface IMobaContinuousPeriodicConfig
    {
        float IntervalSeconds { get; }
        IReadOnlyList<int> IntervalEffectIds { get; }
    }

    public interface IMobaContinuousPeriodicGateConfig
    {
        bool RequireOutOfCombat { get; }
        float OutOfCombatSeconds { get; }
    }

    /// <summary>
    /// 用于在整个持续运行生命周期内绑定 owner-bound trigger 的配置装饰接口。
    /// </summary>
    public interface IMobaContinuousOwnerBoundTriggerConfig
    {
        IReadOnlyList<int> TriggerIds { get; }
    }

    public static class MobaContinuousModifierTargetKind
    {
        public const int Attribute = 1;
        public const int StateMachineParameter = 2;
        public const int StateFlag = 3;
        public const int SkillParameter = 4;
        public const int SkillSlotCandidate = 5;
        public const int ParameterReferenceCandidate = 6;
        public const int Custom = 255;
    }

    public static class MobaContinuousModifierEvaluationPolicy
    {
        public const int Realtime = 0;
        public const int OnApplySnapshot = 1;
    }

    public static class MobaModifierAttributeSourceRole
    {
        public const int Owner = 0;
        public const int Source = 1;
        public const int Target = 2;
    }

    /// <summary>
    /// 持续运行配置中携带的 MOBA 侧状态修饰器声明。
    /// </summary>
    public interface IMobaContinuousModifierSpec
    {
        /// <summary>稳定的语义定义 ID；动态触发器没有定义 ID 时为 0。</summary>
        int DefinitionId { get; }
        int TargetKind { get; }
        int TargetId { get; }
        int Op { get; }
        float Value { get; }
        MagnitudeSource Magnitude { get; }
        int EvaluationPolicy { get; }
        int AttributeSourceRole { get; }
        int Priority { get; }
    }

    public interface IMobaContinuousSkillCandidateSpec
    {
        int CandidateSkillId { get; }
    }

    public interface IMobaContinuousReferenceCandidateSpec
    {
        int CandidateReferenceId { get; }
    }

    public interface IMobaContinuousFormulaSpec
    {
        bool HasFormula { get; }
        float FormulaConstant { get; }
        float FormulaLevelCoefficient { get; }
        IReadOnlyList<AbilityKit.Demo.Moba.Share.Config.ModifierFormulaTermDTO> FormulaTerms { get; }
    }

    /// <summary>
    /// 绑定持续运行配置装饰时使用的运行时投影元数据。
    /// </summary>
    public interface IMobaContinuousProjectionConfig
    {
        int OwnerActorId { get; }
        int SourceActorId { get; }
        int TargetActorId { get; }
        int ModifierSourceId { get; }
        GameplayTagSource TagSource { get; }
    }

    public static class MobaContinuousModifierContractValidator
    {
        public static void Validate(IMobaContinuousModifierSpec spec)
        {
            if (spec == null) throw new System.ArgumentNullException(nameof(spec));
            if (spec.EvaluationPolicy != MobaContinuousModifierEvaluationPolicy.Realtime &&
                spec.EvaluationPolicy != MobaContinuousModifierEvaluationPolicy.OnApplySnapshot)
                throw new System.InvalidOperationException($"Modifier {spec.TargetKind}:{spec.TargetId} has an unknown evaluation policy {spec.EvaluationPolicy}.");

            var magnitude = spec.Magnitude;
            if (magnitude.Type == MagnitudeSourceType.Attribute)
            {
                if (spec.AttributeSourceRole < MobaModifierAttributeSourceRole.Owner ||
                    spec.AttributeSourceRole > MobaModifierAttributeSourceRole.Target)
                    throw new System.InvalidOperationException($"Attribute modifier {spec.TargetKind}:{spec.TargetId} has an invalid source role {spec.AttributeSourceRole}.");
                if (spec.EvaluationPolicy == MobaContinuousModifierEvaluationPolicy.Realtime &&
                    spec.AttributeSourceRole != MobaModifierAttributeSourceRole.Owner)
                    throw new System.InvalidOperationException($"Realtime attribute modifier {spec.TargetKind}:{spec.TargetId} requires the owner source role.");
            }
            else if (spec.AttributeSourceRole != MobaModifierAttributeSourceRole.Owner)
            {
                throw new System.InvalidOperationException($"Attribute source role is only valid for attribute magnitudes ({spec.TargetKind}:{spec.TargetId}).");
            }

            if (spec.EvaluationPolicy == MobaContinuousModifierEvaluationPolicy.Realtime &&
                spec is IMobaContinuousFormulaSpec formula && formula.HasFormula)
                throw new System.InvalidOperationException($"Realtime formula modifier {spec.TargetKind}:{spec.TargetId} is unsupported; use OnApplySnapshot.");
        }

        public static void ValidateAll(IReadOnlyList<IMobaContinuousModifierSpec> modifiers)
        {
            if (modifiers == null) return;
            for (var i = 0; i < modifiers.Count; i++)
                if (modifiers[i] != null) Validate(modifiers[i]);
        }
    }
}
