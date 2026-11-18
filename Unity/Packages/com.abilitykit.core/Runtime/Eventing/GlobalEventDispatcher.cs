namespace AbilityKit.Core.Eventing
{
    /// <summary>
    /// 提供进程共享事件分发器的兼容访问入口。
    /// </summary>
    /// <remarks>
    /// 共享实例不会自动按战斗或场景隔离，且不保证线程安全。
    /// 调用方负责订阅清理、生命周期边界和运行时配置同步；需要独立作用域时自行持有分发器。
    /// </remarks>
    public static class GlobalEventDispatcher
    {
        /// <summary>
        /// 取得进程共享的事件分发器实例；调用方负责管理其中的订阅生命周期。
        /// </summary>
        public static readonly EventDispatcher Instance = new EventDispatcher();

        /// <summary>
        /// 在共享实例中获取或注册稳定字符串事件标识。
        /// </summary>
        /// <remarks>
        /// 参数校验与冲突行为沿用实例分发器。
        /// </remarks>
        /// <param name="eventId">不能为空引用的事件名称，允许空字符串。</param>
        /// <returns>对应的稳定整数标识。</returns>
        public static int GetOrRegisterEventId(string eventId)
        {
            return Instance.GetOrRegisterEventId(eventId);
        }

        /// <summary>
        /// 清空共享实例的全部订阅及字符串注册信息；当前分发快照仍会完成。
        /// </summary>
        public static void Clear()
        {
            Instance.Clear();
        }

        /// <summary>
        /// 向共享分发器注册回调，沿用实例的排序、重入及异常隔离契约。
        /// </summary>
        /// <remarks>
        /// 回调在发布线程同步执行；同优先级按注册顺序调用。
        /// </remarks>
        /// <typeparam name="TArgs">事件参数的静态类型；不同类型使用独立通道。</typeparam>
        /// <param name="eventId">事件名称或整数标识；字符串不能是空引用。</param>
        /// <param name="handler">同步执行的回调，不能是空引用。</param>
        /// <param name="priority">优先级，较大的值先执行。</param>
        /// <param name="once">是否在第一次回调前移除订阅，并保证最多执行一次。</param>
        /// <returns>由调用方持有的幂等退订句柄；不会释放回调目标。</returns>
        public static IEventSubscription Subscribe<TArgs>(string eventId, System.Action<TArgs> handler, int priority = 0, bool once = false)
        {
            return Instance.Subscribe(eventId, handler, priority, once);
        }

        /// <summary>
        /// 向共享分发器注册回调，沿用实例的排序、重入及异常隔离契约。
        /// </summary>
        /// <remarks>
        /// 回调在发布线程同步执行；同优先级按注册顺序调用。
        /// </remarks>
        /// <typeparam name="TArgs">事件参数的静态类型；不同类型使用独立通道。</typeparam>
        /// <param name="eventId">事件名称或整数标识；字符串不能是空引用。</param>
        /// <param name="handler">同步执行的回调，不能是空引用。</param>
        /// <param name="priority">优先级，较大的值先执行。</param>
        /// <param name="once">是否在第一次回调前移除订阅，并保证最多执行一次。</param>
        /// <returns>由调用方持有的幂等退订句柄；不会释放回调目标。</returns>
        public static IEventSubscription Subscribe<TArgs>(int eventId, System.Action<TArgs> handler, int priority = 0, bool once = false)
        {
            return Instance.Subscribe(eventId, handler, priority, once);
        }

        /// <summary>
        /// 通过共享实例同步发布事件，沿用实例的参数所有权与异常契约。
        /// </summary>
        /// <remarks>
        /// 事件名为空引用时忽略发布且不释放参数；启用自动释放后不得继续使用参数。
        /// 完整释放顺序和失败行为见实例分发器的发布方法。
        /// </remarks>
        /// <typeparam name="TArgs">事件参数的静态类型。</typeparam>
        /// <param name="eventId">事件名称或整数标识。</param>
        /// <param name="args">本次发布共享的参数；自动释放后不得继续保留或使用。</param>
        /// <param name="autoReleaseArgs">是否在本次发布结束时执行参数释放流程。</param>
        public static void Publish<TArgs>(string eventId, in TArgs args, bool autoReleaseArgs = true)
        {
            Instance.Publish(eventId, in args, autoReleaseArgs);
        }

        /// <summary>
        /// 通过共享实例同步发布事件，沿用实例的参数所有权与异常契约。
        /// </summary>
        /// <remarks>
        /// 启用自动释放后不得继续使用参数。完整释放顺序和失败行为见实例分发器的发布方法。
        /// </remarks>
        /// <typeparam name="TArgs">事件参数的静态类型。</typeparam>
        /// <param name="eventId">事件名称或整数标识。</param>
        /// <param name="args">本次发布共享的参数；自动释放后不得继续保留或使用。</param>
        /// <param name="autoReleaseArgs">是否在本次发布结束时执行参数释放流程。</param>
        public static void Publish<TArgs>(int eventId, in TArgs args, bool autoReleaseArgs = true)
        {
            Instance.Publish(eventId, in args, autoReleaseArgs);
        }
    }
}
