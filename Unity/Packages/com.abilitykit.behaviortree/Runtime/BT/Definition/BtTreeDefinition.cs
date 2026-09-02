using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree
{
    /// <summary>
    /// 运行时 IR 树定义：导出格式的数据权威。不含布局/分组等编辑态数据，
    /// 不含 CLR 类型名。快照恢复前用 <see cref="ComputeDefinitionHash"/> 校验兼容性。
    /// </summary>
    public sealed class BtTreeDefinition
    {
        public const int CurrentFormatVersion = 1;

        public string TreeId { get; set; } = "";
        public int FormatVersion { get; set; } = CurrentFormatVersion;
        public string RootNodeId { get; set; } = "";
        public List<BtNodeDefinition> Nodes { get; set; } = new();
        public BtBlackboardSchema Blackboard { get; set; } = new();

        /// <summary>
        /// 结构哈希：覆盖节点 id/类型/有序子结构/属性与黑板 schema。
        /// 不含 TreeId；编辑态显示名、注释和布局不属于运行时定义。
        /// </summary>
        public long ComputeDefinitionHash()
        {
            var hash = DeterministicHash.Combine(DeterministicHash.OffsetBasis, (long)FormatVersion);
            hash = DeterministicHash.Combine(hash, HashString(RootNodeId));

            // 按 id 排序遍历，使节点在列表中的顺序不影响哈希
            var ordered = new List<BtNodeDefinition>(Nodes);
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

        /// <summary>
        /// 创建完整的运行时定义副本。运行时与导出器用它取得定义所有权，避免调用方后续修改
        /// 节点、属性或黑板 schema，导致已创建实例的拓扑和定义哈希不一致。
        /// </summary>
        public BtTreeDefinition DeepClone()
        {
            var clone = new BtTreeDefinition
            {
                TreeId = TreeId,
                FormatVersion = FormatVersion,
                RootNodeId = RootNodeId,
            };

            foreach (var node in Nodes)
            {
                var nodeClone = new BtNodeDefinition
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
                clone.Blackboard.Keys.Add(new BtBlackboardKeyDefinition
                {
                    Name = key.Name,
                    Type = key.Type,
                    Default = key.Default == null ? null : CloneValue(key.Default),
                });
            }

            return clone;
        }

        private static long HashPropertyValue(BtPropertyValue value) => value.Type switch
        {
            BtValueType.Bool => value.BoolValue ? 1 : 0,
            BtValueType.Int64 => value.Int64Value,
            BtValueType.Fixed64 => value.Fixed64Raw,
            BtValueType.String => HashString(value.StringValue),
            _ => 0,
        };

        internal static BtPropertyValue CloneValue(BtPropertyValue value) => value.Type switch
        {
            BtValueType.Bool => BtPropertyValue.Of(value.BoolValue),
            BtValueType.Int64 => BtPropertyValue.Of(value.Int64Value),
            BtValueType.Fixed64 => BtPropertyValue.Of(Fixed64.FromRaw(value.Fixed64Raw)),
            BtValueType.String => BtPropertyValue.Of(value.StringValue),
            _ => throw new InvalidOperationException($"Unsupported BT value type '{value.Type}'."),
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
    }
}
