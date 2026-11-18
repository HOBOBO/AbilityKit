using System;

namespace AbilityKit.Core.Continuous
{
    /// <summary>
    /// 旧 Core 持续体的最小配置契约，保留至下一主版本移除窗口。
    /// 新代码使用 AbilityKit.Continuous；可选扩展仅提供数据，默认管理器不自动执行这些策略。
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public interface IContinuousConfig
    {
        /// <summary>
        /// 获取业务配置标识；默认管理器不按此值去重。
        /// </summary>
        string Id { get; }

        /// <summary>
        /// 获取所属实体标识；登记期间必须保持稳定，以免破坏所属实体索引。
        /// </summary>
        long OwnerId { get; }

        /// <summary>
        /// 获取是否允许管理器通过 TryInterrupt 调用 Abort；不限制直接 End 或注销。
        /// </summary>
        bool CanBeInterrupted { get; }
    }

    /// <summary>
    /// 旧 Core 标签配置数据契约；默认管理器不自动执行暂停或阻止规则。
    /// 新代码使用 AbilityKit.Continuous，具体匹配与状态策略由调用方装配。
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public interface ITagConfig
    {
        /// <summary>
        /// 获取本对象的标签，供显式装配的标签策略查询。
        /// </summary>
        ITagContainer Tags { get; }

        /// <summary>
        /// 获取业务定义的暂停标签条件；默认管理器和内置标签阻止策略不读取此属性。
        /// </summary>
        ITagContainer PauseByTags { get; }

        /// <summary>
        /// 获取业务定义的阻止标签条件；内置策略使用构造时传入的标签容器，不读取此属性。
        /// </summary>
        ITagContainer BlockByTags { get; }
    }

    /// <summary>
    /// 旧 Core 标签容器抽象；匹配语义及具体存储由实现提供，新代码使用 AbilityKit.Continuous。
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public interface ITagContainer
    {
        /// <summary>
        /// 判断容器是否包含指定标签，匹配规则由实现定义。
        /// </summary>
        bool HasTag(string tag);

        /// <summary>
        /// 判断是否与另一容器具有匹配标签；空值及空容器规则由实现定义。
        /// </summary>
        bool HasAny(ITagContainer other);

        /// <summary>
        /// 获取标签数量，供策略判断是否存在待匹配标签。
        /// </summary>
        int Count { get; }
    }

    /// <summary>
    /// 旧 Core 互斥配置数据契约；默认管理器不执行互斥组管理，新代码使用 AbilityKit.Continuous。
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public interface IMutexConfig
    {
        /// <summary>
        /// 获取业务定义的互斥组名称。
        /// </summary>
        string MutexGroup { get; }

        /// <summary>
        /// 获取业务定义的优先级，约定数值越大优先级越高；默认管理器不据此排序。
        /// </summary>
        int Priority { get; }
    }

    /// <summary>
    /// 旧 Core 时长配置数据契约；默认管理器不推进计时或自动过期，新代码使用 AbilityKit.Continuous。
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public interface IDurationConfig
    {
        /// <summary>
        /// 获取持续时长，单位为秒，空值表示无限期；合法范围及到期动作由调用方负责。
        /// </summary>
        float? DurationSeconds { get; }
    }

    /// <summary>
    /// 旧 Core 层级配置数据契约；默认管理器不解析父子关系或执行级联，新代码使用 AbilityKit.Continuous。
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public interface IHierarchyConfig
    {
        /// <summary>
        /// 获取业务定义的父级持续体标识。
        /// </summary>
        string ParentId { get; }

        /// <summary>
        /// 获取父级过期时是否请求级联过期的业务配置。
        /// </summary>
        bool CascadeOnExpire { get; }
    }

    /// <summary>
    /// 旧 Core 层数配置数据契约；默认管理器不执行叠加或上限校验，新代码使用 AbilityKit.Continuous。
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public interface IStackConfig
    {
        /// <summary>
        /// 获取或设置业务维护的当前层数。
        /// </summary>
        int Stack { get; set; }

        /// <summary>
        /// 获取业务定义的最大层数；本接口不自动约束 Stack。
        /// </summary>
        int MaxStack { get; }
    }
}
