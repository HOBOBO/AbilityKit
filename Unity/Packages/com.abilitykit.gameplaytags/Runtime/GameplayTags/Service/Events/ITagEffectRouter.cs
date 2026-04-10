using System;
using System.Collections.Generic;

namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 标签效果路由器接口，用于处理标签变化事件。
    /// </summary>
    public interface ITagEffectRouter
    {
        /// <summary>
        /// 注册订阅者
        /// </summary>
        void Register(ITagChangeSubscriber subscriber);

        /// <summary>
        /// 取消注册订阅者
        /// </summary>
        bool Unregister(ITagChangeSubscriber subscriber);

        /// <summary>
        /// 获取所有注册的订阅者
        /// </summary>
        IReadOnlyList<ITagChangeSubscriber> GetSubscribers();
    }
}