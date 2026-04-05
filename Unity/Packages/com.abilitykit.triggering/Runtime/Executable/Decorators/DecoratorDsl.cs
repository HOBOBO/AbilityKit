using System;
using System.Collections.Generic;
using AbilityKit.Modifiers;
using AbilityKit.Triggering.Runtime.Executable;

namespace AbilityKit.Triggering.Runtime.Executable
{
    // ========================================================================
    // 标签系统 — 核心契约，由业务包实现
    //
    //  注意: 核心包不绑定特定的 Tag 实现 (如 GameplayTag, FGameplayTag 等)
    //  业务包通过实现以下接口并注册到 DecoratorRegistry 来替换默认实现
    // ========================================================================

    // ========================================================================
    // 标签系统类型
    // ========================================================================

    /// <summary>
    /// 标签查询模式
    /// </summary>
    public enum ETagQueryMode
    {
        /// <summary>精确匹配</summary>
        Exact,
        /// <summary>包含父标签匹配</summary>
        IncludeParent,
    }

    /// <summary>
    /// 标签容器事件类型
    /// </summary>
    public enum ETagEvent
    {
        Added,
        Removed,
    }

    /// <summary>
    /// 游戏标签接口
    /// 由业务包实现 (如对接 UE 的 FGameplayTag, Unity 的 GameplayTag 等)
    /// </summary>
    public interface IGameplayTag
    {
        string FullName { get; }
        IGameplayTag Parent { get; }
        bool Matches(IGameplayTag other, ETagQueryMode mode = ETagQueryMode.IncludeParent);
    }

    /// <summary>
    /// 标签变更信息
    /// </summary>
    public readonly struct TagEventData
    {
        public readonly IGameplayTag Tag;
        public readonly ETagEvent Event;
        public readonly object Source;

        public TagEventData(IGameplayTag tag, ETagEvent tagEvent, object source = null)
        {
            Tag = tag;
            Event = tagEvent;
            Source = source;
        }
    }

    /// <summary>
    /// 标签容器接口
    /// 由业务包实现 (负责存储和管理标签)
    /// </summary>
    public interface ITagContainer
    {
        bool Has(IGameplayTag tag, ETagQueryMode mode = ETagQueryMode.IncludeParent);
        bool HasAny(IEnumerable<IGameplayTag> tags);
        bool HasAll(IEnumerable<IGameplayTag> tags);
        void Add(IGameplayTag tag);
        void Remove(IGameplayTag tag);
        IEnumerable<IGameplayTag> GetAll();
        int Count { get; }
        event Action<TagEventData> OnTagChanged;
    }

    /// <summary>
    /// 标签查询条件
    /// </summary>
    public readonly struct TagQuery
    {
        public IReadOnlyList<IGameplayTag> RequiredTags { get; }
        public IReadOnlyList<IGameplayTag> IgnoreTags { get; }
        public ETagQueryMode Mode { get; }

        public TagQuery(
            IEnumerable<IGameplayTag> requiredTags = null,
            IEnumerable<IGameplayTag> ignoreTags = null,
            ETagQueryMode mode = ETagQueryMode.IncludeParent)
        {
            RequiredTags = requiredTags != null ? new List<IGameplayTag>(requiredTags) : null;
            IgnoreTags = ignoreTags != null ? new List<IGameplayTag>(ignoreTags) : null;
            Mode = mode;
        }

        public bool Matches(ITagContainer container)
        {
            if (container == null) return RequiredTags == null || RequiredTags.Count == 0;

            if (RequiredTags != null)
            {
                foreach (var tag in RequiredTags)
                {
                    if (!container.Has(tag, Mode))
                        return false;
                }
            }

            if (IgnoreTags != null)
            {
                foreach (var tag in IgnoreTags)
                {
                    if (container.Has(tag, Mode))
                        return false;
                }
            }

            return true;
        }

        public static TagQuery Require(params IGameplayTag[] tags)
            => new(requiredTags: tags);

        public static TagQuery Ignore(params IGameplayTag[] tags)
            => new(ignoreTags: tags);
    }

    // ========================================================================
    // 修饰器 DSL 入口
    // 通过 DecoratorRegistry 创建，核心包不引用具体实现
    // ========================================================================

    /// <summary>
    /// 修饰器 DSL 静态入口
    /// </summary>
    public static class DecoratorDsl
    {
        /// <summary>
        /// 创建带持续时间的行为
        /// </summary>
        public static IDurationDecorator Duration(ISimpleExecutable inner, float durationMs)
        {
            var deco = DecoratorRegistry.CreateDuration(durationMs);
            deco.Inner = inner;
            return deco;
        }

        /// <summary>
        /// 创建带标签的行为
        /// </summary>
        public static ITagDecorator Tags(ISimpleExecutable inner, params string[] tagNames)
        {
            var tagDeco = DecoratorRegistry.CreateTag(tagNames);
            tagDeco.Inner = inner;
            return tagDeco;
        }

        /// <summary>
        /// 创建带修改器的行为
        /// </summary>
        public static IModifierDecorator Modifiers(ISimpleExecutable inner, params ModifierData[] modifiers)
        {
            var modDeco = DecoratorRegistry.CreateModifier(modifiers);
            modDeco.Inner = inner;
            return modDeco;
        }

        /// <summary>
        /// 创建带修改器的行为（带自定义应用器）
        /// </summary>
        public static IModifierDecorator Modifiers(ISimpleExecutable inner, IModifierApplier applier, params ModifierData[] modifiers)
        {
            var modDeco = DecoratorRegistry.CreateModifier(applier, modifiers);
            modDeco.Inner = inner;
            return modDeco;
        }

        /// <summary>
        /// 创建带层数的行为
        /// </summary>
        public static IStackDecorator Stack(ISimpleExecutable inner, int initialStack = 1, float stackMultiplier = 1f)
        {
            var stackDeco = DecoratorRegistry.CreateStack(initialStack, stackMultiplier);
            stackDeco.Inner = inner;
            return stackDeco;
        }

        /// <summary>
        /// 创建带层级的行为
        /// </summary>
        public static IHierarchyDecorator Hierarchy(ISimpleExecutable inner, int? parentId = null)
        {
            var hierDeco = DecoratorRegistry.CreateHierarchy(parentId);
            hierDeco.Inner = inner;
            return hierDeco;
        }

        /// <summary>
        /// 创建带持续行为的行为
        /// </summary>
        public static IContinuousDecorator Continuous(ISimpleExecutable inner, string continuationId = null)
        {
            var deco = DecoratorRegistry.CreateContinuous(continuationId);
            deco.Inner = inner;
            return deco;
        }

        /// <summary>
        /// 创建带能力的行为
        /// </summary>
        public static ICapabilityDecorator Capability(ISimpleExecutable inner, CapabilityId capabilityId = default)
        {
            var deco = DecoratorRegistry.CreateCapability(capabilityId);
            deco.Inner = inner;
            return deco;
        }

        /// <summary>
        /// 创建带能力的行为 (使用能力ID字符串)
        /// </summary>
        public static ICapabilityDecorator Capability(ISimpleExecutable inner, string capabilityNamespace, string capabilityName)
        {
            var deco = DecoratorRegistry.CreateCapability(new CapabilityId(capabilityNamespace, capabilityName));
            deco.Inner = inner;
            return deco;
        }

        /// <summary>
        /// DOT: 持续伤害
        /// </summary>
        public static IScheduledExecutable DOT(
            ISimpleExecutable damageEffect,
            float durationMs,
            float intervalMs,
            params string[] tags)
        {
            return damageEffect
                .WithTags(tags)
                .WithDuration(durationMs)
                .Periodic(intervalMs);
        }

        /// <summary>
        /// HOT: 持续治疗
        /// </summary>
        public static IScheduledExecutable HOT(
            ISimpleExecutable healEffect,
            float durationMs,
            float intervalMs,
            params string[] tags)
        {
            return healEffect
                .WithTags(tags)
                .WithDuration(durationMs)
                .Periodic(intervalMs);
        }

        /// <summary>
        /// Buff: 属性增益
        /// </summary>
        public static IScheduledExecutable Buff(
            ISimpleExecutable applyEffect,
            float durationMs,
            ModifierData[] modifiers,
            params string[] tags)
        {
            return applyEffect
                .WithTags(tags)
                .WithDuration(durationMs)
                .WithModifiers(modifiers)
                .Periodic(1f);
        }

        /// <summary>
        /// Aura: 被动光环 (无限时间)
        /// </summary>
        public static IScheduledExecutable Aura(
            ISimpleExecutable effect,
            ModifierData[] modifiers,
            params string[] tags)
        {
            return effect
                .WithTags(tags)
                .WithDuration(-1)
                .WithModifiers(modifiers)
                .Periodic(100f);
        }
    }

    // ========================================================================
    // 修饰器扩展方法
    // ========================================================================

    /// <summary>
    /// 修饰器扩展方法
    /// </summary>
    public static class DecoratorExtensions
    {
        /// <summary>开始修饰器构建</summary>
        public static DecoratorBuilder Decorate(this ISimpleExecutable inner)
        {
            return new DecoratorBuilder(inner);
        }

        /// <summary>添加持续时间修饰器</summary>
        public static IDurationDecorator WithDuration(this ISimpleExecutable inner, float durationMs, bool autoStart = true)
        {
            var deco = DecoratorRegistry.CreateDuration(durationMs);
            deco.AutoStart = autoStart;
            deco.Inner = inner;
            return deco;
        }

        /// <summary>添加持续时间修饰器 (链式)</summary>
        public static T WithDuration<T>(this T inner, float durationMs, bool autoStart = true) where T : ISimpleExecutable
        {
            var deco = DecoratorRegistry.CreateDuration(durationMs);
            deco.AutoStart = autoStart;
            deco.Inner = inner;
            return (T)(ISimpleExecutable)deco;
        }

        /// <summary>添加标签修饰器</summary>
        public static ITagDecorator WithTags(this ISimpleExecutable inner, params string[] tagNames)
        {
            var tagDeco = DecoratorRegistry.CreateTag(tagNames);
            tagDeco.Inner = inner;
            return tagDeco;
        }

        /// <summary>添加标签修饰器 (链式)</summary>
        public static T WithTags<T>(this T inner, params string[] tagNames) where T : ISimpleExecutable
        {
            var tagDeco = DecoratorRegistry.CreateTag(tagNames);
            tagDeco.Inner = inner;
            return (T)(ISimpleExecutable)tagDeco;
        }

        /// <summary>添加标签修饰器 (使用 TagQuery)</summary>
        public static ITagDecorator WithTags(this ISimpleExecutable inner, TagQuery required, TagQuery ignore = default)
        {
            var tagDeco = DecoratorRegistry.CreateTag();
            tagDeco.Inner = inner;
            tagDeco.RequiredTags = required;
            tagDeco.IgnoreTags = ignore;
            return tagDeco;
        }

        /// <summary>添加修改器修饰器</summary>
        public static IModifierDecorator WithModifiers(this ISimpleExecutable inner, params ModifierData[] modifiers)
        {
            var modDeco = DecoratorRegistry.CreateModifier(modifiers);
            modDeco.Inner = inner;
            return modDeco;
        }

        /// <summary>添加修改器修饰器 (链式)</summary>
        public static T WithModifiers<T>(this T inner, params ModifierData[] modifiers) where T : ISimpleExecutable
        {
            var modDeco = DecoratorRegistry.CreateModifier(modifiers);
            modDeco.Inner = inner;
            return (T)(ISimpleExecutable)modDeco;
        }

        /// <summary>添加修改器修饰器（带自定义应用器）</summary>
        public static IModifierDecorator WithModifiers(this ISimpleExecutable inner, IModifierApplier applier, params ModifierData[] modifiers)
        {
            var modDeco = DecoratorRegistry.CreateModifier(applier, modifiers);
            modDeco.Inner = inner;
            return modDeco;
        }

        /// <summary>添加修改器修饰器（带自定义应用器，链式）</summary>
        public static T WithModifiers<T>(this T inner, IModifierApplier applier, params ModifierData[] modifiers) where T : ISimpleExecutable
        {
            var modDeco = DecoratorRegistry.CreateModifier(applier, modifiers);
            modDeco.Inner = inner;
            return (T)(ISimpleExecutable)modDeco;
        }

        /// <summary>添加层数修饰器</summary>
        public static IStackDecorator WithStack(this ISimpleExecutable inner, int initialStack = 1, float stackMultiplier = 1f)
        {
            var stackDeco = DecoratorRegistry.CreateStack(initialStack, stackMultiplier);
            stackDeco.Inner = inner;
            return stackDeco;
        }

        /// <summary>添加层数修饰器 (链式)</summary>
        public static T WithStack<T>(this T inner, int initialStack = 1, float stackMultiplier = 1f) where T : ISimpleExecutable
        {
            var stackDeco = DecoratorRegistry.CreateStack(initialStack, stackMultiplier);
            stackDeco.Inner = inner;
            return (T)(ISimpleExecutable)stackDeco;
        }

        /// <summary>添加层级修饰器</summary>
        public static IHierarchyDecorator WithHierarchy(this ISimpleExecutable inner, int? parentId = null)
        {
            var hierDeco = DecoratorRegistry.CreateHierarchy(parentId);
            hierDeco.Inner = inner;
            return hierDeco;
        }

        /// <summary>添加层级修饰器 (链式)</summary>
        public static T WithHierarchy<T>(this T inner, int? parentId = null) where T : ISimpleExecutable
        {
            var hierDeco = DecoratorRegistry.CreateHierarchy(parentId);
            hierDeco.Inner = inner;
            return (T)(ISimpleExecutable)hierDeco;
        }

        /// <summary>添加持续行为修饰器</summary>
        public static IContinuousDecorator WithContinuous(this ISimpleExecutable inner, string continuationId = null)
        {
            var deco = DecoratorRegistry.CreateContinuous(continuationId);
            deco.Inner = inner;
            return deco;
        }

        /// <summary>添加持续行为修饰器 (链式)</summary>
        public static T WithContinuous<T>(this T inner, string continuationId = null) where T : ISimpleExecutable
        {
            var deco = DecoratorRegistry.CreateContinuous(continuationId);
            deco.Inner = inner;
            return (T)(ISimpleExecutable)deco;
        }

        /// <summary>添加能力修饰器</summary>
        public static ICapabilityDecorator WithCapability(this ISimpleExecutable inner, CapabilityId capabilityId = default)
        {
            var deco = DecoratorRegistry.CreateCapability(capabilityId);
            deco.Inner = inner;
            return deco;
        }

        /// <summary>添加能力修饰器 (链式)</summary>
        public static T WithCapability<T>(this T inner, CapabilityId capabilityId = default) where T : ISimpleExecutable
        {
            var deco = DecoratorRegistry.CreateCapability(capabilityId);
            deco.Inner = inner;
            return (T)(ISimpleExecutable)deco;
        }

        /// <summary>添加能力修饰器 (使用能力ID字符串)</summary>
        public static ICapabilityDecorator WithCapability(this ISimpleExecutable inner, string capabilityNamespace, string capabilityName)
        {
            var deco = DecoratorRegistry.CreateCapability(new CapabilityId(capabilityNamespace, capabilityName));
            deco.Inner = inner;
            return deco;
        }

        /// <summary>添加能力修饰器 (使用能力ID字符串，链式)</summary>
        public static T WithCapability<T>(this T inner, string capabilityNamespace, string capabilityName) where T : ISimpleExecutable
        {
            var deco = DecoratorRegistry.CreateCapability(new CapabilityId(capabilityNamespace, capabilityName));
            deco.Inner = inner;
            return (T)(ISimpleExecutable)deco;
        }
    }
}
