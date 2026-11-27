#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Compute
{
    /// <summary>
    /// 按配置顺序尝试计算后端，不隐式添加或全局注册后端。
    /// </summary>
    public sealed class ComputeBackendChain
    {
        private readonly IComputeBackend[] _backends;
        private readonly IComputeObserver? _observer;

        /// <summary>
        /// 固定后端顺序并校验标识唯一性；可选观察器用于报告每次尝试。
        /// </summary>
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

        /// <summary>
        /// 获取已配置的后端数量，包括当前不可用的后端。
        /// </summary>
        public int BackendCount => _backends.Length;

        /// <summary>
        /// 依次探测并执行后端，返回首个成功结果；提供验证器时，成功结果还须通过验证。
        /// </summary>
        /// <remarks>
        /// 探测或执行异常计为后端故障，并继续尝试下一后端。失败不会恢复输出缓冲区。
        /// </remarks>
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
                var backendId = backend.Id;
                ComputeAttempt attempt;
                try
                {
                    attempt = backend.IsAvailable
                        ? backend.Execute(kernel, inputs, outputs, count, in options)
                        : ComputeAttempt.Unavailable(backendId);
                }
                catch (Exception exception)
                {
                    attempt = ComputeAttempt.Faulted(backendId, exception);
                }

                if (!string.Equals(attempt.BackendId, backendId, StringComparison.Ordinal))
                {
                    attempt = ComputeAttempt.Faulted(
                        backendId,
                        new InvalidOperationException("The backend returned an attempt for a different backend id."));
                }

                if (attempt.Succeeded && validator != null)
                {
                    try
                    {
                        if (!validator.Validate(inputs, outputs, count, out var reason))
                        {
                            attempt = ComputeAttempt.InvalidOutput(backendId, reason);
                        }
                    }
                    catch (Exception exception)
                    {
                        attempt = ComputeAttempt.InvalidOutput(
                            backendId,
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
