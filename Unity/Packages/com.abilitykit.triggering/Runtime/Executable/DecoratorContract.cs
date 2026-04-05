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
    // 持续行为修饰器接口
    //
    //  与 IDurationDecorator 的区别:
    //    IDurationDecorator: 有时间限制的持续行为，到期自动结束
    //    IContinuousDecorator: 无时间限制的持续行为，需要外部触发退出
    //
    //  生命周期:
    //    OnApplied()     -> 能力被应用时 (Enter)
    //    OnTick()        -> 每帧Tick (Update)
    //    OnRemoved()     -> 能力被移除时 (Exit)
    // ========================================================================

    /// <summary>
    /// 持续行为修饰器接口
    /// 
    /// 与 IDurationDecorator 的区别:
    /// - IDurationDecorator: 有时间限制的持续行为，到期自动结束
    /// - IContinuousDecorator: 无时间限制的持续行为，需要外部触发退出
    /// </summary>
    public interface IContinuousDecorator : IDecorator
    {
        /// <summary>持续行为唯一标识</summary>
        string ContinuationId { get; }

        /// <summary>
        /// 行为被应用时调用 (相当于 OnEnter)
        /// </summary>
        void OnApplied(object ctx);

        /// <summary>
        /// 行为每帧Tick (相当于 Update)
        /// </summary>
        void OnTick(object ctx, float deltaTimeMs);

        /// <summary>
        /// 行为被移除时调用 (相当于 OnExit)
        /// </summary>
        void OnRemoved(object ctx);

        /// <summary>
        /// 检查是否能与另一个持续行为共存
        /// </summary>
        bool CanCoexistWith(IContinuousDecorator other);

        /// <summary>
        /// 行为是否已结束
        /// </summary>
        bool IsTerminated { get; }

        /// <summary>
        /// 终止原因 (如果 IsTerminated 为 true)
        /// </summary>
        string TerminationReason { get; }

        /// <summary>
        /// 请求终止此持续行为
        /// </summary>
        void RequestTermination(string reason);
    }

    // ========================================================================
    // 能力修饰器接口 — 用于替换/修改实体的核心行为能力
    //
    //  能力修饰器 vs 数值修饰器:
    //    IModifierDecorator: 修改数值属性 (Add/Mul/Override)
    //    ICapabilityDecorator: 改变实体行为策略 (移动/攻击/可见性)
    //
    // ========================================================================

    /// <summary>
    /// 能力标识
    /// </summary>
    public readonly struct CapabilityId : IEquatable<CapabilityId>
    {
        public readonly string Namespace;
        public readonly string Name;

        public CapabilityId(string ns, string name)
        {
            Namespace = ns ?? string.Empty;
            Name = name ?? string.Empty;
        }

        public static CapabilityId Invalid => new(string.Empty, string.Empty);

        public static CapabilityId Vehicle => new("Ability", "Vehicle");
        public static CapabilityId Flying => new("Ability", "Flying");
        public static CapabilityId Stealth => new("Ability", "Stealth");
        public static CapabilityId Shapeshift => new("Ability", "Shapeshift");

        public bool IsValid => !string.IsNullOrEmpty(Namespace) || !string.IsNullOrEmpty(Name);

        public string FullName => string.IsNullOrEmpty(Namespace) ? Name : $"{Namespace}.{Name}";

        public bool Equals(CapabilityId other)
            => Namespace == other.Namespace && Name == other.Name;

        public override bool Equals(object obj)
            => obj is CapabilityId other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Namespace, Name);

        public override string ToString() => FullName;

        public static bool operator ==(CapabilityId left, CapabilityId right) => left.Equals(right);
        public static bool operator !=(CapabilityId left, CapabilityId right) => !left.Equals(right);
    }

    /// <summary>
    /// 能力修饰器接口
    /// 用于替换/修改实体的核心行为能力
    /// 
    /// 与 IModifierDecorator (数值修饰器) 的区别:
    /// - IModifierDecorator: 修改数值属性 (Add/Mul/Override)
    /// - ICapabilityDecorator: 改变实体行为策略 (移动/攻击/可见性)
    /// </summary>
    public interface ICapabilityDecorator : IDecorator
    {
        /// <summary>能力唯一标识</summary>
        CapabilityId CapabilityId { get; }

        /// <summary>
        /// 能力应用器引用 (用于获取/创建能力容器)
        /// </summary>
        ICapabilityApplier CapabilityApplier { get; set; }

        /// <summary>
        /// 能力被应用时调用 (相当于 OnEnter)
        /// </summary>
        void OnApplied(object ctx);

        /// <summary>
        /// 能力每帧Tick (相当于 Update)
        /// </summary>
        void OnTick(object ctx, float deltaTimeMs);

        /// <summary>
        /// 能力被移除时调用 (相当于 OnExit)
        /// </summary>
        void OnRemoved(object ctx);

        /// <summary>
        /// 检查是否能与另一个能力共存
        /// </summary>
        bool CanCoexistWith(ICapabilityDecorator other);

        /// <summary>
        /// 获取此能力替换的能力ID列表 (用于优先级管理)
        /// </summary>
        IReadOnlyList<CapabilityId> ReplacedCapabilities { get; }

        /// <summary>
        /// 能力是否已终止
        /// </summary>
        bool IsTerminated { get; }

        /// <summary>
        /// 请求终止此能力
        /// </summary>
        void RequestTermination(string reason);
    }

    // ========================================================================
    // 能力容器接口 — 管理实体的所有能力修饰器
    //
    //  设计说明:
    //    能力容器由业务层实现并注册到 ICapabilityApplier
    //    框架只定义接口契约，不关心具体存储方式
    // ========================================================================

    /// <summary>
    /// 能力容器接口
    /// 管理实体的所有能力修饰器
    /// 
    /// 由业务层实现并注册到 ICapabilityApplier
    /// 框架只定义接口契约，不关心具体存储方式
    /// </summary>
    public interface ICapabilityContainer
    {
        /// <summary>添加能力修饰器</summary>
        bool AddCapability(ICapabilityDecorator capability, object ctx);

        /// <summary>移除能力修饰器</summary>
        bool RemoveCapability(CapabilityId capabilityId, object ctx);

        /// <summary>检查是否拥有指定能力</summary>
        bool HasCapability(CapabilityId capabilityId);

        /// <summary>获取指定能力修饰器</summary>
        ICapabilityDecorator GetCapability(CapabilityId capabilityId);

        /// <summary>获取所有活跃能力</summary>
        IReadOnlyList<ICapabilityDecorator> GetAllCapabilities();

        /// <summary>获取指定类型的有效能力 (用于策略查询)</summary>
        T GetEffectiveCapability<T>() where T : class, ICapabilityDecorator;

        /// <summary>容器Tick</summary>
        void Tick(object ctx, float deltaTimeMs);

        /// <summary>清空所有能力</summary>
        void Clear(object ctx);
    }

    // ========================================================================
    // 能力应用器接口 — 创建/获取能力容器
    //
    //  设计说明:
    //    业务层实现此接口来创建具体的能力容器
    //    框架通过此接口获取能力容器来管理能力修饰器
    // ========================================================================

    /// <summary>
    /// 能力应用器接口
    /// 创建/获取能力容器
    /// 
    /// 业务层实现此接口来创建具体的能力容器
    /// 框架通过此接口获取能力容器来管理能力修饰器
    /// </summary>
    public interface ICapabilityApplier
    {
        /// <summary>
        /// 获取或创建指定实体的能力容器
        /// </summary>
        ICapabilityContainer GetOrCreateContainer(object target);

        /// <summary>
        /// 获取指定实体的能力容器 (如果不存在返回 null)
        /// </summary>
        ICapabilityContainer GetContainer(object target);

        /// <summary>
        /// 销毁指定实体的能力容器
        /// </summary>
        void DestroyContainer(object target);
    }

    /// <summary>
    /// 能力应用器注册 Attribute
    /// 业务包实现 ICapabilityApplier 后用此特性标记，框架自动发现并注册
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class CapabilityApplierAttribute : Attribute
    {
        public int Priority { get; }

        public CapabilityApplierAttribute(int priority = 0)
        {
            Priority = priority;
        }
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
