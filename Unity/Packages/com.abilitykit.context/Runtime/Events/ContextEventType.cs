using System;

namespace AbilityKit.Context
{
    /// <summary>
    /// 上下文事件类型
    /// </summary>
    public enum ContextEventType
    {
        /// <summary>
        /// 实体被创建
        /// </summary>
        Created,

        /// <summary>
        /// 属性被更新
        /// </summary>
        Updated,

        /// <summary>
        /// 实体即将销毁
        /// </summary>
        Destroying,

        /// <summary>
        /// 实体已销毁
        /// </summary>
        Destroyed,
    }
}
