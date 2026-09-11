namespace AbilityKit.Compute
{
    public interface IComputeBackend
    {
        string Id { get; }
        bool IsAvailable { get; }

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
