#nullable enable

using System;

namespace AbilityKit.Compute
{
    public readonly struct ComputeAttempt
    {
        private ComputeAttempt(
            string backendId,
            ComputeAttemptOutcome outcome,
            string? reason,
            Exception? exception)
        {
            BackendId = backendId;
            Outcome = outcome;
            Reason = reason;
            Exception = exception;
        }

        public string BackendId { get; }
        public ComputeAttemptOutcome Outcome { get; }
        public string? Reason { get; }
        public Exception? Exception { get; }
        public bool Succeeded => Outcome == ComputeAttemptOutcome.Succeeded;

        public static ComputeAttempt Success(string backendId)
        {
            return new ComputeAttempt(RequireBackendId(backendId), ComputeAttemptOutcome.Succeeded, null, null);
        }

        public static ComputeAttempt Unavailable(string backendId, string? reason = null)
        {
            return new ComputeAttempt(RequireBackendId(backendId), ComputeAttemptOutcome.Unavailable, reason, null);
        }

        public static ComputeAttempt Declined(string backendId, string? reason = null)
        {
            return new ComputeAttempt(RequireBackendId(backendId), ComputeAttemptOutcome.Declined, reason, null);
        }

        public static ComputeAttempt InvalidOutput(
            string backendId,
            string? reason = null,
            Exception? exception = null)
        {
            return new ComputeAttempt(
                RequireBackendId(backendId),
                ComputeAttemptOutcome.InvalidOutput,
                reason,
                exception);
        }

        public static ComputeAttempt Faulted(string backendId, Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            return new ComputeAttempt(RequireBackendId(backendId), ComputeAttemptOutcome.Faulted, exception.Message, exception);
        }

        private static string RequireBackendId(string backendId)
        {
            if (string.IsNullOrWhiteSpace(backendId))
            {
                throw new ArgumentException("A backend id is required.", nameof(backendId));
            }

            return backendId;
        }
    }
}
