namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 一次管线运行的控制句柄。运行对象是一次性的，进入已完成或失败终态后调用控制方法应保持幂等。
    /// </summary>
    /// <typeparam name="TCtx">管线上文类型。</typeparam>
    public interface IAbilityPipelineRun<TCtx> : IPipelineRunControl
        where TCtx : IAbilityPipelineContext
    {
        /// <summary>
        /// 本次运行绑定的上下文。
        /// </summary>
        TCtx Context { get; }

        /// <summary>
        /// 当前正在执行或最近执行的阶段 ID。
        /// </summary>
        AbilityPipelinePhaseId CurrentPhaseId { get; }

        /// <summary>
        /// 推进一次运行。非执行中状态或暂停状态下调用不会产生副作用。
        /// </summary>
        void Tick(float deltaTime);

    }
}
