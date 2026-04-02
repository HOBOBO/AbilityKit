using System;
using System.Collections.Generic;
using AbilityKit.Modifiers;

namespace AbilityKit.Triggering.Runtime.Executable
{
    // ========================================================================
    // 修饰器扩展点 — 核心包只定义契约，业务包提供实现
    //
    //  契约层 (此文件):
    //    - 修饰器标记接口 IDecorator (所有修饰器的统一入口)
    //    - 各功能修饰器接口
    //    - DecoratorImplAttribute (业务包注册实现的入口)
    //
    //  实现层 (DefaultDecorators.cs):
    //    - 各修饰器的默认实现 (业务包可替换)
    //
    //  标签系统 (DecoratorDsl.cs):
    //    - IGameplayTag, ITagContainer, TagQuery 及相关枚举/结构体
    //    - 由具体业务包实现，核心包不绑定特定 Tag 系统
    // ========================================================================

    // ========================================================================
    // 修饰器标记接口 — 所有修饰器实现的统一入口
    // 业务包实现此接口并标记 [DecoratorImpl(typeof(YourDecorator))] 即可自动注册
    // ========================================================================

    /// <summary>
    /// 修饰器标记接口 — 框架识别修饰器实现的唯一标识
    /// 所有具体修饰器类都应实现此接口
    /// </summary>
    public interface IDecorator : IComposableExecutable
    {
        /// <summary>修饰器唯一标识 (对应注册时的 Type)</summary>
        Type DecoratorType { get; }

        /// <summary>是否已准备好执行 (OnBeforeExecute 的前置检查结果)</summary>
        bool IsReady { get; }
    }

    // ========================================================================
    // 各功能修饰器接口定义 — 核心契约，由业务包实现
    // ========================================================================

    /// <summary>
    /// 持续时间修饰器接口
    /// </summary>
    public interface IDurationDecorator : IDecorator
    {
        float DurationMs { get; set; }
        float RemainingMs { get; }
        bool IsExpired { get; }
        bool CanBeInterrupted { get; set; }
        bool AutoStart { get; set; }
        void Refresh(float additionalMs);
        bool Update(object ctx, float deltaTimeMs);
        event Action<object> OnExpired;
    }

    /// <summary>
    /// 标签修饰器接口
    /// </summary>
    public interface ITagDecorator : IDecorator
    {
        ITagContainer Tags { get; set; }
        TagQuery RequiredTags { get; set; }
        TagQuery IgnoreTags { get; set; }
        void AddTag(string tagName);
        void RemoveTag(string tagName);
        event Action<TagEventData> OnTagChanged;
    }

    /// <summary>
    /// 修改器修饰器接口
    /// 集成 modifiers 包的修改器能力，包括计算、叠加、等级缩放等
    /// </summary>
    public interface IModifierDecorator : IDecorator
    {
        /// <summary>来源标识（用于溯源和批量移除）</summary>
        int SourceId { get; set; }

        /// <summary>
        /// 获取当前所有修改器
        /// </summary>
        IReadOnlyList<ModifierData> GetModifiers();

        /// <summary>
        /// 添加修改器
        /// </summary>
        void AddModifier(ModifierData modifier);

        /// <summary>
        /// 移除指定修改器
        /// </summary>
        bool RemoveModifier(ModifierData modifier);

        /// <summary>
        /// 清空所有修改器
        /// </summary>
        void ClearModifiers();

        /// <summary>
        /// 应用器扩展点（可被业务代码替换）
        /// </summary>
        IModifierApplier Applier { get; set; }

        /// <summary>
        /// 等级（用于 ScalableFloat 缩放）
        /// </summary>
        float Level { get; set; }

        /// <summary>
        /// 计算修改器对目标属性的最终影响值
        /// </summary>
        /// <param name="baseValue">基础值</param>
        /// <param name="context">修改器上下文（可为空）</param>
        /// <returns>计算结果</returns>
        ModifierResult Calculate(float baseValue, IModifierContext context = null);

        /// <summary>
        /// 直接应用修改器到目标
        /// </summary>
        /// <param name="target">目标对象</param>
        /// <param name="sourceId">来源ID</param>
        /// <returns>应用结果</returns>
        ModifierApplyResult ApplyTo(object target, int? sourceId = null);

        /// <summary>
        /// 修改器应用成功事件
        /// </summary>
        event Action<ModifierData> OnModifierApplied;

        /// <summary>
        /// 修改器被移除事件
        /// </summary>
        event Action<ModifierData> OnModifierRemoved;
    }

    /// <summary>
    /// 层数修饰器接口
    /// </summary>
    public interface IStackDecorator : IDecorator
    {
        int Stack { get; set; }
        float BaseValue { get; set; }
        float StackMultiplier { get; set; }
        int MaxStack { get; set; }
        float CalculateEffectiveValue(float baseValue);
        void IncrementStack(int amount = 1);
        void DecrementStack(int amount = 1);
        void ResetStack();
        event Action<int, int> OnStackChanged;
    }

    /// <summary>
    /// 层级修饰器接口
    /// </summary>
    public interface IHierarchyDecorator : IDecorator
    {
        int? ParentId { get; set; }
        bool CascadeOnExpire { get; set; }
        bool CascadeOnInterrupt { get; set; }
        void AddChild(int childId);
        void RemoveChild(int childId);
        IReadOnlyList<int> GetChildren();
        event Action<int, bool> OnHierarchyChanged;
    }

    // ========================================================================
    // 修饰器实现标记 Attribute — 业务包注册实现的核心契约
    //
    //  使用方式:
    //    [DecoratorImpl(typeof(IDurationDecorator))]
    //    public sealed class MyDurationDecorator : IDurationDecorator { ... }
    //
    //  框架会自动发现并优先使用业务包注册的实现
    // ========================================================================

    /// <summary>
    /// 标记修饰器实现的 Attribute
    /// 业务包实现接口后用此特性标记，框架自动发现并注册
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class DecoratorImplAttribute : Attribute
    {
        /// <summary>
        /// 该实现所实现的修饰器接口类型
        /// 用作注册时的唯一 key，业务包可通过此类型获取实现
        /// </summary>
        public Type DecoratorType { get; }

        /// <summary>
        /// 注册优先级，数值越大优先级越高 (默认 0)
        /// </summary>
        public int Priority { get; set; }

        public DecoratorImplAttribute(Type decoratorType)
        {
            if (decoratorType == null)
                throw new ArgumentNullException(nameof(decoratorType));

            DecoratorType = decoratorType;
            Priority = 0;
        }
    }
}
