namespace AbilityKit.BehaviorTree.Diagnostics
{
    public sealed class DebugHandle
    {
        internal long Id { get; }

        internal DebugHandle(long id) => Id = id;
    }
}
