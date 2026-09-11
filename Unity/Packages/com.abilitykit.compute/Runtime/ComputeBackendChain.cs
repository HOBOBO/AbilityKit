#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Compute
{
    /// <summary>
    /// Tries explicitly configured backends in order. No backend is registered globally or implicitly.
    /// </summary>
    public sealed class ComputeBackendChain
    {
        private readonly IComputeBackend[] _backends;
        private readonly IComputeObserver? _observer;

        public ComputeBackendChain(IEnumerable<IComputeBackend> backends, IComputeObserver? observer = null)
        {
            if (backends == null)
            {
                throw new ArgumentNullException(nameof(backends));
            }

            var backendList = new List<IComputeBackend>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var backend in backends)
            {
                if (backend == null)
                {
                    throw new ArgumentException("Backend collections cannot contain null entries.", nameof(backends));
                }

                if (string.IsNullOrWhiteSpace(backend.Id))
                {
                    throw new ArgumentException("Every compute backend must have a stable id.", nameof(backends));
                }

                if (!ids.Add(backend.Id))
                {
                    throw new ArgumentException("Compute backend ids must be unique: " + backend.Id, nameof(backends));
                }

                backendList.Add(backend);
            }

            _backends = backendList.ToArray();
            _observer = observer;
        }

        public int BackendCount => _backends.Length;

        public ComputeExecutionResult Execute<TKernel, TInput, TOutput>(
            TKernel kernel,
            TInput[] inputs,
            TOutput[] outputs,
            int count,
            in ComputeExecutionOptions options,
            IComputeResultValidator<TInput, TOutput>? validator = null)
            where TKernel : struct, IComputeKernel<TInput, TOutput>
            where TInput : unmanaged
            where TOutput : unmanaged
        {
            ValidateRequest(inputs, outputs, count, in options);

            var lastAttempt = ComputeAttempt.Declined("compute.none", "No compute backend was configured.");
            var attemptCount = 0;
            for (var index = 0; index < _backends.Length; index++)
            {
                var backend = _backends[index];
                ComputeAttempt attempt;
                if (!backend.IsAvailable)
                {
                    attempt = ComputeAttempt.Unavailable(backend.Id);
                }
                else
                {
                    try
                    {
                        attempt = backend.Execute(kernel, inputs, outputs, count, in options);
                    }
                    catch (Exception exception)
                    {
                        attempt = ComputeAttempt.Faulted(backend.Id, exception);
                    }
                }

                if (!string.Equals(attempt.BackendId, backend.Id, StringComparison.Ordinal))
                {
                    attempt = ComputeAttempt.Faulted(
                        backend.Id,
                        new InvalidOperationException("The backend returned an attempt for a different backend id."));
                }

                if (attempt.Succeeded && validator != null)
                {
                    try
                    {
                        if (!validator.Validate(inputs, outputs, count, out var reason))
                        {
                            attempt = ComputeAttempt.InvalidOutput(backend.Id, reason);
                        }
                    }
                    catch (Exception exception)
                    {
                        attempt = ComputeAttempt.InvalidOutput(
                            backend.Id,
                            "The accelerated output validator threw an exception.",
                            exception);
                    }
                }

                attemptCount++;
                lastAttempt = attempt;
                Observe(options.OperationId!, count, in attempt);
                if (attempt.Succeeded)
                {
                    return new ComputeExecutionResult(true, attemptCount, in attempt);
                }
            }

            return new ComputeExecutionResult(false, attemptCount, in lastAttempt);
        }

        private static void ValidateRequest<TInput, TOutput>(
            TInput[] inputs,
            TOutput[] outputs,
            int count,
            in ComputeExecutionOptions options)
        {
            if (inputs == null)
            {
                throw new ArgumentNullException(nameof(inputs));
            }

            if (outputs == null)
            {
                throw new ArgumentNullException(nameof(outputs));
            }

            if (count < 0 || count > inputs.Length || count > outputs.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (string.IsNullOrWhiteSpace(options.OperationId))
            {
                throw new ArgumentException("ComputeExecutionOptions must contain an operation id.", nameof(options));
            }
        }

        private void Observe(string operationId, int count, in ComputeAttempt attempt)
        {
            if (_observer == null)
            {
                return;
            }

            try
            {
                _observer.OnAttempt(operationId, count, in attempt);
            }
            catch
            {
                // Diagnostics must not change compute or fallback semantics.
            }
        }
    }
}
