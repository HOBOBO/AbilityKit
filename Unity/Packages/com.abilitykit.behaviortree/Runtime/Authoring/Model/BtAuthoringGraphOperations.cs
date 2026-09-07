using System.Collections.Generic;
using AbilityKit.BehaviorTree.Definition;

namespace AbilityKit.BehaviorTree.Authoring.Model
{
    public static class GraphOperations
    {
        public static bool CanConnect(
            TreeDefinition tree,
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

        public static bool MoveChild(TreeDefinition tree, string parentId, int fromIndex, int toIndex)
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
            TreeDefinition tree,
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

namespace AbilityKit.BehaviorTree.Authoring
{
    [System.Obsolete("Use AbilityKit.BehaviorTree.Authoring.Model.GraphOperations.", false)]
    public static class BtAuthoringGraphOperations
    {
#pragma warning disable CS0618
        public static bool CanConnect(
            BtTreeDefinition tree,
            string parentId,
            string childId,
            int maxChildren,
            out string error)
            => Model.GraphOperations.CanConnect(
                AuthoringCompatibility.ToModel(tree),
                parentId,
                childId,
                maxChildren,
                out error);

        public static bool MoveChild(BtTreeDefinition tree, string parentId, int fromIndex, int toIndex)
        {
            var model = AuthoringCompatibility.ToModel(tree);
            var moved = Model.GraphOperations.MoveChild(model, parentId, fromIndex, toIndex);
            if (!moved) return false;

            var migrated = AuthoringCompatibility.ToLegacy(model);
            tree.Nodes = migrated.Nodes;
            return true;
        }
#pragma warning restore CS0618
    }
}
