#nullable enable

using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Editor.Authoring.Workspace
{
    internal sealed class AuthoringDeleteImpact
    {
        public List<string> DeletedNodeIds { get; } = new();
        public List<string> DeletedGroupIds { get; } = new();
        public List<string> DeletedNoteIds { get; } = new();
        public List<string> RootNodeIds { get; } = new();
        public List<string> DetachedChildNodeIds { get; } = new();
        public List<string> AffectedGroupIds { get; } = new();
        public int RemovedEdgeCount { get; set; }
        public bool DeletesRoot => RootNodeIds.Count > 0;
        public bool HasStructuralImpact => DeletedNodeIds.Count > 0 || RemovedEdgeCount > 0 || DetachedChildNodeIds.Count > 0;
    }
}
