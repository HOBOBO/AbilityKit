#nullable enable

namespace AbilityKit.Compute
{
    /// <summary>
    /// Defines one independent element of a portable batch computation.
    /// Implementations must not rely on shared mutable state or element ordering.
    /// </summary>
    public interface IComputeKernel<TInput, TOutput>
        where TInput : unmanaged
        where TOutput : unmanaged
    {
        TOutput Execute(in TInput input);
    }
}
