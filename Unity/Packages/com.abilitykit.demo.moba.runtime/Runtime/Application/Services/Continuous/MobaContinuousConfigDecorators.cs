using System.Collections.Generic;
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
        public const int Custom = 255;
    }

    public static class MobaContinuousModifierEvaluationPolicy
    {
        public const int Realtime = 0;
        public const int OnApplySnapshot = 1;
    }

    /// <summary>
    /// 持续运行配置中携带的 MOBA 侧状态修饰器声明。
    /// </summary>
    public interface IMobaContinuousModifierSpec
    {
        int TargetKind { get; }
        int TargetId { get; }
        int Op { get; }
        float Value { get; }
        MagnitudeSource Magnitude { get; }
        int EvaluationPolicy { get; }
        int Priority { get; }
    }

    /// <summary>
    /// 绑定持续运行配置装饰时使用的运行时投影元数据。
    /// </summary>
    public interface IMobaContinuousProjectionConfig
    {
        int OwnerActorId { get; }
        int ModifierSourceId { get; }
        GameplayTagSource TagSource { get; }
    }
}
