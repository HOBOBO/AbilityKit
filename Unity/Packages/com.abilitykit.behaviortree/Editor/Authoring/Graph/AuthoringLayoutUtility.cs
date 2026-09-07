#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.BehaviorTree.Authoring;

using UnityEngine.Scripting.APIUpdating;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;
namespace AbilityKit.BehaviorTree.Editor
{
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtAuthoringLayoutPosition")]
    public readonly struct AuthoringLayoutPosition
    {
        public AuthoringLayoutPosition(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }
    }

    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtAuthoringLayoutSize")]
    public readonly struct AuthoringLayoutSize
    {
        public AuthoringLayoutSize(float width, float height)
        {
            Width = Math.Max(1f, width);
            Height = Math.Max(1f, height);
        }

        public float Width { get; }
        public float Height { get; }
    }

    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtAuthoringLayoutRect")]
    public readonly struct AuthoringLayoutRect
    {
        public AuthoringLayoutRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = Math.Max(1f, width);
            Height = Math.Max(1f, height);
        }

        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
        public float Right => X + Width;
        public float Bottom => Y + Height;
        public AuthoringLayoutPosition Position => new(X, Y);
    }

    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtAuthoringLayoutOptions")]
    public sealed class AuthoringLayoutOptions
    {
        public const float DefaultColumnSpacing = 220f;
        public const float DefaultRowSpacing = 170f;
        public const float DefaultOriginX = 40f;
        public const float DefaultOriginY = 40f;
        public const float DefaultNodeWidth = 190f;
        public const float DefaultNodeHeight = 104f;

        public string RootNodeId { get; set; } = "";
        public IReadOnlyCollection<string>? LayoutNodeIds { get; set; }
        public IReadOnlyCollection<string>? FixedNodeIds { get; set; }
        public bool AnchorRootToExistingPosition { get; set; }
        public bool PreserveUnscopedNodesAsObstacles { get; set; } = true;
        public float OriginX { get; set; } = DefaultOriginX;
        public float OriginY { get; set; } = DefaultOriginY;
        public float HorizontalSpacing { get; set; } = DefaultColumnSpacing;
        public float VerticalSpacing { get; set; } = DefaultRowSpacing - DefaultNodeHeight;
        public float ComponentSpacing { get; set; } = DefaultColumnSpacing;
        public float DefaultNodeWidthValue { get; set; } = DefaultNodeWidth;
        public float DefaultNodeHeightValue { get; set; } = DefaultNodeHeight;

        public static AuthoringLayoutOptions Full => new();

        public static AuthoringLayoutOptions Subtree(string rootNodeId)
            => new()
            {
                RootNodeId = rootNodeId ?? "",
                AnchorRootToExistingPosition = true,
            };
    }

    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtAuthoringLayoutInput")]
    public sealed class AuthoringLayoutInput
    {
        public AuthoringLayoutInput(
            TreeDefinition tree,
            IReadOnlyDictionary<string, AuthoringLayoutPosition>? existingPositions = null,
            IReadOnlyDictionary<string, AuthoringLayoutSize>? nodeSizes = null,
            AuthoringLayoutOptions? options = null)
        {
            Tree = tree ?? throw new ArgumentNullException(nameof(tree));
            ExistingPositions = existingPositions
                ?? new Dictionary<string, AuthoringLayoutPosition>(StringComparer.Ordinal);
            NodeSizes = nodeSizes
                ?? new Dictionary<string, AuthoringLayoutSize>(StringComparer.Ordinal);
            Options = options ?? AuthoringLayoutOptions.Full;
        }

        public TreeDefinition Tree { get; }
        public IReadOnlyDictionary<string, AuthoringLayoutPosition> ExistingPositions { get; }
        public IReadOnlyDictionary<string, AuthoringLayoutSize> NodeSizes { get; }
        public AuthoringLayoutOptions Options { get; }
    }

    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtAuthoringLayoutResult")]
    public sealed class AuthoringLayoutResult
    {
        public Dictionary<string, AuthoringLayoutPosition> NodePositions { get; } =
            new(StringComparer.Ordinal);
        public List<string> ChangedNodeIds { get; } = new();
        public List<string> CycleNodeIds { get; } = new();
        public List<string> SkippedNodeIds { get; } = new();
        public List<string> UpdatedGroupIds { get; } = new();
    }

    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtAuthoringLayoutUtility")]
    public static class AuthoringLayoutUtility
    {
        private const float PositionTolerance = 0.01f;
        private const float GroupHorizontalPadding = 28f;
        private const float GroupTopPadding = 48f;
        private const float GroupBottomPadding = 28f;

        public static bool EnsureLayout(AuthoringSourceDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            var result = CalculateLayout(CreateInput(document, AuthoringLayoutOptions.Full));
            var changed = false;
            foreach (var node in document.Tree.Nodes)
            {
                if (!result.NodePositions.TryGetValue(node.Id, out var position)
                    || document.Layout.Exists(layout => string.Equals(layout.NodeId, node.Id, StringComparison.Ordinal)))
                {
                    continue;
                }

                document.Layout.Add(new NodeLayoutData
                {
                    NodeId = node.Id,
                    X = position.X,
                    Y = position.Y,
                });
                changed = true;
            }

            return changed;
        }

        public static bool ApplyLayout(AuthoringSourceDocument document)
            => ApplyLayout(document, AuthoringLayoutOptions.Full);

        public static bool ApplyLayout(
            AuthoringSourceDocument document,
            AuthoringLayoutOptions options)
            => ApplyLayout(document, options, null, out _);

        public static bool ApplyLayout(
            AuthoringSourceDocument document,
            AuthoringLayoutOptions options,
            IReadOnlyDictionary<string, AuthoringLayoutSize>? nodeSizes,
            out AuthoringLayoutResult result)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            options ??= AuthoringLayoutOptions.Full;

            result = CalculateLayout(CreateInput(document, options, nodeSizes));
            var changed = false;
            var changedOrScopedNodeIds = new HashSet<string>(result.NodePositions.Keys, StringComparer.Ordinal);
            foreach (var pair in result.NodePositions)
            {
                var layout = document.Layout.Find(item =>
                    string.Equals(item.NodeId, pair.Key, StringComparison.Ordinal));
                if (layout == null)
                {
                    document.Layout.Add(new NodeLayoutData
                    {
                        NodeId = pair.Key,
                        X = pair.Value.X,
                        Y = pair.Value.Y,
                    });
                    changed = true;
                    continue;
                }

                if (IsSamePosition(layout.X, layout.Y, pair.Value)) continue;
                layout.X = pair.Value.X;
                layout.Y = pair.Value.Y;
                changed = true;
            }

            var fullDocumentLayout = string.IsNullOrWhiteSpace(options.RootNodeId)
                && (options.LayoutNodeIds == null || options.LayoutNodeIds.Count == 0);
            foreach (var group in document.Groups)
            {
                if (!fullDocumentLayout && !ContainsAny(group.NodeIds, changedOrScopedNodeIds)) continue;
                if (!UpdateGroupBounds(document, group, nodeSizes, options)) continue;

                result.UpdatedGroupIds.Add(group.Id);
                changed = true;
            }

            return changed;
        }

        public static AuthoringLayoutResult CalculateLayout(AuthoringLayoutInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            var options = input.Options ?? AuthoringLayoutOptions.Full;
            var nodesById = IndexNodes(input.Tree);
            var scope = BuildScope(input.Tree, nodesById, options);
            var roots = BuildRootOrder(input.Tree, scope, options.RootNodeId);
            var result = new AuthoringLayoutResult();
            var measured = new Dictionary<string, MeasuredSubtree>(StringComparer.Ordinal);
            var levelHeights = new Dictionary<int, float>();
            foreach (var root in roots)
            {
                MeasureDepthRelative(root, 0, nodesById, scope, input, options, measured, levelHeights,
                    new HashSet<string>(StringComparer.Ordinal), result);
            }

            var levelOffsets = BuildLevelOffsets(levelHeights, options);
            var nextLeft = options.OriginX;
            foreach (var root in roots)
            {
                if (!measured.TryGetValue(root, out var subtree)) continue;
                Place(subtree, nextLeft, levelOffsets, input, result);
                nextLeft += subtree.Width + Math.Max(0f, options.ComponentSpacing);
            }

            AnchorRootIfRequested(roots, input, options, result);
            RestoreFixedPositions(input, options, result);
            AvoidExistingContours(input, options, scope, result);
            PopulateChangedNodes(input, result);
            return result;
        }

        private static AuthoringLayoutInput CreateInput(
            AuthoringSourceDocument document,
            AuthoringLayoutOptions options,
            IReadOnlyDictionary<string, AuthoringLayoutSize>? nodeSizes = null)
        {
            var existingPositions = new Dictionary<string, AuthoringLayoutPosition>(StringComparer.Ordinal);
            foreach (var layout in document.Layout)
            {
                if (string.IsNullOrWhiteSpace(layout.NodeId) || existingPositions.ContainsKey(layout.NodeId)) continue;
                existingPositions.Add(layout.NodeId, new AuthoringLayoutPosition(layout.X, layout.Y));
            }
            return new AuthoringLayoutInput(document.Tree, existingPositions, nodeSizes, options);
        }

        private static Dictionary<string, NodeDefinition> IndexNodes(TreeDefinition definition)
        {
            var nodesById = new Dictionary<string, NodeDefinition>(StringComparer.Ordinal);
            foreach (var node in definition.Nodes)
            {
                if (!string.IsNullOrWhiteSpace(node.Id) && !nodesById.ContainsKey(node.Id))
                    nodesById.Add(node.Id, node);
            }
            return nodesById;
        }

        private static HashSet<string> BuildScope(
            TreeDefinition definition,
            IReadOnlyDictionary<string, NodeDefinition> nodesById,
            AuthoringLayoutOptions options)
        {
            var explicitScope = ToSet(options.LayoutNodeIds);
            var hasExplicitRoot = !string.IsNullOrWhiteSpace(options.RootNodeId);
            var scope = new HashSet<string>(StringComparer.Ordinal);

            if (hasExplicitRoot)
            {
                if (nodesById.ContainsKey(options.RootNodeId))
                    CollectReachable(options.RootNodeId, nodesById, explicitScope, scope, new HashSet<string>(StringComparer.Ordinal));
                return scope;
            }

            if (explicitScope != null)
            {
                foreach (var nodeId in explicitScope)
                {
                    if (nodesById.ContainsKey(nodeId)) scope.Add(nodeId);
                }
                return scope;
            }

            foreach (var node in definition.Nodes)
            {
                if (!string.IsNullOrWhiteSpace(node.Id) && nodesById.ContainsKey(node.Id)) scope.Add(node.Id);
            }
            return scope;
        }

        private static void CollectReachable(
            string nodeId,
            IReadOnlyDictionary<string, NodeDefinition> nodesById,
            ISet<string>? explicitScope,
            ISet<string> result,
            ISet<string> activePath)
        {
            if (!nodesById.TryGetValue(nodeId, out var node)) return;
            if (explicitScope != null && !explicitScope.Contains(nodeId)) return;
            if (!result.Add(nodeId)) return;
            if (!activePath.Add(nodeId)) return;
            foreach (var childId in node.ChildIds)
                CollectReachable(childId, nodesById, explicitScope, result, activePath);
            activePath.Remove(nodeId);
        }

        private static List<string> BuildRootOrder(
            TreeDefinition definition,
            ISet<string> scope,
            string requestedRootId)
        {
            var roots = new List<string>();
            var parented = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in definition.Nodes)
            {
                if (!scope.Contains(node.Id)) continue;
                foreach (var childId in node.ChildIds)
                {
                    if (scope.Contains(childId)) parented.Add(childId);
                }
            }

            AddRoot(requestedRootId);
            AddRoot(definition.RootNodeId);
            foreach (var node in definition.Nodes)
            {
                if (scope.Contains(node.Id) && !parented.Contains(node.Id)) AddRoot(node.Id);
            }
            foreach (var node in definition.Nodes)
            {
                if (scope.Contains(node.Id)) AddRoot(node.Id);
            }
            return roots;

            void AddRoot(string nodeId)
            {
                if (string.IsNullOrWhiteSpace(nodeId) || !scope.Contains(nodeId) || roots.Contains(nodeId)) return;
                roots.Add(nodeId);
            }
        }

        private static MeasuredSubtree MeasureDepthRelative(
            string nodeId,
            int depth,
            IReadOnlyDictionary<string, NodeDefinition> nodesById,
            ISet<string> scope,
            AuthoringLayoutInput input,
            AuthoringLayoutOptions options,
            IDictionary<string, MeasuredSubtree> measured,
            IDictionary<int, float> levelHeights,
            ISet<string> activePath,
            AuthoringLayoutResult result)
        {
            if (measured.TryGetValue(nodeId, out var existing)) return existing;
            if (!nodesById.TryGetValue(nodeId, out var node))
            {
                result.SkippedNodeIds.Add(nodeId);
                return MeasuredSubtree.Empty(nodeId);
            }

            var size = SizeOf(input, options, nodeId);
            levelHeights[depth] = Math.Max(levelHeights.TryGetValue(depth, out var height) ? height : 0f, size.Height);

            activePath.Add(nodeId);
            var children = new List<MeasuredSubtree>();
            foreach (var childId in node.ChildIds)
            {
                if (!scope.Contains(childId) || !nodesById.ContainsKey(childId)) continue;
                if (activePath.Contains(childId))
                {
                    if (!result.CycleNodeIds.Contains(childId)) result.CycleNodeIds.Add(childId);
                    continue;
                }

                var child = MeasureDepthRelative(
                    childId,
                    depth + 1,
                    nodesById,
                    scope,
                    input,
                    options,
                    measured,
                    levelHeights,
                    activePath,
                    result);
                if (child.NodeId.Length > 0) children.Add(child);
            }
            activePath.Remove(nodeId);

            var childSpan = 0f;
            foreach (var child in children)
            {
                if (childSpan > 0f) childSpan += Math.Max(0f, options.HorizontalSpacing);
                childSpan += child.Width;
            }

            var width = Math.Max(size.Width, childSpan);
            var subtree = new MeasuredSubtree(nodeId, node, size, children, width);
            measured[nodeId] = subtree;
            return subtree;
        }

        private static Dictionary<int, float> BuildLevelOffsets(
            IReadOnlyDictionary<int, float> levelHeights,
            AuthoringLayoutOptions options)
        {
            var offsets = new Dictionary<int, float>();
            var y = options.OriginY;
            var maxDepth = 0;
            foreach (var depth in levelHeights.Keys) maxDepth = Math.Max(maxDepth, depth);
            for (var depth = 0; depth <= maxDepth; depth++)
            {
                offsets[depth] = y;
                var height = levelHeights.TryGetValue(depth, out var value)
                    ? value
                    : Math.Max(1f, options.DefaultNodeHeightValue);
                y += height + Math.Max(0f, options.VerticalSpacing);
            }
            return offsets;
        }

        private static void Place(
            MeasuredSubtree subtree,
            float left,
            IReadOnlyDictionary<int, float> levelOffsets,
            AuthoringLayoutInput input,
            AuthoringLayoutResult result,
            int depth = 0)
        {
            if (result.NodePositions.ContainsKey(subtree.NodeId)) return;

            var x = left + (subtree.Width - subtree.Size.Width) * 0.5f;
            var y = levelOffsets.TryGetValue(depth, out var levelY) ? levelY : input.Options.OriginY;
            result.NodePositions[subtree.NodeId] = new AuthoringLayoutPosition(x, y);

            if (subtree.Children.Count == 0) return;
            var childSpan = 0f;
            foreach (var child in subtree.Children)
            {
                if (childSpan > 0f) childSpan += Math.Max(0f, input.Options.HorizontalSpacing);
                childSpan += child.Width;
            }

            var childLeft = left + (subtree.Width - childSpan) * 0.5f;
            foreach (var child in subtree.Children)
            {
                Place(child, childLeft, levelOffsets, input, result, depth + 1);
                childLeft += child.Width + Math.Max(0f, input.Options.HorizontalSpacing);
            }
        }

        private static void AnchorRootIfRequested(
            IReadOnlyList<string> roots,
            AuthoringLayoutInput input,
            AuthoringLayoutOptions options,
            AuthoringLayoutResult result)
        {
            if (!options.AnchorRootToExistingPosition || string.IsNullOrWhiteSpace(options.RootNodeId)) return;
            if (!input.ExistingPositions.TryGetValue(options.RootNodeId, out var anchor)) return;
            if (!result.NodePositions.TryGetValue(options.RootNodeId, out var current)) return;

            var dx = anchor.X - current.X;
            var dy = anchor.Y - current.Y;
            if (Math.Abs(dx) < PositionTolerance && Math.Abs(dy) < PositionTolerance) return;
            var rootScope = roots.Count == 1 && string.Equals(roots[0], options.RootNodeId, StringComparison.Ordinal)
                ? new HashSet<string>(result.NodePositions.Keys, StringComparer.Ordinal)
                : CollectResultComponent(options.RootNodeId, input.Tree, result.NodePositions);
            foreach (var nodeId in rootScope)
            {
                var position = result.NodePositions[nodeId];
                result.NodePositions[nodeId] = new AuthoringLayoutPosition(position.X + dx, position.Y + dy);
            }
        }

        private static HashSet<string> CollectResultComponent(
            string rootNodeId,
            TreeDefinition tree,
            IReadOnlyDictionary<string, AuthoringLayoutPosition> positions)
        {
            var nodesById = IndexNodes(tree);
            var result = new HashSet<string>(StringComparer.Ordinal);
            Collect(rootNodeId, new HashSet<string>(StringComparer.Ordinal));
            return result;

            void Collect(string nodeId, ISet<string> active)
            {
                if (!positions.ContainsKey(nodeId) || !nodesById.TryGetValue(nodeId, out var node)) return;
                if (!result.Add(nodeId) || !active.Add(nodeId)) return;
                foreach (var childId in node.ChildIds) Collect(childId, active);
                active.Remove(nodeId);
            }
        }

        private static void RestoreFixedPositions(
            AuthoringLayoutInput input,
            AuthoringLayoutOptions options,
            AuthoringLayoutResult result)
        {
            var fixedIds = ToSet(options.FixedNodeIds);
            if (fixedIds == null) return;
            foreach (var nodeId in fixedIds)
            {
                if (!result.NodePositions.ContainsKey(nodeId)) continue;
                if (input.ExistingPositions.TryGetValue(nodeId, out var existing))
                    result.NodePositions[nodeId] = existing;
            }
        }

        private static void AvoidExistingContours(
            AuthoringLayoutInput input,
            AuthoringLayoutOptions options,
            ISet<string> scope,
            AuthoringLayoutResult result)
        {
            var fixedIds = ToSet(options.FixedNodeIds) ?? new HashSet<string>(StringComparer.Ordinal);
            var obstacles = new List<AuthoringLayoutRect>();
            foreach (var nodeId in fixedIds)
            {
                if (!input.ExistingPositions.TryGetValue(nodeId, out var position)) continue;
                obstacles.Add(RectOf(input, options, nodeId, position));
            }

            if (options.PreserveUnscopedNodesAsObstacles)
            {
                foreach (var pair in input.ExistingPositions)
                {
                    if (scope.Contains(pair.Key) || fixedIds.Contains(pair.Key)) continue;
                    obstacles.Add(RectOf(input, options, pair.Key, pair.Value));
                }
            }

            var ordered = new List<string>(result.NodePositions.Keys);
            ordered.Sort((left, right) =>
            {
                var byY = result.NodePositions[left].Y.CompareTo(result.NodePositions[right].Y);
                return byY != 0 ? byY : result.NodePositions[left].X.CompareTo(result.NodePositions[right].X);
            });

            var placed = new List<AuthoringLayoutRect>(obstacles);
            foreach (var nodeId in ordered)
            {
                var position = result.NodePositions[nodeId];
                var rect = RectOf(input, options, nodeId, position);
                if (fixedIds.Contains(nodeId))
                {
                    placed.Add(rect);
                    continue;
                }

                var guard = 0;
                while (TryFindOverlap(rect, placed, Math.Max(0f, options.HorizontalSpacing), out var blocker) && guard++ < 256)
                {
                    rect = new AuthoringLayoutRect(
                        blocker.Right + Math.Max(0f, options.HorizontalSpacing),
                        rect.Y,
                        rect.Width,
                        rect.Height);
                }

                result.NodePositions[nodeId] = rect.Position;
                placed.Add(rect);
            }
        }

        private static bool TryFindOverlap(
            AuthoringLayoutRect rect,
            IEnumerable<AuthoringLayoutRect> candidates,
            float spacing,
            out AuthoringLayoutRect blocker)
        {
            foreach (var candidate in candidates)
            {
                if (rect.Right <= candidate.X - spacing
                    || rect.X >= candidate.Right + spacing
                    || rect.Bottom <= candidate.Y
                    || rect.Y >= candidate.Bottom)
                {
                    continue;
                }

                blocker = candidate;
                return true;
            }

            blocker = default;
            return false;
        }

        private static void PopulateChangedNodes(
            AuthoringLayoutInput input,
            AuthoringLayoutResult result)
        {
            foreach (var pair in result.NodePositions)
            {
                if (input.ExistingPositions.TryGetValue(pair.Key, out var existing)
                    && IsSamePosition(existing.X, existing.Y, pair.Value))
                {
                    continue;
                }

                result.ChangedNodeIds.Add(pair.Key);
            }
        }

        private static AuthoringLayoutSize SizeOf(
            AuthoringLayoutInput input,
            AuthoringLayoutOptions options,
            string nodeId)
        {
            if (input.NodeSizes.TryGetValue(nodeId, out var size)) return size;
            return new AuthoringLayoutSize(options.DefaultNodeWidthValue, options.DefaultNodeHeightValue);
        }

        private static AuthoringLayoutRect RectOf(
            AuthoringLayoutInput input,
            AuthoringLayoutOptions options,
            string nodeId,
            AuthoringLayoutPosition position)
        {
            var size = SizeOf(input, options, nodeId);
            return new AuthoringLayoutRect(position.X, position.Y, size.Width, size.Height);
        }

        private static bool UpdateGroupBounds(
            AuthoringSourceDocument document,
            AuthoringGroupData group,
            IReadOnlyDictionary<string, AuthoringLayoutSize>? nodeSizes,
            AuthoringLayoutOptions options)
        {
            var members = new List<AuthoringLayoutRect>();
            foreach (var nodeId in group.NodeIds)
            {
                var layout = document.Layout.Find(item =>
                    string.Equals(item.NodeId, nodeId, StringComparison.Ordinal));
                if (layout == null) continue;
                var size = nodeSizes != null && nodeSizes.TryGetValue(nodeId, out var customSize)
                    ? customSize
                    : new AuthoringLayoutSize(options.DefaultNodeWidthValue, options.DefaultNodeHeightValue);
                members.Add(new AuthoringLayoutRect(layout.X, layout.Y, size.Width, size.Height));
            }
            if (members.Count == 0) return false;

            var minX = members[0].X;
            var minY = members[0].Y;
            var maxX = members[0].Right;
            var maxY = members[0].Bottom;
            for (var i = 1; i < members.Count; i++)
            {
                minX = Math.Min(minX, members[i].X);
                minY = Math.Min(minY, members[i].Y);
                maxX = Math.Max(maxX, members[i].Right);
                maxY = Math.Max(maxY, members[i].Bottom);
            }

            var x = minX - GroupHorizontalPadding;
            var y = minY - GroupTopPadding;
            var width = maxX - minX + GroupHorizontalPadding * 2f;
            var height = maxY - minY + GroupTopPadding + GroupBottomPadding;
            if (Math.Abs(group.X - x) < PositionTolerance
                && Math.Abs(group.Y - y) < PositionTolerance
                && Math.Abs(group.Width - width) < PositionTolerance
                && Math.Abs(group.Height - height) < PositionTolerance) return false;

            group.X = x;
            group.Y = y;
            group.Width = width;
            group.Height = height;
            return true;
        }

        private static bool ContainsAny(IEnumerable<string> values, ISet<string> candidates)
        {
            foreach (var value in values)
            {
                if (candidates.Contains(value)) return true;
            }
            return false;
        }

        private static HashSet<string>? ToSet(IReadOnlyCollection<string>? values)
        {
            if (values == null || values.Count == 0) return null;
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value)) set.Add(value);
            }
            return set.Count == 0 ? null : set;
        }

        private static bool IsSamePosition(float x, float y, AuthoringLayoutPosition position)
            => Math.Abs(x - position.X) < PositionTolerance
               && Math.Abs(y - position.Y) < PositionTolerance;

        private sealed class MeasuredSubtree
        {
            public MeasuredSubtree(
                string nodeId,
                NodeDefinition node,
                AuthoringLayoutSize size,
                List<MeasuredSubtree> children,
                float width)
            {
                NodeId = nodeId;
                Node = node;
                Size = size;
                Children = children;
                Width = Math.Max(size.Width, width);
            }

            private MeasuredSubtree(string nodeId)
            {
                NodeId = nodeId;
                Node = null!;
                Size = new AuthoringLayoutSize(1f, 1f);
                Children = new List<MeasuredSubtree>();
                Width = 1f;
            }

            public string NodeId { get; }
            public NodeDefinition Node { get; }
            public AuthoringLayoutSize Size { get; }
            public List<MeasuredSubtree> Children { get; }
            public float Width { get; }

            public static MeasuredSubtree Empty(string nodeId) => new(nodeId);
        }
    }
}
