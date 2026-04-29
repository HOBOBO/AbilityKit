namespace AbilityKit.Triggering.Runtime.Schedule.Behavior
{
    /// <summary>
    /// 调度效果接口
    /// 定义每次调度执行时的效果/行为
    /// 框架通过此接口调用业务方注册的调度效果
    /// </summary>
    public interface IScheduleEffect
    {
        /// <summary>
        /// 执行调度效果
        /// </summary>
        /// <param name="ctx">调度上下文</param>
        void Execute(in ScheduleContext ctx);

        /// <summary>
        /// 可选：执行前检查是否满足条件
        /// </summary>
        /// <param name="ctx">调度上下文</param>
        /// <returns>是否满足执行条件</returns>
        bool CanExecute(in ScheduleContext ctx) => true;
    }
}
