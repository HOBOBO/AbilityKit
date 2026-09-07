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

        internal AbilityKit.BehaviorTree.BtNodeDescriptor ToLegacy()
        {
            var fields = new List<AbilityKit.BehaviorTree.BtPropertyField>(PropertySchema.Count);
            foreach (var field in PropertySchema) fields.Add(field.ToLegacy());
            var keys = new List<AbilityKit.BehaviorTree.BtBlackboardKeyRef>(BlackboardKeys.Count);
            foreach (var key in BlackboardKeys) keys.Add(key.ToLegacy());
            return new AbilityKit.BehaviorTree.BtNodeDescriptor(
                TypeId, DisplayName, Category, Kind.ToLegacy(), MinChildren, MaxChildren,
                Factory, fields, keys, ColorHint, MenuOrder);
        }

        internal static NodeDescriptor FromLegacy(AbilityKit.BehaviorTree.BtNodeDescriptor source)
        {
            var fields = new List<PropertyField>(source.PropertySchema.Count);
            foreach (var field in source.PropertySchema) fields.Add(PropertyField.FromLegacy(field));
            var keys = new List<BlackboardKeyRef>(source.BlackboardKeys.Count);
            foreach (var key in source.BlackboardKeys) keys.Add(BlackboardKeyRef.FromLegacy(key));
            return new NodeDescriptor(
                source.TypeId, source.DisplayName, source.Category, source.Kind.ToApi(),
                source.MinChildren, source.MaxChildren, source.Factory, fields, keys,
                source.ColorHint, source.MenuOrder);
        }
    }
}
