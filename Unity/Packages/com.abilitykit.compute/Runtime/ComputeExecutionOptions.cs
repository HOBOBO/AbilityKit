#nullable enable

using System;

namespace AbilityKit.Compute
{
    public readonly struct ComputeExecutionOptions
    {
        public ComputeExecutionOptions(
            string operationId,
            int innerLoopBatchCount = 0)
        {
            if (string.IsNullOrWhiteSpace(operationId))
            {
                throw new ArgumentException("A stable operation id is required.", nameof(operationId));
            }

            if (innerLoopBatchCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(innerLoopBatchCount));
            }

            OperationId = operationId;
            InnerLoopBatchCount = innerLoopBatchCount;
        }

        public string? OperationId { get; }

        /// <summary>
        /// Gets the requested backend batch size. Zero lets the backend use its measured default.
        /// </summary>
        public int InnerLoopBatchCount { get; }
    }
}
