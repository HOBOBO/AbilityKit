using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree.Registry
{
    using AbilityKit.BehaviorTree.Definition;
    using AbilityKit.BehaviorTree.Nodes;

    public sealed class NodeDescriptor
    {
        public string TypeId { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public NodeKind Kind { get; }
        public int MinChildren { get; }
        public int MaxChildren { get; }
        public IReadOnlyList<PropertyField> PropertySchema { get; }
        public IReadOnlyList<BlackboardKeyRef> BlackboardKeys { get; }
        public string? ColorHint { get; }
        public int MenuOrder { get; }
        public Func<NodeBase> Factory { get; }

        public NodeDescriptor(
            string typeId,
            string displayName,
            string category,
            NodeKind kind,
            int minChildren,
            int maxChildren,
            Func<NodeBase> factory,
            IReadOnlyList<PropertyField>? propertySchema = null,
            IReadOnlyList<BlackboardKeyRef>? blackboardKeys = null,
            string? colorHint = null,
            int menuOrder = 0)
        {
            TypeId = typeId;
            DisplayName = displayName;
            Category = category;
            Kind = kind;
            MinChildren = minChildren;
            MaxChildren = maxChildren;
            Factory = factory;
            PropertySchema = propertySchema ?? Array.Empty<PropertyField>();
            BlackboardKeys = blackboardKeys ?? Array.Empty<BlackboardKeyRef>();
            ColorHint = colorHint;
            MenuOrder = menuOrder;
        }




    }
}
