namespace AbilityKit.Triggering.Runtime.Dispatcher
{
    /// <summary>
    /// 保留原事件总线场景下的上下文适配器；不要求宿主使用此实现。
    /// </summary>
    public class EventBusDispatcherContext : ITriggerDispatcherContext
    {
        /// <inheritdoc />
        public object Context { get; }

        /// <summary>
        /// 取得构造时传入的时间值，单位为毫秒；不会自行推进。
        /// </summary>
        public float CurrentTimeMs { get; }

        /// <summary>
        /// 保存上下文对象和时间值；未指定时默认时间为零。
        /// </summary>
        public EventBusDispatcherContext(object context, float currentTimeMs = 0)
        {
            Context = context;
            CurrentTimeMs = currentTimeMs;
        }

        /// <summary>
        /// 将上下文对象转换为指定服务类型；不能转换时返回空值。
        /// </summary>
        public T GetService<T>() where T : class
        {
            return Context as T;
        }
    }
}
