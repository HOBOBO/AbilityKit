namespace AbilityKit.Compute
{
    /// <summary>
    /// Validates accelerated output against business invariants before it is accepted.
    /// </summary>
    public interface IComputeResultValidator<TInput, TOutput>
        where TInput : unmanaged
        where TOutput : unmanaged
    {
        bool Validate(TInput[] inputs, TOutput[] outputs, int count, out string reason);
    }
}
