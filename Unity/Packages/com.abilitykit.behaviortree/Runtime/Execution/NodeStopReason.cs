namespace AbilityKit.BehaviorTree.Execution
{
    public enum NodeStopReason
    {
        None = 0,
        Completed = 1,
        Disabled = 2,
        Disposed = 3,
        Restarted = 4,
        Aborted = 5,
        Preempted = 6,
        EnableFailed = 7,
        Restored = 8,
    }
}