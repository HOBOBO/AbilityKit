#nullable enable

using System;

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// Result returned after a host runs a sample.
    /// </summary>
    public sealed class SampleExecutionResult
    {
        /// <summary>
        /// Creates an execution result.
        /// </summary>
        public SampleExecutionResult(
            SampleCatalogEntry entry,
            bool succeeded,
            string? errorMessage = null,
            Exception? exception = null)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            Succeeded = succeeded;
            ErrorMessage = errorMessage ?? string.Empty;
            Exception = exception;
        }

        /// <summary>Executed catalog entry.</summary>
        public SampleCatalogEntry Entry { get; }
        /// <summary>Whether the sample completed without an exception.</summary>
        public bool Succeeded { get; }
        /// <summary>Error message when execution failed.</summary>
        public string ErrorMessage { get; }
        /// <summary>Exception captured during execution.</summary>
        public Exception? Exception { get; }
    }
}
