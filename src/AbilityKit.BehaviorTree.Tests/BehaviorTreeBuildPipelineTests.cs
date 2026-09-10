using System.Collections.Generic;
using System.IO;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using AbilityKit.Deterministic;
using Xunit;

namespace AbilityKit.BehaviorTree.Tests
{
    public sealed class BehaviorTreeBuildPipelineTests
    {
        private const string NullFactoryType = "test.nullFactory";
        private const string ThrowingFactoryType = "test.throwingFactory";

        private sealed class DictionaryResolver : TreeDefinitionResolver
        {
            private readonly Dictionary<string, TreeDefinition> _definitions =
                new(System.StringComparer.Ordinal);

            public void Add(TreeDefinition definition) => _definitions.Add(definition.TreeId, definition);

            public bool TryResolve(string treeId, out TreeDefinition definition)
            {
                if (_definitions.TryGetValue(treeId, out var source))
                {
                    definition = source.DeepClone();
                    return true;
                }

                definition = null!;
                return false;
            }
        }

        [Fact]
        public void Build_MissingSubtreeResolver_ReturnsStableDiagnostic()
        {
            var result = BehaviorTreeBuildPipeline.Build(
                SubtreeDocument("parent", "missing"),
                BuiltinRegistry());

            Assert.False(result.Success);
            var diagnostic = Assert.Single(result.Diagnostics,
                item => item.Code == BehaviorTreeBuildPipeline.MissingResolverCode);
            Assert.Equal("sub", diagnostic.NodeId);
        }

        [Fact]
        public void Build_MissingSubtree_ReturnsExpansionDiagnostic()
        {
            var result = BehaviorTreeBuildPipeline.Build(
                SubtreeDocument("parent", "missing"),
                BuiltinRegistry(),
                new DictionaryResolver());

            Assert.False(result.Success);
            var diagnostic = Assert.Single(result.Diagnostics,
                item => item.Code == BehaviorTreeBuildPipeline.ExpansionFailedCode);
            Assert.Equal("sub", diagnostic.NodeId);
            Assert.Contains("missing", diagnostic.Message);
        }

        [Fact]
        public void Build_CrossTreeCycle_ReturnsExpansionDiagnostic()
        {
            var a = SubtreeDocument("a", "b");
            var b = SubtreeDocument("b", "a");
            var resolver = new DictionaryResolver();
            resolver.Add(a.Tree);
            resolver.Add(b.Tree);

            var result = BehaviorTreeBuildPipeline.Build(a, BuiltinRegistry(), resolver);

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics,
                item => item.Code == BehaviorTreeBuildPipeline.ExpansionFailedCode
                    && item.Message.Contains("循环引用"));
        }

        [Fact]
        public void Build_SubtreeBindingTypeMismatch_ReturnsExpansionDiagnostic()
        {
            var child = LeafDocument("child");
            child.Tree.Blackboard.Keys.Add(new BlackboardKeyDefinition
            {
                Name = "value",
                Type = TreeValueType.Bool,
            });
            var parent = SubtreeDocument("parent", "child");
            parent.Tree.Blackboard.Keys.Add(new BlackboardKeyDefinition
            {
                Name = "value",
                Type = TreeValueType.Int64,
            });
            parent.Tree.Nodes[0].SubtreeBlackboard = new SubtreeBlackboardConfiguration
            {
                Bindings = new List<SubtreeBlackboardBinding>
                {
                    new() { SubtreeKey = "value", ParentKey = "value" },
                },
            };
            var resolver = new DictionaryResolver();
            resolver.Add(child.Tree);

            var result = BehaviorTreeBuildPipeline.Build(parent, BuiltinRegistry(), resolver);

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics,
                item => item.Code == BehaviorTreeBuildPipeline.ExpansionFailedCode
                    && item.Message.Contains("类型不一致"));
        }

        [Fact]
        public void Build_FactoryReturningNull_ReturnsRuntimeCreationDiagnostic()
        {
            var registry = BuiltinRegistry();
            registry.Register(new NodeDescriptor(
                NullFactoryType, "Null", "Test", NodeKind.Action, 0, 0, () => null!));

            var result = BehaviorTreeBuildPipeline.Build(
                SingleNodeDocument("null_factory", NullFactoryType), registry);

            Assert.False(result.Success);
            var diagnostic = Assert.Single(result.Diagnostics,
                item => item.Code == BehaviorTreeBuildPipeline.RuntimeCreationFailedCode);
            Assert.Equal("root", diagnostic.NodeId);
            Assert.Contains("returned null", diagnostic.Message);
        }

        [Fact]
        public void Build_FactoryThrowing_ReturnsRuntimeCreationDiagnostic()
        {
            var registry = BuiltinRegistry();
            registry.Register(new NodeDescriptor(
                ThrowingFactoryType,
                "Throw",
                "Test",
                NodeKind.Action,
                0,
                0,
                () => throw new System.InvalidOperationException("factory exploded")));

            var result = BehaviorTreeBuildPipeline.Build(
                SingleNodeDocument("throwing_factory", ThrowingFactoryType), registry);

            Assert.False(result.Success);
            var diagnostic = Assert.Single(result.Diagnostics,
                item => item.Code == BehaviorTreeBuildPipeline.RuntimeCreationFailedCode);
            Assert.Equal("root", diagnostic.NodeId);
            Assert.Contains("factory exploded", diagnostic.Message);
        }

        [Fact]
        public void Build_OnInitSemanticFailure_ReturnsRuntimeCreationDiagnosticWithNode()
        {
            var document = SingleNodeDocument("invalid_wait", BuiltInNodeTypes.Wait);
            document.Tree.Nodes[0].Properties.Set(
                WaitNode.DurationSecondsProperty,
                PropertyValue.Of(Fixed64.Zero));

            var result = BehaviorTreeBuildPipeline.Build(document, BuiltinRegistry());

            Assert.False(result.Success);
            var diagnostic = Assert.Single(result.Diagnostics,
                item => item.Code == BehaviorTreeBuildPipeline.RuntimeCreationFailedCode);
            Assert.Equal("root", diagnostic.NodeId);
            Assert.Contains("duration", diagnostic.Message);
        }

        [Fact]
        public void Build_SuccessPreservesSourceReferenceAndReturnsExpandedDefinition()
        {
            var child = LeafDocument("child");
            var parent = SubtreeDocument("parent", "child");
            var resolver = new DictionaryResolver();
            resolver.Add(child.Tree);

            var result = BehaviorTreeBuildPipeline.Build(parent, BuiltinRegistry(), resolver);

            Assert.True(result.Success);
            Assert.NotNull(result.Expansion);
            Assert.Contains(result.SourceDefinition.Nodes, node => node.Type == BuiltInNodeTypes.Subtree);
            Assert.DoesNotContain(result.CompiledDefinition!.Nodes, node => node.Type == BuiltInNodeTypes.Subtree);
            Assert.Contains(result.CompiledDefinition.Nodes, node => node.Id == "sub.root");
        }

        [Fact]
        public void ExportAll_ResolvesProjectSubtreesAndKeepsSourceReference()
        {
            var root = Path.Combine(Path.GetTempPath(), "ak-bt-build-" + System.Guid.NewGuid().ToString("N"));
            var target = Path.Combine(root, "out");
            try
            {
                var child = LeafDocument("child");
                var parent = SubtreeDocument("parent", "child");
                var trees = new List<KeyValuePair<string, AuthoringSourceDocument>>
                {
                    new("parent", parent),
                    new("child", child),
                };

                var report = ExportPipeline.ExportAll(trees, new[] { target }, BuiltinRegistry(), root);

                Assert.All(report, item => Assert.Equal(ExportStatus.Exported, item.Status));
                var exportedParent = TreeJson.Load(File.ReadAllText(Path.Combine(target, "parent.json")));
                Assert.Contains(exportedParent.Nodes, node => node.Type == BuiltInNodeTypes.Subtree);
                Assert.DoesNotContain(exportedParent.Nodes, node => node.Id == "sub.root");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void ExportAll_MissingSubtreeDoesNotOverwriteExistingArtifact()
        {
            var root = Path.Combine(Path.GetTempPath(), "ak-bt-build-" + System.Guid.NewGuid().ToString("N"));
            var target = Path.Combine(root, "out");
            Directory.CreateDirectory(target);
            var path = Path.Combine(target, "parent.json");
            File.WriteAllText(path, "old artifact");
            try
            {
                var parent = SubtreeDocument("parent", "missing");
                var report = ExportPipeline.ExportAll(
                    new[] { new KeyValuePair<string, AuthoringSourceDocument>("parent", parent) },
                    new[] { target },
                    BuiltinRegistry(),
                    root);

                Assert.Equal(ExportStatus.Error, Assert.Single(report).Status);
                Assert.Contains(BehaviorTreeBuildPipeline.ExpansionFailedCode, report[0].Message);
                Assert.Equal("old artifact", File.ReadAllText(path));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        private static NodeRegistry BuiltinRegistry()
        {
            var registry = new NodeRegistry();
            BuiltInNodes.RegisterAll(registry);
            return registry;
        }

        private static AuthoringSourceDocument SingleNodeDocument(string treeId, string type)
        {
            var document = new AuthoringSourceDocument();
            document.Tree.TreeId = treeId;
            document.Tree.RootNodeId = "root";
            document.Tree.Nodes.Add(new NodeDefinition { Id = "root", Type = type });
            return document;
        }

        private static AuthoringSourceDocument LeafDocument(string treeId)
            => SingleNodeDocument(treeId, BuiltInNodeTypes.Succeed);

        private static AuthoringSourceDocument SubtreeDocument(string treeId, string childTreeId)
        {
            var document = SingleNodeDocument(treeId, BuiltInNodeTypes.Subtree);
            document.Tree.Nodes[0].Id = "sub";
            document.Tree.RootNodeId = "sub";
            document.Tree.Nodes[0].Properties.Set(
                SubtreeNode.TreeIdProperty,
                PropertyValue.Of(childTreeId));
            return document;
        }
    }
}
