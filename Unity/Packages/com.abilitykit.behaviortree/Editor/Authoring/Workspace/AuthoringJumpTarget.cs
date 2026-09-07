#nullable enable

namespace AbilityKit.BehaviorTree.Editor.Authoring.Workspace
{
    internal sealed class AuthoringJumpTarget
    {
        public AuthoringJumpTarget(string nodeId, string propertyName = "", string elementId = "")
        {
            NodeId = nodeId ?? "";
            PropertyName = propertyName ?? "";
            ElementId = elementId ?? "";
        }

        public string NodeId { get; }
        public string PropertyName { get; }
        public string ElementId { get; }
        public bool CanJumpToNode => !string.IsNullOrWhiteSpace(NodeId);
    }
}
