#if UNITY_EDITOR
#nullable enable

using System.Collections.Generic;
using AbilityKit.BehaviorTree.Authoring;
using NUnit.Framework;

using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Editor;
using AbilityKit.BehaviorTree.Nodes;
namespace AbilityKit.BehaviorTree.Editor.Tests
{
    public sealed class AuthoringLayoutUtilityTests
    {
        [Test]
        public void CalculateLayout_BalancedTree_CentersParentsOverChildren()
        {
            var tree = Tree(
                Node("root", BuiltInNodeTypes.Sequence, "left", "right"),
                Node("left", BuiltInNodeTypes.Sequence, "left-a", "left-b"),
                Node("right", BuiltInNodeTypes.Sequence, "right-a", "right-b"),
                Node("left-a", BuiltInNodeTypes.Succeed),
                Node("left-b", BuiltInNodeTypes.Succeed),
                Node("right-a", BuiltInNodeTypes.Succeed),
                Node("right-b", BuiltInNodeTypes.Succeed));

            var result = AuthoringLayoutUtility.CalculateLayout(new AuthoringLayoutInput(tree));

            Assert.That(result.NodePositions, Has.Count.EqualTo(tree.Nodes.Count));
            Assert.That(Pos(result, "left").Y, Is.EqualTo(Pos(result, "right").Y));
            Assert.That(Pos(result, "left-a").Y, Is.GreaterThan(Pos(result, "left").Y));
            Assert.That(Pos(result, "left").X, Is.EqualTo((Pos(result, "left-a").X + Pos(result, "left-b").X) * 0.5f));
            Assert.That(Pos(result, "root").X, Is.EqualTo((Pos(result, "left").X + Pos(result, "right").X) * 0.5f));
        }

        [Test]
        public void CalculateLayout_UnbalancedTree_AvoidsSiblingSubtreeOverlap()
        {
            var tree = Tree(
                Node("root", BuiltInNodeTypes.Sequence, "deep", "leaf"),
                Node("deep", BuiltInNodeTypes.Sequence, "deep-a", "deep-b"),
                Node("deep-a", BuiltInNodeTypes.Sequence, "deep-a-1", "deep-a-2"),
                Node("deep-a-1", BuiltInNodeTypes.Succeed),
                Node("deep-a-2", BuiltInNodeTypes.Succeed),
                Node("deep-b", BuiltInNodeTypes.Succeed),
                Node("leaf", BuiltInNodeTypes.Succeed));

            var result = AuthoringLayoutUtility.CalculateLayout(new AuthoringLayoutInput(tree));

            Assert.That(Pos(result, "deep").Y, Is.EqualTo(Pos(result, "leaf").Y));
            Assert.That(Rect(result, "deep-a-2").Right, Is.LessThanOrEqualTo(Pos(result, "leaf").X));
            Assert.That(Pos(result, "deep-a-1").Y, Is.GreaterThan(Pos(result, "deep").Y));
        }

        [Test]
        public void CalculateLayout_DisconnectedNodes_AreLaidOutAsSeparateComponents()
        {
            var tree = Tree(
                Node("root", BuiltInNodeTypes.Sequence, "child"),
                Node("child", BuiltInNodeTypes.Succeed),
                Node("orphan", BuiltInNodeTypes.Succeed));

            var result = AuthoringLayoutUtility.CalculateLayout(new AuthoringLayoutInput(tree));

            Assert.That(result.NodePositions, Has.Count.EqualTo(3));
            Assert.That(Pos(result, "orphan").Y, Is.EqualTo(Pos(result, "root").Y));
            Assert.That(Pos(result, "orphan").X, Is.GreaterThan(Rect(result, "root").Right));
        }

        [Test]
        public void CalculateLayout_Cycle_SkipsBackEdgeAndReportsCycleNode()
        {
            var tree = Tree(
                Node("root", BuiltInNodeTypes.Sequence, "child"),
                Node("child", BuiltInNodeTypes.Sequence, "root"));

            var result = AuthoringLayoutUtility.CalculateLayout(new AuthoringLayoutInput(tree));

            Assert.That(result.NodePositions.ContainsKey("root"), Is.True);
            Assert.That(result.NodePositions.ContainsKey("child"), Is.True);
            Assert.That(result.CycleNodeIds, Does.Contain("root"));
        }

        [Test]
        public void CalculateLayout_DifferentSizes_UsesMeasuredWidthsForSpacing()
        {
            var tree = Tree(
                Node("root", BuiltInNodeTypes.Sequence, "wide", "narrow"),
                Node("wide", BuiltInNodeTypes.Succeed),
                Node("narrow", BuiltInNodeTypes.Succeed));
            var sizes = new Dictionary<string, AuthoringLayoutSize>
            {
                ["root"] = new(190f, 104f),
                ["wide"] = new(420f, 140f),
                ["narrow"] = new(120f, 80f),
            };

            var result = AuthoringLayoutUtility.CalculateLayout(new AuthoringLayoutInput(
                tree,
                nodeSizes: sizes));

            Assert.That(Rect(result, "wide", sizes).Right, Is.LessThanOrEqualTo(
                Pos(result, "narrow").X - AuthoringLayoutOptions.DefaultColumnSpacing));
            Assert.That(Pos(result, "root").X + 95f, Is.EqualTo(
                (Rect(result, "wide", sizes).X + Rect(result, "narrow", sizes).Right) * 0.5f));
        }

        [Test]
        public void ApplyLayout_FixedNodes_KeepAuthoredPositionAndMovableNodesAvoidThem()
        {
            var document = Document(
                Node("root", BuiltInNodeTypes.Sequence, "left", "right"),
                Node("left", BuiltInNodeTypes.Succeed),
                Node("right", BuiltInNodeTypes.Succeed));
            document.Layout.Add(new NodeLayoutData { NodeId = "right", X = 40f, Y = 210f });

            var changed = AuthoringLayoutUtility.ApplyLayout(
                document,
                new AuthoringLayoutOptions { FixedNodeIds = new[] { "right" } },
                null,
                out var result);

            Assert.That(changed, Is.True);
            Assert.That(Layout(document, "right").X, Is.EqualTo(40f));
            Assert.That(Layout(document, "right").Y, Is.EqualTo(210f));
            Assert.That(Rect(document, "left").Right, Is.LessThanOrEqualTo(
                Layout(document, "right").X - AuthoringLayoutOptions.DefaultColumnSpacing)
                .Or.GreaterThanOrEqualTo(Layout(document, "right").X + AuthoringLayoutOptions.DefaultNodeWidth));
            Assert.That(result.ChangedNodeIds, Does.Not.Contain("right"));
        }

        [Test]
        public void ApplyLayout_LocalSubtree_AnchorsSelectedRootAndLeavesUnscopedNodes()
        {
            var document = Document(
                Node("root", BuiltInNodeTypes.Sequence, "branch", "sibling"),
                Node("branch", BuiltInNodeTypes.Sequence, "leaf-a", "leaf-b"),
                Node("leaf-a", BuiltInNodeTypes.Succeed),
                Node("leaf-b", BuiltInNodeTypes.Succeed),
                Node("sibling", BuiltInNodeTypes.Succeed));
            document.Layout.Add(new NodeLayoutData { NodeId = "branch", X = 600f, Y = 300f });
            document.Layout.Add(new NodeLayoutData { NodeId = "sibling", X = 1200f, Y = 300f });
            document.Groups.Add(new AuthoringGroupData
            {
                Id = "branch-group",
                Title = "Branch",
                NodeIds = { "branch", "leaf-a", "leaf-b" },
            });

            var changed = AuthoringLayoutUtility.ApplyLayout(
                document,
                AuthoringLayoutOptions.Subtree("branch"),
                null,
                out var result);

            Assert.That(changed, Is.True);
            Assert.That(Layout(document, "branch").X, Is.EqualTo(600f));
            Assert.That(Layout(document, "branch").Y, Is.EqualTo(300f));
            Assert.That(Layout(document, "sibling").X, Is.EqualTo(1200f));
            Assert.That(result.NodePositions.ContainsKey("root"), Is.False);
            Assert.That(result.NodePositions.ContainsKey("sibling"), Is.False);
            Assert.That(result.UpdatedGroupIds, Does.Contain("branch-group"));
        }

        private static AuthoringSourceDocument Document(params NodeDefinition[] nodes)
        {
            var document = new AuthoringSourceDocument();
            foreach (var node in nodes) document.Tree.Nodes.Add(node);
            document.Tree.RootNodeId = nodes.Length > 0 ? nodes[0].Id : "";
            return document;
        }

        private static TreeDefinition Tree(params NodeDefinition[] nodes)
        {
            var tree = new TreeDefinition();
            foreach (var node in nodes) tree.Nodes.Add(node);
            tree.RootNodeId = nodes.Length > 0 ? nodes[0].Id : "";
            return tree;
        }

        private static NodeDefinition Node(string id, string type, params string[] childIds)
        {
            var node = new NodeDefinition { Id = id, Type = type };
            node.ChildIds.AddRange(childIds);
            return node;
        }

        private static AuthoringLayoutPosition Pos(AuthoringLayoutResult result, string nodeId)
            => result.NodePositions[nodeId];

        private static AuthoringLayoutRect Rect(AuthoringLayoutResult result, string nodeId)
            => Rect(result, nodeId, new Dictionary<string, AuthoringLayoutSize>());

        private static AuthoringLayoutRect Rect(
            AuthoringLayoutResult result,
            string nodeId,
            IReadOnlyDictionary<string, AuthoringLayoutSize> sizes)
        {
            var position = result.NodePositions[nodeId];
            var size = sizes.TryGetValue(nodeId, out var custom)
                ? custom
                : new AuthoringLayoutSize(
                    AuthoringLayoutOptions.DefaultNodeWidth,
                    AuthoringLayoutOptions.DefaultNodeHeight);
            return new AuthoringLayoutRect(position.X, position.Y, size.Width, size.Height);
        }

        private static NodeLayoutData Layout(AuthoringSourceDocument document, string nodeId)
            => document.Layout.Find(item => item.NodeId == nodeId)!;

        private static AuthoringLayoutRect Rect(AuthoringSourceDocument document, string nodeId)
        {
            var layout = Layout(document, nodeId);
            return new AuthoringLayoutRect(
                layout.X,
                layout.Y,
                AuthoringLayoutOptions.DefaultNodeWidth,
                AuthoringLayoutOptions.DefaultNodeHeight);
        }
    }
}
#endif
