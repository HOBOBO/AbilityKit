#nullable enable

using System;

namespace AbilityKit.Compute
{
    /// <summary>
    /// Portable reference backend. It participates only when explicitly added to a backend chain.
    /// </summary>
    public sealed class ManagedSequentialComputeBackend : IComputeBackend
    {
        public const string DefaultId = "abilitykit.compute.managed-sequential";

        public ManagedSequentialComputeBackend(string id = DefaultId)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A backend id is required.", nameof(id));
            }

            Id = id;
        }

        public string Id { get; }
        public bool IsAvailable => true;

        public ComputeAttempt Execute<TKernel, TInput, TOutput>(
            TKernel kernel,
            TInput[] inputs,
            TOutput[] outputs,
            int count,
            in ComputeExecutionOptions options)
            where TKernel : struct, IComputeKernel<TInput, TOutput>
            where TInput : unmanaged
            where TOutput : unmanaged
        {
            try
            {
                for (var index = 0; index < count; index++)
                {
                    outputs[index] = kernel.Execute(in inputs[index]);
                }

                return ComputeAttempt.Success(Id);
            }
            catch (Exception exception)
            {
                return ComputeAttempt.Faulted(Id, exception);
            }
        }
    }
}
