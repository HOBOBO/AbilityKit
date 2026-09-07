#nullable enable

namespace AbilityKit.BehaviorTree.Editor.Authoring.Workspace
{
    internal sealed class AuthoringNodeSearchHit
    {
        public AuthoringNodeSearchHit(
            string nodeId,
            string displayName,
            string typeId,
            string category,
            bool isRoot,
            bool isOrphan)
        {
            NodeId = nodeId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            TypeId = typeId ?? string.Empty;
            Category = category ?? string.Empty;
            IsRoot = isRoot;
            IsOrphan = isOrphan;
        }

        public string NodeId { get; }
        public string DisplayName { get; }
        public string TypeId { get; }
        public string Category { get; }
        public bool IsRoot { get; }
        public bool IsOrphan { get; }
    }
}
