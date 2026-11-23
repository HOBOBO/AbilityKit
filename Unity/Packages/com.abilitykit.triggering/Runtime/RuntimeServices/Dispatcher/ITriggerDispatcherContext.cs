namespace AbilityKit.Triggering.Runtime.Dispatcher
{
    /// <summary>
    /// 为计划动作调度提供宿主上下文、服务查询和当前时间。
    /// 此契约不依赖旧派发器，也不负责推进宿主时钟。
    /// </summary>
    public interface ITriggerDispatcherContext
    {
        /// <summary>
        /// 取得宿主提供的执行上下文对象。
        /// </summary>
        object Context { get; }

        /// <summary>
        /// 向当前宿主查询指定类型的服务。
        /// </summary>
        T GetService<T>() where T : class;

        /// <summary>
        /// 取得宿主提供的当前时间，单位为毫秒。
        /// </summary>
        float CurrentTimeMs { get; }
    }
}
