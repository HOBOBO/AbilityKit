namespace AbilityKit.Compute
{
    /// <summary>
    /// 由应用显式配置、可拒绝或执行批量计算的后端。
    /// </summary>
    public interface IComputeBackend
    {
        /// <summary>
        /// 获取后端生命周期内稳定的标识，并用它标记本后端的尝试结果。
        /// </summary>
        string Id { get; }

        /// <summary>
        /// 探测当前是否可执行；后端链将探测异常记为故障，而不是普通不可用。
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// 尝试计算指定数量的元素；只有成功时，调用方才能消费对应输出。
        /// </summary>
        ComputeAttempt Execute<TKernel, TInput, TOutput>(
            TKernel kernel,
            TInput[] inputs,
            TOutput[] outputs,
            int count,
            in ComputeExecutionOptions options)
            where TKernel : struct, IComputeKernel<TInput, TOutput>
            where TInput : unmanaged
            where TOutput : unmanaged;
    }
}
