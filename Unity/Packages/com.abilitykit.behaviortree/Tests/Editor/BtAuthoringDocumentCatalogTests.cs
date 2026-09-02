#if UNITY_EDITOR
using System.Collections.Generic;
using AbilityKit.BehaviorTree.Authoring;
using NUnit.Framework;

namespace AbilityKit.BehaviorTree.Editor.Tests
{
    public sealed class BtAuthoringDocumentCatalogTests
    {
        [Test]
        public void CustomProvider_MapsExpandedNodeBackToSourceMetadata()
        {
            var child = new BtTreeDefinition { TreeId = "child" };
            child.Nodes.Add(new BtNodeDefinition { Id = "leaf", Type = BtBuiltInNodeTypes.Succeed });
            child.RootNodeId = "leaf";

            var parent = new BtTreeDefinition { TreeId = "parent" };
            var subtree = new BtNodeDefinition { Id = "sub", Type = BtBuiltInNodeTypes.Subtree };
            subtree.Properties.Set(BtSubtreeNode.TreeIdProperty, BtPropertyValue.Of("child"));
            parent.Nodes.Add(subtree);
            parent.RootNodeId = "sub";

            var childAuthoring = BtTreeExporter.Import(child);
            childAuthoring.GetOrCreateNodeMetadata("leaf").DisplayName = "Authored Leaf";
            var provider = new StaticProvider(childAuthoring);
            var resolver = new StaticResolver(child);
            var registry = new BtNodeRegistry();
            BtBuiltInNodes.RegisterAll(registry);

            using var registration = BtAuthoringDocumentCatalog.RegisterProvider(provider);
            using var runtime = BtTreeRuntime.Create(parent, registry, subtreeResolver: resolver);
            runtime.Enable();
            var observation = BtAuthoringDocumentCatalog.BuildObservationDocument(runtime, registry);

            Assert.That(observation.TryGetNodeMetadata("sub.leaf", out var metadata), Is.True);
            Assert.That(metadata.DisplayName, Is.EqualTo("Authored Leaf"));
        }

        [Test]
        public void HigherPriorityProvider_WinsWhenTreeIdsConflict()
        {
            var child = new BtTreeDefinition { TreeId = "priority-child" };
            child.Nodes.Add(new BtNodeDefinition { Id = "leaf", Type = BtBuiltInNodeTypes.Succeed });
            child.RootNodeId = "leaf";

            var parent = new BtTreeDefinition { TreeId = "priority-parent" };
            var subtree = new BtNodeDefinition { Id = "sub", Type = BtBuiltInNodeTypes.Subtree };
            subtree.Properties.Set(BtSubtreeNode.TreeIdProperty, BtPropertyValue.Of(child.TreeId));
            parent.Nodes.Add(subtree);
            parent.RootNodeId = subtree.Id;

            var low = BtTreeExporter.Import(child);
            low.GetOrCreateNodeMetadata("leaf").DisplayName = "Low priority";
            var high = BtTreeExporter.Import(child);
            high.GetOrCreateNodeMetadata("leaf").DisplayName = "High priority";
            var registry = new BtNodeRegistry();
            BtBuiltInNodes.RegisterAll(registry);

            using var highRegistration = BtAuthoringDocumentCatalog.RegisterProvider(new StaticProvider(high), 20);
            using var lowRegistration = BtAuthoringDocumentCatalog.RegisterProvider(new StaticProvider(low), 10);
            using var runtime = BtTreeRuntime.Create(parent, registry, subtreeResolver: new StaticResolver(child));
            runtime.Enable();

            var observation = BtAuthoringDocumentCatalog.BuildObservationDocument(runtime, registry);

            Assert.That(observation.TryGetNodeMetadata("sub.leaf", out var metadata), Is.True);
            Assert.That(metadata.DisplayName, Is.EqualTo("High priority"));
        }

        [Test]
        public void DuplicateProviderRegistrations_HaveIndependentLifetimes()
        {
            var tree = new BtTreeDefinition { TreeId = "duplicate-registration" };
            tree.Nodes.Add(new BtNodeDefinition { Id = "root", Type = BtBuiltInNodeTypes.Succeed });
            tree.RootNodeId = "root";
            var document = BtTreeExporter.Import(tree);
            document.GetOrCreateNodeMetadata("root").DisplayName = "Still registered";
            var provider = new StaticProvider(document);
            var registry = new BtNodeRegistry();
            BtBuiltInNodes.RegisterAll(registry);

            using var first = BtAuthoringDocumentCatalog.RegisterProvider(provider, 10);
            var second = BtAuthoringDocumentCatalog.RegisterProvider(provider, 20);
            second.Dispose();
            using var runtime = BtTreeRuntime.Create(tree, registry);
            runtime.Enable();

            var observation = BtAuthoringDocumentCatalog.BuildObservationDocument(runtime, registry);

            Assert.That(observation.TryGetNodeMetadata("root", out var metadata), Is.True);
            Assert.That(metadata.DisplayName, Is.EqualTo("Still registered"));
        }

        [Test]
        public void ObservationFallback_UsesTopDownCenteredLayout()
        {
            var tree = new BtTreeDefinition { TreeId = "layout-" + System.Guid.NewGuid().ToString("N") };
            tree.Nodes.Add(Node("root", BtBuiltInNodeTypes.Sequence, "left", "right"));
            tree.Nodes.Add(Node("left", BtBuiltInNodeTypes.Sequence, "left-a", "left-b"));
            tree.Nodes.Add(Node("right", BtBuiltInNodeTypes.Sequence, "right-a"));
            tree.Nodes.Add(Node("left-a", BtBuiltInNodeTypes.Succeed));
            tree.Nodes.Add(Node("left-b", BtBuiltInNodeTypes.Succeed));
            tree.Nodes.Add(Node("right-a", BtBuiltInNodeTypes.Succeed));
            tree.RootNodeId = "root";
            var registry = new BtNodeRegistry();
            BtBuiltInNodes.RegisterAll(registry);

            using var runtime = BtTreeRuntime.Create(tree, registry);
            runtime.Enable();
            var observation = BtAuthoringDocumentCatalog.BuildObservationDocument(runtime, registry);

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
            Assert.That(root.X, Is.EqualTo((left.X + right.X) * 0.5f));
        }

        [Test]
        public void AutoLayout_ReordersNodesAndGroupsWithoutMovingNotes()
        {
            var document = new BtAuthoringSourceDocument();
            document.Tree.Nodes.Add(Node("root", BtBuiltInNodeTypes.Sequence, "left", "right"));
            document.Tree.Nodes.Add(Node("left", BtBuiltInNodeTypes.Succeed));
            document.Tree.Nodes.Add(Node("right", BtBuiltInNodeTypes.Succeed));
            document.Tree.RootNodeId = "root";
            document.Layout.Add(new BtNodeLayoutData { NodeId = "root", X = 800f, Y = 900f });
            document.Layout.Add(new BtNodeLayoutData { NodeId = "left", X = 10f, Y = 15f });
            document.Layout.Add(new BtNodeLayoutData { NodeId = "right", X = 12f, Y = 18f });
            document.Groups.Add(new BtAuthoringGroupData
            {
                Id = "children",
                Title = "Children",
                X = 0f,
                Y = 0f,
                Width = 20f,
                Height = 20f,
                NodeIds = { "left", "right" },
            });
            document.Notes.Add(new BtAuthoringNoteData
            {
                Id = "note",
                Text = "Keep me",
                X = 912f,
                Y = 734f,
            });

            Assert.That(BtAuthoringLayoutUtility.ApplyLayout(document), Is.True);

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

        private static BtNodeDefinition Node(string id, string type, params string[] childIds)
        {
            var node = new BtNodeDefinition { Id = id, Type = type };
            node.ChildIds.AddRange(childIds);
            return node;
        }

        private sealed class StaticProvider : IBtAuthoringDocumentProvider
        {
            private readonly BtAuthoringSourceDocument _document;
            public StaticProvider(BtAuthoringSourceDocument document) => _document = document;
            public IEnumerable<BtAuthoringSourceDocument> LoadDocuments()
            {
                yield return _document;
            }
        }

        private sealed class StaticResolver : IBtTreeDefinitionResolver
        {
            private readonly BtTreeDefinition _definition;
            public StaticResolver(BtTreeDefinition definition) => _definition = definition;
            public bool TryResolve(string treeId, out BtTreeDefinition definition)
            {
                definition = _definition;
                return treeId == _definition.TreeId;
            }
        }
    }
}
#endif
