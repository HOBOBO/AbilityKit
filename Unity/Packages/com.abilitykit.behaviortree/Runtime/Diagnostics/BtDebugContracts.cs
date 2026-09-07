using System.Collections.Generic;

namespace AbilityKit.BehaviorTree
{
    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public sealed class BtTreeDebugDelta
    {
        public long Sequence { get; set; }
        public bool IsFull { get; set; }
        public int LastFrame { get; set; }
        public List<BtNodeDebugInfo> Nodes { get; set; } = new();
        public BtBlackboardValueSnapshot? Blackboard { get; set; }
    }

    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public interface IBtTreeDebugDeltaView
    {
        long DebugSequence { get; }
        BtTreeDebugDelta CaptureDebugDelta(long knownSequence = 0, bool includeBlackboard = false);
    }
}
