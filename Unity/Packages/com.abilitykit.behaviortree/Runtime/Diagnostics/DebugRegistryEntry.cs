namespace AbilityKit.BehaviorTree.Diagnostics
{
    public sealed class DebugRegistryEntry
    {
        public long Id { get; }
        public TreeDebugView View { get; }

        internal DebugRegistryEntry(long id, TreeDebugView view)
        {
            Id = id;
            View = view;
        }

    }
}
