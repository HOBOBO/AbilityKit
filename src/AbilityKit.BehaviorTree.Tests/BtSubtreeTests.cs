using System.Collections.Generic;
using AbilityKit.Deterministic;
using Xunit;
using static AbilityKit.BehaviorTree.Tests.TestNodeTypes;

namespace AbilityKit.BehaviorTree.Tests
{
    /// <summary>子树引用节点：内联展开、前缀、黑板合并、环检测、来源追踪、运行时/快照兼容。</summary>
    public sealed class BtSubtreeTests
    {
        private sealed class DictionaryResolver : IBtTreeDefinitionResolver
        {
            private readonly Dictionary<string, BtTreeDefinition> _trees = new();
            public void Add(BtTreeDefinition tree) => _trees[tree.TreeId] = tree;
            public bool TryResolve(string treeId, out BtTreeDefinition definition) => _trees.TryGetValue(treeId, out definition!);
        }

        private static BtTreeDefinition LeafTree(string treeId, string leafType, string leafId)
        {
            var tree = new BtTreeDefinition { TreeId = treeId };
            tree.Nodes.Add(new BtNodeDefinition { Id = leafId, Type = leafType });
            tree.RootNodeId = leafId;
            return tree;
        }

        [Fact]
        public void Expand_SingleLevel_InlinesReferencedTreeWithPrefix()
        {
            // parent: Sequence[ subtree(skill), succeed ]
            // skill: Wait
            var skill = new BtTreeDefinition { TreeId = "skill_tree" };
            skill.Nodes.Add(new BtNodeDefinition { Id = "wait", Type = BtBuiltInNodeTypes.Wait });
            skill.RootNodeId = "wait";

            var parent = new TreeBuilder()
                .Node("root", BtBuiltInNodeTypes.Sequence, "sub", "finish")
                .Node("sub", BtBuiltInNodeTypes.Subtree)
                .Node("finish", BtBuiltInNodeTypes.Succeed)
                .Root("root");
            parent.Nodes[1].Properties.Set(BtSubtreeNode.TreeIdProperty, BtPropertyValue.Of("skill_tree"));

            var resolver = new DictionaryResolver();
            resolver.Add(skill);
            var expansion = BtTreeCompiler.ExpandReferences(parent, resolver);

            Assert.Equal("root", expansion.Definition.RootNodeId);
            // 无子树节点残留
            Assert.DoesNotContain(expansion.Definition.Nodes, n => n.Type == BtBuiltInNodeTypes.Subtree);
            // 被引用节点以 "sub." 前缀内联
            Assert.Contains(expansion.Definition.Nodes, n => n.Id == "sub.wait");
            // root 的第二个子节点被替换为内联根
            var rootNode = expansion.Definition.Nodes.Find(n => n.Id == "root")!;
            Assert.Contains("sub.wait", rootNode.ChildIds);
            // 来源追踪
            Assert.Equal("skill_tree", expansion.NodeSourceTree["sub.wait"]);
            Assert.Equal(parent.TreeId, expansion.NodeSourceTree["root"]);
            Assert.Equal("wait", expansion.NodeSourceNode["sub.wait"]);
            Assert.Equal("root", expansion.NodeSourceNode["root"]);

            // 展开后可运行（wait 结束 → 完成）
            var registry = CreateRegistry();
            var runtime = BtTreeRuntime.Create(parent, registry, null, null, resolver);
            runtime.Enable();
            Assert.NotNull(runtime.NodeSourceTree);
            runtime.Update(1, Fixed64.Zero);
            runtime.Update(2, Fixed64.One);   // wait 默认 1s
            Assert.Equal(BtNodeState.Success, runtime.RootNodeState);
        }

        [Fact]
        public void Expand_NestedSubtree_CompoundsPrefixes()
        {
            var inner = LeafTree("inner", BtBuiltInNodeTypes.Succeed, "leaf");
            var middle = new BtTreeDefinition { TreeId = "middle" };
            middle.Nodes.Add(new BtNodeDefinition { Id = "mid_sub", Type = BtBuiltInNodeTypes.Subtree });
            middle.Nodes[0].Properties.Set(BtSubtreeNode.TreeIdProperty, BtPropertyValue.Of("inner"));
            middle.RootNodeId = "mid_sub";

            var parent = new TreeBuilder()
                .Node("root", BtBuiltInNodeTypes.Sequence, "sub")
                .Node("sub", BtBuiltInNodeTypes.Subtree)
                .Root("root");
            parent.Nodes[1].Properties.Set(BtSubtreeNode.TreeIdProperty, BtPropertyValue.Of("middle"));

            var resolver = new DictionaryResolver();
            resolver.Add(inner);
            resolver.Add(middle);
            var expansion = BtTreeCompiler.ExpandReferences(parent, resolver);

            Assert.Contains(expansion.Definition.Nodes, n => n.Id == "sub.mid_sub.leaf");
            Assert.Equal("inner", expansion.NodeSourceTree["sub.mid_sub.leaf"]);
        }

        [Fact]
        public void Expand_Cycle_Throws()
        {
            var a = new BtTreeDefinition { TreeId = "a" };
            a.Nodes.Add(new BtNodeDefinition { Id = "ra", Type = BtBuiltInNodeTypes.Subtree });
            a.Nodes[0].Properties.Set(BtSubtreeNode.TreeIdProperty, BtPropertyValue.Of("b"));
            a.RootNodeId = "ra";

            var b = new BtTreeDefinition { TreeId = "b" };
            b.Nodes.Add(new BtNodeDefinition { Id = "rb", Type = BtBuiltInNodeTypes.Subtree });
            b.Nodes[0].Properties.Set(BtSubtreeNode.TreeIdProperty, BtPropertyValue.Of("a"));
            b.RootNodeId = "rb";

            var resolver = new DictionaryResolver();
            resolver.Add(a);
            resolver.Add(b);
            Assert.Throws<System.InvalidOperationException>(() => BtTreeCompiler.ExpandReferences(a, resolver));
        }

        [Fact]
        public void Expand_UnknownTreeId_Throws()
        {
            var parent = new TreeBuilder()
                .Node("root", BtBuiltInNodeTypes.Subtree)
                .Root("root");
            parent.Nodes[0].Properties.Set(BtSubtreeNode.TreeIdProperty, BtPropertyValue.Of("missing"));

            Assert.Throws<System.InvalidOperationException>(
                () => BtTreeCompiler.ExpandReferences(parent, new DictionaryResolver()));
        }

        [Fact]
        public void Expand_MergesBlackboard_AndRejectsTypeConflict()
        {
            var child = new BtTreeDefinition { TreeId = "child" };
            child.Blackboard.Keys.Add(new BtBlackboardKeyDefinition { Name = "self.hp", Type = BtValueType.Int64 });
            child.Nodes.Add(new BtNodeDefinition { Id = "c", Type = BtBuiltInNodeTypes.Succeed });
            child.RootNodeId = "c";

            var parent = new TreeBuilder()
                .Blackboard("target.dist", BtValueType.Fixed64)
                .Node("root", BtBuiltInNodeTypes.Subtree)
                .Root("root");
            parent.Nodes[0].Properties.Set(BtSubtreeNode.TreeIdProperty, BtPropertyValue.Of("child"));

            var resolver = new DictionaryResolver();
            resolver.Add(child);
            var expansion = BtTreeCompiler.ExpandReferences(parent, resolver);
            // 并集：两个 key 都在
            Assert.Contains(expansion.Definition.Blackboard.Keys, k => k.Name == "target.dist");
            Assert.Contains(expansion.Definition.Blackboard.Keys, k => k.Name == "self.hp");

            // 同名不同类型 → 冲突
            var child2 = new BtTreeDefinition { TreeId = "child2" };
            child2.Blackboard.Keys.Add(new BtBlackboardKeyDefinition { Name = "target.dist", Type = BtValueType.Bool });
            child2.Nodes.Add(new BtNodeDefinition { Id = "c2", Type = BtBuiltInNodeTypes.Succeed });
            child2.RootNodeId = "c2";
            var resolver2 = new DictionaryResolver();
            resolver2.Add(child2);
            Assert.Throws<System.InvalidOperationException>(
                () => BtTreeCompiler.ExpandReferences(parent, resolver2));
        }

        [Fact]
        public void Expansion_RecordsSubtreeInstances_ForCrossTreeNavigation()
        {
            var skill = LeafTree("skill", BtBuiltInNodeTypes.Succeed, "w");
            var parent = new TreeBuilder()
                .Node("root", BtBuiltInNodeTypes.Sequence, "sub")
                .Node("sub", BtBuiltInNodeTypes.Subtree)
                .Root("root");
            parent.Nodes[1].Properties.Set(BtSubtreeNode.TreeIdProperty, BtPropertyValue.Of("skill"));

            var resolver = new DictionaryResolver();
            resolver.Add(skill);
            var expansion = BtTreeCompiler.ExpandReferences(parent, resolver);

            Assert.Single(expansion.SubtreeInstances);
            Assert.Equal("sub.w", expansion.SubtreeInstances[0].InlinedRootNodeId);
            Assert.Equal("skill", expansion.SubtreeInstances[0].ReferencedTreeId);

            // 运行时经调试视图暴露给观察端
            var registry = CreateRegistry();
            var runtime = BtTreeRuntime.Create(parent, registry, null, null, resolver);
            runtime.Enable();
            var view = (IBtTreeDebugView)runtime;
            Assert.Single(view.SubtreeInstances);
            Assert.Equal("skill", view.SubtreeInstances[0].ReferencedTreeId);
            Assert.Equal("skill", view.NodeSourceTree!["sub.w"]);
            Assert.Equal("w", view.NodeSourceNode!["sub.w"]);
        }

        [Fact]
        public void Expand_IsDeterministic_AndSnapshotCompatible()
        {
            var skill = LeafTree("skill", BtBuiltInNodeTypes.Wait, "w");
            var parent = new TreeBuilder()
                .Node("root", BtBuiltInNodeTypes.Sequence, "sub")
                .Node("sub", BtBuiltInNodeTypes.Subtree)
                .Root("root");
            parent.Nodes[1].Properties.Set(BtSubtreeNode.TreeIdProperty, BtPropertyValue.Of("skill"));

            var resolver = new DictionaryResolver();
            resolver.Add(skill);

            // 两次展开同哈希
            var a = BtTreeCompiler.ExpandReferences(parent, resolver).Definition;
            var b = BtTreeCompiler.ExpandReferences(parent, resolver).Definition;
            Assert.Equal(a.ComputeDefinitionHash(), b.ComputeDefinitionHash());

            // 快照往返
            var runtime = BtTreeRuntime.Create(parent, CreateRegistry(), null, new BtTreeRunOptions { Seed = 7 }, resolver);
            runtime.Enable();
            runtime.Update(1, Fixed64.Zero);
            var snapshot = runtime.CaptureState();
            var restored = BtTreeRuntime.Create(parent, CreateRegistry(), null, new BtTreeRunOptions { Seed = 7 }, resolver);
            restored.Enable();
            restored.RestoreState(snapshot);
            Assert.Equal(runtime.RootNodeState, restored.RootNodeState);
        }
    }
}
