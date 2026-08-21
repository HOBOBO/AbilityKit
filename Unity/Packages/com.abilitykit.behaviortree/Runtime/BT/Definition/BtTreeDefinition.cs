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
        /// 不含 TreeId / Name / Comment（重命名与注释不应使快照失效）。
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

        private static long HashPropertyValue(BtPropertyValue value) => value.Type switch
        {
            BtValueType.Bool => value.BoolValue ? 1 : 0,
            BtValueType.Int64 => value.Int64Value,
            BtValueType.Fixed64 => value.Fixed64Raw,
            BtValueType.String => HashString(value.StringValue),
            _ => 0,
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
