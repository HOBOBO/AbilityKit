#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using AbilityKit.BehaviorTree.Authoring;
using NUnit.Framework;

using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Editor;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Editor.Tests
{
    public sealed class AuthoringDocumentCatalogTests
    {
        [SetUp]
        public void SetUp() => DebugRegistry.ClearForTests();

        [Test]
        public void CustomProvider_MapsExpandedNodeBackToSourceMetadata()
        {
            var child = new TreeDefinition { TreeId = "child" };
            child.Nodes.Add(new NodeDefinition { Id = "leaf", Type = BuiltInNodeTypes.Succeed });
            child.RootNodeId = "leaf";

            var parent = new TreeDefinition { TreeId = "parent" };
            var subtree = new NodeDefinition { Id = "sub", Type = BuiltInNodeTypes.Subtree };
            subtree.Properties.Set(SubtreeNode.TreeIdProperty, PropertyValue.Of("child"));
            parent.Nodes.Add(subtree);
            parent.RootNodeId = "sub";

            var childAuthoring = TreeExporter.Import(child);
            childAuthoring.GetOrCreateNodeMetadata("leaf").DisplayName = "Authored Leaf";
            var provider = new StaticProvider(childAuthoring);
            var resolver = new StaticResolver(child);
            var registry = new NodeRegistry();
            BuiltInNodes.RegisterAll(registry);

            using var registration = AuthoringDocumentCatalog.RegisterProvider(provider);
            using var runtime = CreateRuntime(parent, registry, resolver);
            var observation = AuthoringDocumentCatalog.BuildObservationDocument(DebugViewFor(parent.TreeId), registry);

            Assert.That(observation.TryGetNodeMetadata("sub.leaf", out var metadata), Is.True);
            Assert.That(metadata.DisplayName, Is.EqualTo("Authored Leaf"));
        }

        [Test]
        public void HigherPriorityProvider_WinsWhenTreeIdsConflict()
        {
            var child = new TreeDefinition { TreeId = "priority-child" };
            child.Nodes.Add(new NodeDefinition { Id = "leaf", Type = BuiltInNodeTypes.Succeed });
            child.RootNodeId = "leaf";

            var parent = new TreeDefinition { TreeId = "priority-parent" };
            var subtree = new NodeDefinition { Id = "sub", Type = BuiltInNodeTypes.Subtree };
            subtree.Properties.Set(SubtreeNode.TreeIdProperty, PropertyValue.Of(child.TreeId));
            parent.Nodes.Add(subtree);
            parent.RootNodeId = subtree.Id;

            var low = TreeExporter.Import(child);
            low.GetOrCreateNodeMetadata("leaf").DisplayName = "Low priority";
            var high = TreeExporter.Import(child);
            high.GetOrCreateNodeMetadata("leaf").DisplayName = "High priority";
            var registry = new NodeRegistry();
            BuiltInNodes.RegisterAll(registry);

            using var highRegistration = AuthoringDocumentCatalog.RegisterProvider(new StaticProvider(high), 20);
            using var lowRegistration = AuthoringDocumentCatalog.RegisterProvider(new StaticProvider(low), 10);
            using var runtime = CreateRuntime(parent, registry, new StaticResolver(child));
            var observation = AuthoringDocumentCatalog.BuildObservationDocument(DebugViewFor(parent.TreeId), registry);

            Assert.That(observation.TryGetNodeMetadata("sub.leaf", out var metadata), Is.True);
            Assert.That(metadata.DisplayName, Is.EqualTo("High priority"));
        }

        [Test]
        public void DuplicateProviderRegistrations_HaveIndependentLifetimes()
        {
            var tree = new TreeDefinition { TreeId = "duplicate-registration" };
            tree.Nodes.Add(new NodeDefinition { Id = "root", Type = BuiltInNodeTypes.Succeed });
            tree.RootNodeId = "root";
            var document = TreeExporter.Import(tree);
            document.GetOrCreateNodeMetadata("root").DisplayName = "Still registered";
            var provider = new StaticProvider(document);
            var registry = new NodeRegistry();
            BuiltInNodes.RegisterAll(registry);

            using var first = AuthoringDocumentCatalog.RegisterProvider(provider, 10);
            var second = AuthoringDocumentCatalog.RegisterProvider(provider, 20);
            second.Dispose();
            using var runtime = CreateRuntime(tree, registry, null);
            var observation = AuthoringDocumentCatalog.BuildObservationDocument(DebugViewFor(tree.TreeId), registry);

            Assert.That(observation.TryGetNodeMetadata("root", out var metadata), Is.True);
            Assert.That(metadata.DisplayName, Is.EqualTo("Still registered"));
        }

        [Test]
        public void ObservationFallback_UsesTopDownCenteredLayout()
        {
            var tree = new TreeDefinition { TreeId = "layout-" + System.Guid.NewGuid().ToString("N") };
            tree.Nodes.Add(Node("root", BuiltInNodeTypes.Sequence, "left", "right"));
            tree.Nodes.Add(Node("left", BuiltInNodeTypes.Sequence, "left-a", "left-b"));
            tree.Nodes.Add(Node("right", BuiltInNodeTypes.Sequence, "right-a"));
            tree.Nodes.Add(Node("left-a", BuiltInNodeTypes.Succeed));
            tree.Nodes.Add(Node("left-b", BuiltInNodeTypes.Succeed));
            tree.Nodes.Add(Node("right-a", BuiltInNodeTypes.Succeed));
            tree.RootNodeId = "root";
            var registry = new NodeRegistry();
            BuiltInNodes.RegisterAll(registry);

            using var runtime = CreateRuntime(tree, registry, null);
            var observation = AuthoringDocumentCatalog.BuildObservationDocument(DebugViewFor(tree.TreeId), registry);

            Assert.That(observation.Layout, Has.Count.EqualTo(tree.Nodes.Count));
            var root = observation.Layout.Find(item => item.NodeId == "root");
            var left = observation.Layout.Find(item => item.NodeId == "left");
            var right = observation.Layout.Find(item => item.NodeId == "right");
            var leftA = observation.Layout.Find(item => item.NodeId == "left-a");
            var leftB = observation.Layout.Find(item => item.NodeId == "left-b");
            var rightA = observation.Layout.Find(item => item.NodeId == "right-a");

            Assert.That(left!.Y, Is.GreaterThan(root!.Y));
            Assert.That(right!.Y, Is.EqualTo(left.Y));
            Assert.That(leftA!.Y, Is.GreaterThan(left.Y));
            Assert.That(leftB!.Y, Is.EqualTo(leftA.Y));
            Assert.That(rightA!.Y, Is.EqualTo(leftA.Y));
            Assert.That(left.X, Is.EqualTo((leftA.X + leftB.X) * 0.5f));
            Assert.That(root.X, Is.GreaterThan(left.X));
            Assert.That(root.X, Is.LessThan(right.X));
        }

        [Test]
        public void AutoLayout_ReordersNodesAndGroupsWithoutMovingNotes()
        {
            var document = new AuthoringSourceDocument();
            document.Tree.Nodes.Add(Node("root", BuiltInNodeTypes.Sequence, "left", "right"));
            document.Tree.Nodes.Add(Node("left", BuiltInNodeTypes.Succeed));
            document.Tree.Nodes.Add(Node("right", BuiltInNodeTypes.Succeed));
            document.Tree.RootNodeId = "root";
            document.Layout.Add(new NodeLayoutData { NodeId = "root", X = 800f, Y = 900f });
            document.Layout.Add(new NodeLayoutData { NodeId = "left", X = 10f, Y = 15f });
            document.Layout.Add(new NodeLayoutData { NodeId = "right", X = 12f, Y = 18f });
            document.Groups.Add(new AuthoringGroupData
            {
                Id = "children",
                Title = "Children",
                X = 0f,
                Y = 0f,
                Width = 20f,
                Height = 20f,
                NodeIds = { "left", "right" },
            });
            document.Notes.Add(new AuthoringNoteData
            {
                Id = "note",
                Text = "Keep me",
                X = 912f,
                Y = 734f,
            });

            Assert.That(AuthoringLayoutUtility.ApplyLayout(document), Is.True);

            var root = document.Layout.Find(item => item.NodeId == "root")!;
            var left = document.Layout.Find(item => item.NodeId == "left")!;
            var right = document.Layout.Find(item => item.NodeId == "right")!;
            Assert.That(left.Y, Is.GreaterThan(root.Y));
            Assert.That(right.Y, Is.EqualTo(left.Y));
            Assert.That(root.X, Is.EqualTo((left.X + right.X) * 0.5f));
            Assert.That(document.Groups[0].Width, Is.GreaterThan(right.X - left.X));
            Assert.That(document.Notes[0].X, Is.EqualTo(912f));
            Assert.That(document.Notes[0].Y, Is.EqualTo(734f));
        }

        private static NodeDefinition Node(string id, string type, params string[] childIds)
        {
            var node = new NodeDefinition { Id = id, Type = type };
            node.ChildIds.AddRange(childIds);
            return node;
        }

        private static TreeRuntime CreateRuntime(TreeDefinition tree, NodeRegistry registry, TreeDefinitionResolver? resolver)
        {
            var runtime = TreeRuntime.Create(
                tree,
                registry,
                options: new TreeRunOptions { DebugName = tree.TreeId },
                subtreeResolver: resolver);
            runtime.Enable();
            return runtime;
        }

        private static TreeDebugView DebugViewFor(string treeId)
            => DebugRegistry.GetEntries().Single(entry => entry.View.TreeId == treeId).View;

        private sealed class StaticProvider : IAuthoringDocumentProvider
        {
            private readonly AuthoringSourceDocument _document;
            public StaticProvider(AuthoringSourceDocument document) => _document = document;
            public IEnumerable<AuthoringSourceDocument> LoadDocuments()
            {
                yield return _document;
            }
        }

        private sealed class StaticResolver : TreeDefinitionResolver
        {
            private readonly TreeDefinition _definition;
            public StaticResolver(TreeDefinition definition) => _definition = definition;
            public bool TryResolve(string treeId, out TreeDefinition definition)
            {
                definition = _definition;
                return treeId == _definition.TreeId;
            }
        }
    }
}
#endif
