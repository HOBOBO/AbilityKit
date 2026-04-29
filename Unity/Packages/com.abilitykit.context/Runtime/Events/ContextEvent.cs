using System;

namespace AbilityKit.Context
{
    /// <summary>
    /// 上下文事件数据
    /// </summary>
    public readonly struct ContextEvent
    {
        /// <summary>
        /// 事件类型
        /// </summary>
        public ContextEventType Type { get; }

        /// <summary>
        /// 实体 ID
        /// </summary>
        public long EntityId { get; }

        /// <summary>
        /// 属性类型 ID（用于 Updated 事件）
        /// </summary>
        public int PropertyTypeId { get; }

        /// <summary>
        /// 变更的键名（仅 Updated 事件）
        /// </summary>
        public string? ChangedKey { get; }

        /// <summary>
        /// 旧值（仅 Updated 事件）
        /// </summary>
        public object? OldValue { get; }

        /// <summary>
        /// 新值（仅 Updated 事件）
        /// </summary>
        public object? NewValue { get; }

        public ContextEvent(
            ContextEventType type,
            long entityId,
            int propertyTypeId = 0,
            string? changedKey = null,
            object? oldValue = null,
            object? newValue = null)
        {
            Type = type;
            EntityId = entityId;
            PropertyTypeId = propertyTypeId;
            ChangedKey = changedKey;
            OldValue = oldValue;
            NewValue = newValue;
        }

        /// <summary>
        /// 创建 Created 事件
        /// </summary>
        public static ContextEvent Created(long entityId)
        {
            return new ContextEvent(ContextEventType.Created, entityId);
        }

        /// <summary>
        /// 创建 Updated 事件
        /// </summary>
        public static ContextEvent Updated(long entityId, int propertyTypeId,
            string key, object? oldValue, object? newValue)
        {
            return new ContextEvent(ContextEventType.Updated, entityId, propertyTypeId, key, oldValue, newValue);
        }

        /// <summary>
        /// 创建 Destroying 事件
        /// </summary>
        public static ContextEvent Destroying(long entityId)
        {
            return new ContextEvent(ContextEventType.Destroying, entityId);
        }

        /// <summary>
        /// 创建 Destroyed 事件
        /// </summary>
        public static ContextEvent Destroyed(long entityId)
        {
            return new ContextEvent(ContextEventType.Destroyed, entityId);
        }
    }
}
