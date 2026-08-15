using System;

namespace AbilityKit.Core.Continuous
{
    /// <summary>
    /// 持续体配置接口（核心，最小化）
    /// 
    /// 只定义核心属性，业务层可按需实现扩展接口。
    /// 扩展接口（可选实现）：
    /// - ITagConfig: 标签配置
    /// - IMutexConfig: 互斥配置
    /// - IDurationConfig: 时长配置
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public interface IContinuousConfig
    {
        /// <summary>
        /// 唯一标识
        /// </summary>
        string Id { get; }

        /// <summary>
        /// 所属实体ID
        /// </summary>
        long OwnerId { get; }

        /// <summary>
        /// 是否可被中断
        /// </summary>
        bool CanBeInterrupted { get; }
    }

    // ========================================================================
    // 扩展接口 — 业务层按需实现
    // ========================================================================

    /// <summary>
    /// 标签配置扩展接口
    /// 实现此接口可支持标签匹配、暂停/阻止规则
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public interface ITagConfig
    {
        /// <summary>
        /// 获取标签
        /// </summary>
        ITagContainer Tags { get; }

        /// <summary>
        /// 被哪些标签暂停
        /// </summary>
        ITagContainer PauseByTags { get; }

        /// <summary>
        /// 被哪些标签阻止
        /// </summary>
        ITagContainer BlockByTags { get; }
    }

    /// <summary>
    /// 标签容器接口（核心抽象，业务层实现具体容器）
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public interface ITagContainer
    {
        bool HasTag(string tag);
        bool HasAny(ITagContainer other);
        int Count { get; }
    }

    /// <summary>
    /// 互斥配置扩展接口
    /// 实现此接口可支持互斥组管理
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public interface IMutexConfig
    {
        /// <summary>
        /// 互斥组名称
        /// </summary>
        string MutexGroup { get; }

        /// <summary>
        /// 优先级（数值越大优先级越高）
        /// </summary>
        int Priority { get; }
    }

    /// <summary>
    /// 时长配置扩展接口
    /// 实现此接口可支持定时过期
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public interface IDurationConfig
    {
        /// <summary>
        /// 持续时长（秒），null 表示无限期
        /// </summary>
        float? DurationSeconds { get; }
    }

    /// <summary>
    /// 层级配置扩展接口
    /// 实现此接口可支持嵌套持续体（如Buff/DEBUFF层级）
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public interface IHierarchyConfig
    {
        /// <summary>
        /// 父级持续体ID
        /// </summary>
        string ParentId { get; }

        /// <summary>
        /// 父级过期时是否级联过期
        /// </summary>
        bool CascadeOnExpire { get; }
    }

    /// <summary>
    /// 层数配置扩展接口
    /// 实现此接口可支持堆叠
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public interface IStackConfig
    {
        /// <summary>
        /// 当前层数
        /// </summary>
        int Stack { get; set; }

        /// <summary>
        /// 最大层数
        /// </summary>
        int MaxStack { get; }
    }
}
