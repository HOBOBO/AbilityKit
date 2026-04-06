using System;

namespace AbilityKit.World.ECS
{
    /// <summary>
    /// 实体世界事件总线。
    /// 提供强类型事件的发布/订阅功能。
    /// </summary>
    public interface IWorldEventBus
    {
        // ============ 生命周期事件 ============

        /// <summary>订阅实体创建事件。</summary>
        IDisposable OnEntityCreated(Action<EntityCreated> handler);

        /// <summary>订阅实体销毁事件。</summary>
        IDisposable OnEntityDestroyed(Action<EntityDestroyed> handler);

        // ============ 组件事件 ============

        /// <summary>订阅组件设置事件。</summary>
        IDisposable OnComponentSet(Action<ComponentSet> handler);

        /// <summary>订阅组件移除事件。</summary>
        IDisposable OnComponentRemoved(Action<ComponentRemoved> handler);

        // ============ 层级事件 ============

        /// <summary>订阅父子关系变化事件。</summary>
        IDisposable OnParentChanged(Action<ParentChanged> handler);

        // ============ 通用事件 ============

        /// <summary>发布事件。</summary>
        void Publish<TEvent>(TEvent evt) where TEvent : struct;

        /// <summary>订阅事件类型。</summary>
        IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;

        /// <summary>获取指定事件类型的订阅者数量（调试用）。</summary>
        int GetSubscriberCount<TEvent>() where TEvent : struct;

        /// <summary>移除所有订阅（慎用）。</summary>
        void Clear();
    }
}
