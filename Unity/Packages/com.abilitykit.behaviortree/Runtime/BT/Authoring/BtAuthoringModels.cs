using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Authoring
{
    /// <summary>授权源文档 schema 标识与版本。</summary>
    public static class BtAuthoringSchema
    {
        public const string Id = "abilitykit-bt-authoring";
        public const string Version = "2.0";
        public const string LegacyVersion = "1.0";
    }

    /// <summary>
    /// 授权源文档（编辑态权威）：结构直接复用运行时 IR <see cref="BtTreeDefinition"/>，
    /// 布局（节点坐标/分组）以平行列表存放，导出时天然剥离——编辑配置与导出配置分离
    /// 由数据模型本身保证，而非导出步骤临时过滤。
    /// </summary>
    public sealed class BtAuthoringSourceDocument
    {
        public string Schema { get; set; } = BtAuthoringSchema.Id;
        public string Version { get; set; } = BtAuthoringSchema.Version;
        public BtAuthoringMetadata Metadata { get; set; } = new();
        public BtTreeDefinition Tree { get; set; } = new();
        public List<BtAuthoringNodeMetadata> NodeMetadata { get; set; } = new();
        public List<BtNodeLayoutData> Layout { get; set; } = new();
        public List<BtAuthoringGroupData> Groups { get; set; } = new();
        public List<BtAuthoringNoteData> Notes { get; set; } = new();

        public BtAuthoringNodeMetadata GetOrCreateNodeMetadata(string nodeId)
        {
            foreach (var metadata in NodeMetadata)
            {
                if (string.Equals(metadata.NodeId, nodeId, System.StringComparison.Ordinal))
                {
                    return metadata;
                }
            }

            var created = new BtAuthoringNodeMetadata { NodeId = nodeId ?? "" };
            NodeMetadata.Add(created);
            return created;
        }

        public bool TryGetNodeMetadata(string nodeId, out BtAuthoringNodeMetadata metadata)
        {
            foreach (var candidate in NodeMetadata)
            {
                if (string.Equals(candidate.NodeId, nodeId, System.StringComparison.Ordinal))
                {
                    metadata = candidate;
                    return true;
                }
            }

            metadata = null!;
            return false;
        }
    }

    public sealed class BtAuthoringMetadata
    {
        public string Author { get; set; } = "team";
        public string Description { get; set; } = "";
    }

    /// <summary>节点显示信息（编辑态，不进运行时 IR）。</summary>
    public sealed class BtAuthoringNodeMetadata
    {
        public string NodeId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Comment { get; set; } = "";
    }

    /// <summary>节点画布坐标（编辑态，不进运行时 IR）。</summary>
    public sealed class BtNodeLayoutData
    {
        public string NodeId { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
    }

    /// <summary>编辑态分组框（视觉分组，不进运行时 IR）。</summary>
    public sealed class BtAuthoringGroupData
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public List<string> NodeIds { get; set; } = new();
    }

    /// <summary>画布注释（编辑态说明，不参与行为树结构和运行时导出）。</summary>
    public sealed class BtAuthoringNoteData
    {
        public string Id { get; set; } = "";
        public string Text { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; } = 240f;
        public float Height { get; set; } = 140f;
    }

    /// <summary>图编辑操作规则：在写入文档前阻止环、多父节点和超出子节点上限。</summary>
    public static class BtAuthoringGraphOperations
    {
        public static bool CanConnect(
            BtTreeDefinition tree,
            string parentId,
            string childId,
            int maxChildren,
            out string error)
        {
            error = "";
            if (tree == null)
            {
                error = "行为树文档为空。";
                return false;
            }
            if (string.Equals(parentId, childId, System.StringComparison.Ordinal))
            {
                error = "节点不能连接到自身。";
                return false;
            }

            var parent = tree.Nodes.Find(n => string.Equals(n.Id, parentId, System.StringComparison.Ordinal));
            var child = tree.Nodes.Find(n => string.Equals(n.Id, childId, System.StringComparison.Ordinal));
            if (parent == null || child == null)
            {
                error = "连接的父节点或子节点不存在。";
                return false;
            }
            if (parent.ChildIds.Contains(childId)) return true;
            if (maxChildren >= 0 && parent.ChildIds.Count >= maxChildren)
            {
                error = $"节点 '{parentId}' 最多允许 {maxChildren} 个子节点。";
                return false;
            }

            foreach (var candidate in tree.Nodes)
            {
                if (!string.Equals(candidate.Id, parentId, System.StringComparison.Ordinal)
                    && candidate.ChildIds.Contains(childId))
                {
                    error = $"节点 '{childId}' 已属于父节点 '{candidate.Id}'。";
                    return false;
                }
            }

            if (CanReach(tree, childId, parentId, new HashSet<string>(System.StringComparer.Ordinal)))
            {
                error = $"连接 '{parentId}' -> '{childId}' 会形成环。";
                return false;
            }
            return true;
        }

        public static bool MoveChild(BtTreeDefinition tree, string parentId, int fromIndex, int toIndex)
        {
            var parent = tree?.Nodes.Find(n => string.Equals(n.Id, parentId, System.StringComparison.Ordinal));
            if (parent == null
                || fromIndex < 0 || fromIndex >= parent.ChildIds.Count
                || toIndex < 0 || toIndex >= parent.ChildIds.Count
                || fromIndex == toIndex)
            {
                return false;
            }

            var childId = parent.ChildIds[fromIndex];
            parent.ChildIds.RemoveAt(fromIndex);
            parent.ChildIds.Insert(toIndex, childId);
            return true;
        }

        private static bool CanReach(
            BtTreeDefinition tree,
            string currentId,
            string targetId,
            HashSet<string> visited)
        {
            if (string.Equals(currentId, targetId, System.StringComparison.Ordinal)) return true;
            if (!visited.Add(currentId)) return false;
            var current = tree.Nodes.Find(n => string.Equals(n.Id, currentId, System.StringComparison.Ordinal));
            if (current == null) return false;
            foreach (var childId in current.ChildIds)
            {
                if (CanReach(tree, childId, targetId, visited)) return true;
            }
            return false;
        }
    }
}
