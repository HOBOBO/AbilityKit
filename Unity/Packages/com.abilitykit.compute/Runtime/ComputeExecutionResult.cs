#nullable enable

namespace AbilityKit.Compute
{
    public readonly struct ComputeExecutionResult
    {
        public ComputeExecutionResult(bool succeeded, int attemptCount, in ComputeAttempt lastAttempt)
        {
            Succeeded = succeeded;
            AttemptCount = attemptCount;
            LastAttempt = lastAttempt;
        }

        public bool Succeeded { get; }
        public int AttemptCount { get; }
        public ComputeAttempt LastAttempt { get; }
        public string? BackendId => LastAttempt.BackendId;
    }
}
