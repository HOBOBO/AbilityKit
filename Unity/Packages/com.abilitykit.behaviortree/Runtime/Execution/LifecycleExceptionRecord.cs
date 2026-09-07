using System;

namespace AbilityKit.BehaviorTree.Execution
{
    public sealed class LifecycleExceptionRecord
    {
        public string NodeId { get; }
        public string Callback { get; }
        public NodeStopReason StopReason { get; }
        public Exception Exception { get; }

        public LifecycleExceptionRecord(
            string nodeId,
            string callback,
            NodeStopReason stopReason,
            Exception exception)
        {
            NodeId = nodeId ?? "";
            Callback = callback ?? "";
            StopReason = stopReason;
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }
    }
}
