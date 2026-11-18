namespace AbilityKit.Core.Eventing
{
    /// <summary>
    /// 表示用于移除单个事件订阅的幂等句柄。
    /// </summary>
    /// <remarks>
    /// 句柄不拥有回调目标，也不会释放目标；不保证线程安全。
    /// </remarks>
    public interface IEventSubscription
    {
        /// <summary>
        /// 移除订阅；重复调用不再产生效果。
        /// </summary>
        /// <remarks>
        /// 已经进入当前发布快照的普通回调仍可能执行；此操作不撤销已开始的发布。
        /// </remarks>
        void Unsubscribe();
    }
}
