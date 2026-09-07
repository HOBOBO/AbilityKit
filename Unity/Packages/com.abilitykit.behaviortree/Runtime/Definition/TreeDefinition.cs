using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree.Definition
{
    public sealed class TreeDefinition
    {
        public const int CurrentFormatVersion = 1;

        public string TreeId { get; set; } = "";
        public int FormatVersion { get; set; } = CurrentFormatVersion;
        public string RootNodeId { get; set; } = "";
        public List<NodeDefinition> Nodes { get; set; } = new();
        public BlackboardSchema Blackboard { get; set; } = new();

        public long ComputeDefinitionHash()
        {
            var hash = DeterministicHash.Combine(DeterministicHash.OffsetBasis, (long)FormatVersion);
            hash = DeterministicHash.Combine(hash, HashString(RootNodeId));

            var ordered = new List<NodeDefinition>(Nodes);
            ordered.Sort(static (a, b) => string.CompareOrdinal(a.Id, b.Id));
            foreach (var node in ordered)
            {
                hash = DeterministicHash.Combine(hash, HashString(node.Id));
                hash = DeterministicHash.Combine(hash, HashString(node.Type));
                foreach (var childId in node.ChildIds)
                {
                    hash = DeterministicHash.Combine(hash, HashString(childId));
                }

                hash = DeterministicHash.Combine(hash, (long)node.Properties.Values.Count);
                foreach (var pair in node.Properties.Values)
                {
                    hash = DeterministicHash.Combine(hash, HashString(pair.Key));
                    hash = DeterministicHash.Combine(hash, (long)pair.Value.Type);
                    hash = DeterministicHash.Combine(hash, HashPropertyValue(pair.Value));
                }
            }

            hash = DeterministicHash.Combine(hash, (long)Blackboard.Keys.Count);
            foreach (var key in Blackboard.Keys)
            {
                hash = DeterministicHash.Combine(hash, HashString(key.Name));
                hash = DeterministicHash.Combine(hash, (long)key.Type);
                if (key.Default != null)
                {
                    hash = DeterministicHash.Combine(hash, HashPropertyValue(key.Default));
                }
            }

            return hash;
        }

        public TreeDefinition DeepClone()
        {
            var clone = new TreeDefinition
            {
                TreeId = TreeId,
                FormatVersion = FormatVersion,
                RootNodeId = RootNodeId,
            };

            foreach (var node in Nodes)
            {
                var nodeClone = new NodeDefinition
                {
                    Id = node.Id,
                    Type = node.Type,
                };
                foreach (var property in node.Properties.Values)
                {
                    nodeClone.Properties.Set(property.Key, CloneValue(property.Value));
                }

                nodeClone.ChildIds.AddRange(node.ChildIds);
                clone.Nodes.Add(nodeClone);
            }

            foreach (var key in Blackboard.Keys)
            {
                clone.Blackboard.Keys.Add(new BlackboardKeyDefinition
                {
                    Name = key.Name,
                    Type = key.Type,
                    Default = key.Default == null ? null : CloneValue(key.Default),
                });
            }

            return clone;
        }

        private static long HashPropertyValue(PropertyValue value) => value.Type switch
        {
            ValueType.Bool => value.BoolValue ? 1 : 0,
            ValueType.Int64 => value.Int64Value,
            ValueType.Fixed64 => value.Fixed64Raw,
            ValueType.String => HashString(value.StringValue),
            _ => 0,
        };

        internal static PropertyValue CloneValue(PropertyValue value) => value.Type switch
        {
            ValueType.Bool => PropertyValue.Of(value.BoolValue),
            ValueType.Int64 => PropertyValue.Of(value.Int64Value),
            ValueType.Fixed64 => PropertyValue.Of(Fixed64.FromRaw(value.Fixed64Raw)),
            ValueType.String => PropertyValue.Of(value.StringValue),
            _ => throw new InvalidOperationException($"Unsupported behavior tree value type '{value.Type}'."),
        };

        internal static long HashString(string value)
        {
            var hash = DeterministicHash.OffsetBasis;
            foreach (var c in value)
            {
                hash = DeterministicHash.Combine(hash, (long)c);
            }
            return hash;
        }

        internal AbilityKit.BehaviorTree.BtTreeDefinition ToLegacy()
        {
            var definition = new AbilityKit.BehaviorTree.BtTreeDefinition
            {
                TreeId = TreeId,
                FormatVersion = FormatVersion,
                RootNodeId = RootNodeId,
                Blackboard = Blackboard.ToLegacy(),
            };
            foreach (var node in Nodes)
            {
                definition.Nodes.Add(node.ToLegacy());
            }
            return definition;
        }

        internal static TreeDefinition FromLegacy(AbilityKit.BehaviorTree.BtTreeDefinition source)
        {
            var definition = new TreeDefinition
            {
                TreeId = source.TreeId,
                FormatVersion = source.FormatVersion,
                RootNodeId = source.RootNodeId,
                Blackboard = BlackboardSchema.FromLegacy(source.Blackboard),
            };
            foreach (var node in source.Nodes)
            {
                definition.Nodes.Add(NodeDefinition.FromLegacy(node));
            }
            return definition;
        }
    }
}
