#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.BehaviorTree.Authoring;

namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>编辑器布局补全：按树深度分层，只填充缺失坐标，不覆盖用户布局。</summary>
    public static class BtAuthoringLayoutUtility
    {
        private const float ColumnSpacing = 220f;
        private const float RowSpacing = 170f;
        private const float OriginX = 40f;
        private const float OriginY = 40f;
        private const float NodeWidth = 190f;
        private const float NodeHeight = 104f;
        private const float GroupHorizontalPadding = 28f;
        private const float GroupTopPadding = 48f;
        private const float GroupBottomPadding = 28f;

        public static bool EnsureLayout(BtAuthoringSourceDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            var suggested = BuildSuggestedLayout(document.Tree);
            var changed = false;
            foreach (var node in document.Tree.Nodes)
            {
                if (!suggested.TryGetValue(node.Id, out var position)
                    || document.Layout.Exists(layout => string.Equals(layout.NodeId, node.Id, StringComparison.Ordinal)))
                    continue;

                document.Layout.Add(new BtNodeLayoutData
                {
                    NodeId = node.Id,
                    X = position.X,
                    Y = position.Y,
                });
                changed = true;
            }

            return changed;
        }

        /// <summary>重新整理全部节点，并让已有分组重新包围其成员；不移动画布注释。</summary>
        public static bool ApplyLayout(BtAuthoringSourceDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            var suggested = BuildSuggestedLayout(document.Tree);
            var changed = false;
            foreach (var node in document.Tree.Nodes)
            {
                if (!suggested.TryGetValue(node.Id, out var position)) continue;
                var layout = document.Layout.Find(item =>
                    string.Equals(item.NodeId, node.Id, StringComparison.Ordinal));
                if (layout == null)
                {
                    document.Layout.Add(new BtNodeLayoutData
                    {
                        NodeId = node.Id,
                        X = position.X,
                        Y = position.Y,
                    });
                    changed = true;
                    continue;
                }

                if (Math.Abs(layout.X - position.X) < 0.01f
                    && Math.Abs(layout.Y - position.Y) < 0.01f) continue;
                layout.X = position.X;
                layout.Y = position.Y;
                changed = true;
            }

            foreach (var group in document.Groups)
            {
                if (UpdateGroupBounds(document, group)) changed = true;
            }
            return changed;
        }

        private static Dictionary<string, (float X, float Y)> BuildSuggestedLayout(BtTreeDefinition definition)
        {
            var nodesById = new Dictionary<string, BtNodeDefinition>(StringComparer.Ordinal);
            foreach (var node in definition.Nodes)
            {
                if (!string.IsNullOrWhiteSpace(node.Id) && !nodesById.ContainsKey(node.Id))
                    nodesById.Add(node.Id, node);
            }

            var positions = new Dictionary<string, (float X, float Y)>(StringComparer.Ordinal);
            var activePath = new HashSet<string>(StringComparer.Ordinal);
            var nextLeafColumn = 0;
            PlaceSubtree(
                definition.RootNodeId, 0, nodesById, positions, activePath, ref nextLeafColumn);

            // Invalid or disconnected nodes remain inspectable without disturbing authored positions.
            foreach (var node in definition.Nodes)
            {
                if (!positions.ContainsKey(node.Id))
                    PlaceSubtree(node.Id, 0, nodesById, positions, activePath, ref nextLeafColumn);
            }

            return positions;
        }

        private static (float X, float Y) PlaceSubtree(
            string nodeId,
            int depth,
            IReadOnlyDictionary<string, BtNodeDefinition> nodesById,
            IDictionary<string, (float X, float Y)> positions,
            ISet<string> activePath,
            ref int nextLeafColumn)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || !nodesById.TryGetValue(nodeId, out var node))
                return (OriginX + nextLeafColumn * ColumnSpacing, OriginY + depth * RowSpacing);
            if (positions.TryGetValue(nodeId, out var existing)) return existing;

            activePath.Add(nodeId);
            var childPositions = new List<(float X, float Y)>();
            foreach (var childId in node.ChildIds)
            {
                if (activePath.Contains(childId) || !nodesById.ContainsKey(childId)) continue;
                childPositions.Add(PlaceSubtree(
                    childId, depth + 1, nodesById, positions, activePath, ref nextLeafColumn));
            }

            float x;
            if (childPositions.Count == 0)
            {
                x = OriginX + nextLeafColumn * ColumnSpacing;
                nextLeafColumn++;
            }
            else
            {
                x = (childPositions[0].X + childPositions[childPositions.Count - 1].X) * 0.5f;
            }

            var position = (X: x, Y: OriginY + depth * RowSpacing);
            positions[nodeId] = position;
            activePath.Remove(nodeId);
            return position;
        }

        private static bool UpdateGroupBounds(BtAuthoringSourceDocument document, BtAuthoringGroupData group)
        {
            var members = new List<BtNodeLayoutData>();
            foreach (var nodeId in group.NodeIds)
            {
                var layout = document.Layout.Find(item =>
                    string.Equals(item.NodeId, nodeId, StringComparison.Ordinal));
                if (layout != null) members.Add(layout);
            }
            if (members.Count == 0) return false;

            var minX = members[0].X;
            var minY = members[0].Y;
            var maxX = members[0].X + NodeWidth;
            var maxY = members[0].Y + NodeHeight;
            for (var i = 1; i < members.Count; i++)
            {
                minX = Math.Min(minX, members[i].X);
                minY = Math.Min(minY, members[i].Y);
                maxX = Math.Max(maxX, members[i].X + NodeWidth);
                maxY = Math.Max(maxY, members[i].Y + NodeHeight);
            }

            var x = minX - GroupHorizontalPadding;
            var y = minY - GroupTopPadding;
            var width = maxX - minX + GroupHorizontalPadding * 2f;
            var height = maxY - minY + GroupTopPadding + GroupBottomPadding;
            if (Math.Abs(group.X - x) < 0.01f
                && Math.Abs(group.Y - y) < 0.01f
                && Math.Abs(group.Width - width) < 0.01f
                && Math.Abs(group.Height - height) < 0.01f) return false;

            group.X = x;
            group.Y = y;
            group.Width = width;
            group.Height = height;
            return true;
        }
    }
}
