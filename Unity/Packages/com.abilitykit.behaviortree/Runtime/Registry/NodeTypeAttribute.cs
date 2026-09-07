using System;

namespace AbilityKit.BehaviorTree.Registry
{
    using AbilityKit.BehaviorTree.Definition;

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class NodeTypeAttribute : Attribute
    {
        public string NodeTypeId { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public NodeKind Kind { get; }

        public NodeTypeAttribute(string nodeTypeId, string displayName, string category, NodeKind kind)
        {
            NodeTypeId = nodeTypeId;
            DisplayName = displayName;
            Category = category;
            Kind = kind;
        }
    }
}
