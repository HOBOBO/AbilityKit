namespace AbilityKit.BehaviorTree.Diagnostics
{
    public interface TreeDebugDeltaView
    {
        long DebugSequence { get; }
        TreeDebugDelta CaptureDebugDelta(long knownSequence, bool includeBlackboard);
    }
}
