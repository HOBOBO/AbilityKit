#nullable enable

using System;
using System.Threading.Tasks;

namespace AbilityKit.Compute
{
    /// <summary>
    /// Explicit managed parallel backend for runtimes where the thread pool is an
    /// appropriate acceleration mechanism. It is never installed automatically.
    /// </summary>
    public sealed class ManagedParallelComputeBackend : IComputeBackend
    {
        public const string DefaultId = "abilitykit.compute.managed-parallel";
        public const int DefaultMinimumItemCount = 128;

        private readonly int _minimumItemCount;
        private readonly int _maximumDegreeOfParallelism;

        public ManagedParallelComputeBackend(
            int minimumItemCount = DefaultMinimumItemCount,
            int maximumDegreeOfParallelism = 0,
            string id = DefaultId)
        {
            if (minimumItemCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumItemCount));
            }

            if (maximumDegreeOfParallelism < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumDegreeOfParallelism));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A backend id is required.", nameof(id));
            }

            _minimumItemCount = minimumItemCount;
            _maximumDegreeOfParallelism = maximumDegreeOfParallelism;
            Id = id;
        }

        public string Id { get; }
        public bool IsAvailable => Environment.ProcessorCount > 1;

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
            if (!IsAvailable)
            {
                return ComputeAttempt.Unavailable(Id, "The runtime exposes only one processor.");
            }

            if (count < _minimumItemCount)
            {
                return ComputeAttempt.Declined(Id, "The batch is below the configured parallel threshold.");
            }

            try
            {
                var parallelOptions = new ParallelOptions();
                if (_maximumDegreeOfParallelism > 0)
                {
                    parallelOptions.MaxDegreeOfParallelism = _maximumDegreeOfParallelism;
                }

                Parallel.For(0, count, parallelOptions, index =>
                {
                    outputs[index] = kernel.Execute(in inputs[index]);
                });
                return ComputeAttempt.Success(Id);
            }
            catch (Exception exception)
            {
                return ComputeAttempt.Faulted(Id, exception);
            }
        }
    }
}
