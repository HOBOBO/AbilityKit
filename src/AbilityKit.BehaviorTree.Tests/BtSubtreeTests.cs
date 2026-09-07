using System.Collections.Generic;
using AbilityKit.Deterministic;
using Xunit;
using static AbilityKit.BehaviorTree.Tests.TestNodeTypes;

namespace AbilityKit.BehaviorTree.Tests
{
    /// <summary>瀛愭爲寮曠敤鑺傜偣锛氬唴鑱斿睍寮€銆佸墠缂€銆侀粦鏉垮悎骞躲€佺幆妫€娴嬨€佹潵婧愯拷韪€佽繍琛屾椂/蹇収鍏煎銆?/summary>
    public sealed class BtSubtreeTests
    {
        private sealed class DictionaryResolver : TreeDefinitionResolver
        {
            private readonly Dictionary<string, TreeDefinition> _trees = new();
            public void Add(TreeDefinition tree) => _trees[tree.TreeId] = tree;
            public bool TryResolve(string treeId, out TreeDefinition definition) => _trees.TryGetValue(treeId, out definition!);
        }

        private static TreeDefinition LeafTree(string treeId, string leafType, string leafId)
        {
            var tree = new TreeDefinition { TreeId = treeId };
            tree.Nodes.Add(new NodeDefinition { Id = leafId, Type = leafType });
            tree.RootNodeId = leafId;
            return tree;
        }

        [Fact]
        public void Expand_SingleLevel_InlinesReferencedTreeWithPrefix()
        {
            // parent: Sequence[ subtree(skill), succeed ]
            // skill: Wait
            var skill = new TreeDefinition { TreeId = "skill_tree" };
            skill.Nodes.Add(new NodeDefinition { Id = "wait", Type = BuiltInNodeTypes.Wait });
            skill.RootNodeId = "wait";

            var parent = new TreeBuilder()
                .Node("root", BuiltInNodeTypes.Sequence, "sub", "finish")
                .Node("sub", BuiltInNodeTypes.Subtree)
                .Node("finish", BuiltInNodeTypes.Succeed)
                .Root("root");
            parent.Nodes[1].Properties.Set(SubtreeNode.TreeIdProperty, PropertyValue.Of("skill_tree"));

            var resolver = new DictionaryResolver();
            resolver.Add(skill);
            var expansion = TreeCompiler.ExpandReferences(parent, resolver);

            Assert.Equal("root", expansion.Definition.RootNodeId);
            Assert.DoesNotContain(expansion.Definition.Nodes, n => n.Type == BuiltInNodeTypes.Subtree);
            // 琚紩鐢ㄨ妭鐐逛互 "sub." 鍓嶇紑鍐呰仈
            Assert.Contains(expansion.Definition.Nodes, n => n.Id == "sub.wait");
            // root 鐨勭浜屼釜瀛愯妭鐐硅鏇挎崲涓哄唴鑱旀牴
            var rootNode = expansion.Definition.Nodes.Find(n => n.Id == "root")!;
            Assert.Contains("sub.wait", rootNode.ChildIds);
            // 鏉ユ簮杩借釜
            Assert.Equal("skill_tree", expansion.NodeSourceTree["sub.wait"]);
            Assert.Equal(parent.TreeId, expansion.NodeSourceTree["root"]);
            Assert.Equal("wait", expansion.NodeSourceNode["sub.wait"]);
            Assert.Equal("root", expansion.NodeSourceNode["root"]);
            var registry = CreateRegistry();
            var runtime = TreeRuntime.Create(parent, registry, null, null, resolver);
            runtime.Enable();
            Assert.NotNull(runtime.NodeSourceTree);
            runtime.Update(1, Fixed64.Zero);
            runtime.Update(2, Fixed64.One);   // wait 榛樿 1s
            Assert.Equal(NodeState.Success, runtime.RootNodeState);
        }

        [Fact]
        public void Expand_NestedSubtree_CompoundsPrefixes()
        {
            var inner = LeafTree("inner", BuiltInNodeTypes.Succeed, "leaf");
            var middle = new TreeDefinition { TreeId = "middle" };
            middle.Nodes.Add(new NodeDefinition { Id = "mid_sub", Type = BuiltInNodeTypes.Subtree });
            middle.Nodes[0].Properties.Set(SubtreeNode.TreeIdProperty, PropertyValue.Of("inner"));
            middle.RootNodeId = "mid_sub";

            var parent = new TreeBuilder()
                .Node("root", BuiltInNodeTypes.Sequence, "sub")
                .Node("sub", BuiltInNodeTypes.Subtree)
                .Root("root");
            parent.Nodes[1].Properties.Set(SubtreeNode.TreeIdProperty, PropertyValue.Of("middle"));

            var resolver = new DictionaryResolver();
            resolver.Add(inner);
            resolver.Add(middle);
            var expansion = TreeCompiler.ExpandReferences(parent, resolver);

            Assert.Contains(expansion.Definition.Nodes, n => n.Id == "sub.mid_sub.leaf");
            Assert.Equal("inner", expansion.NodeSourceTree["sub.mid_sub.leaf"]);
        }

        [Fact]
        public void Expand_Cycle_Throws()
        {
            var a = new TreeDefinition { TreeId = "a" };
            a.Nodes.Add(new NodeDefinition { Id = "ra", Type = BuiltInNodeTypes.Subtree });
            a.Nodes[0].Properties.Set(SubtreeNode.TreeIdProperty, PropertyValue.Of("b"));
            a.RootNodeId = "ra";

            var b = new TreeDefinition { TreeId = "b" };
            b.Nodes.Add(new NodeDefinition { Id = "rb", Type = BuiltInNodeTypes.Subtree });
            b.Nodes[0].Properties.Set(SubtreeNode.TreeIdProperty, PropertyValue.Of("a"));
            b.RootNodeId = "rb";

            var resolver = new DictionaryResolver();
            resolver.Add(a);
            resolver.Add(b);
            Assert.Throws<System.InvalidOperationException>(() => TreeCompiler.ExpandReferences(a, resolver));
        }

        [Fact]
        public void Expand_UnknownTreeId_Throws()
        {
            var parent = new TreeBuilder()
                .Node("root", BuiltInNodeTypes.Subtree)
                .Root("root");
            parent.Nodes[0].Properties.Set(SubtreeNode.TreeIdProperty, PropertyValue.Of("missing"));

            Assert.Throws<System.InvalidOperationException>(
                () => TreeCompiler.ExpandReferences(parent, new DictionaryResolver()));
        }

        [Fact]
        public void Expand_MergesBlackboard_AndRejectsTypeConflict()
        {
            var child = new TreeDefinition { TreeId = "child" };
            child.Blackboard.Keys.Add(new BlackboardKeyDefinition { Name = "self.hp", Type = TreeValueType.Int64 });
            child.Nodes.Add(new NodeDefinition { Id = "c", Type = BuiltInNodeTypes.Succeed });
            child.RootNodeId = "c";

            var parent = new TreeBuilder()
                .Blackboard("target.dist", TreeValueType.Fixed64)
                .Node("root", BuiltInNodeTypes.Subtree)
                .Root("root");
            parent.Nodes[0].Properties.Set(SubtreeNode.TreeIdProperty, PropertyValue.Of("child"));

            var resolver = new DictionaryResolver();
            resolver.Add(child);
            var expansion = TreeCompiler.ExpandReferences(parent, resolver);
            // 骞堕泦锛氫袱涓?key 閮藉湪
            Assert.Contains(expansion.Definition.Blackboard.Keys, k => k.Name == "target.dist");
            Assert.Contains(expansion.Definition.Blackboard.Keys, k => k.Name == "self.hp");

            // 鍚屽悕涓嶅悓绫诲瀷 鈫?鍐茬獊
            var child2 = new TreeDefinition { TreeId = "child2" };
            child2.Blackboard.Keys.Add(new BlackboardKeyDefinition { Name = "target.dist", Type = TreeValueType.Bool });
            child2.Nodes.Add(new NodeDefinition { Id = "c2", Type = BuiltInNodeTypes.Succeed });
            child2.RootNodeId = "c2";
            var resolver2 = new DictionaryResolver();
            resolver2.Add(child2);
            Assert.Throws<System.InvalidOperationException>(
                () => TreeCompiler.ExpandReferences(parent, resolver2));
        }

        [Fact]
        public void Expansion_RecordsSubtreeInstances_ForCrossTreeNavigation()
        {
            var skill = LeafTree("skill", BuiltInNodeTypes.Succeed, "w");
            var parent = new TreeBuilder()
                .Node("root", BuiltInNodeTypes.Sequence, "sub")
                .Node("sub", BuiltInNodeTypes.Subtree)
                .Root("root");
            parent.Nodes[1].Properties.Set(SubtreeNode.TreeIdProperty, PropertyValue.Of("skill"));

            var resolver = new DictionaryResolver();
            resolver.Add(skill);
            var expansion = TreeCompiler.ExpandReferences(parent, resolver);

            Assert.Single(expansion.SubtreeInstances);
            Assert.Equal("sub.w", expansion.SubtreeInstances[0].InlinedRootNodeId);
            Assert.Equal("skill", expansion.SubtreeInstances[0].ReferencedTreeId);

            // 杩愯鏃剁粡璋冭瘯瑙嗗浘鏆撮湶缁欒瀵熺
            var registry = CreateRegistry();
            var runtime = TreeRuntime.Create(parent, registry, null, null, resolver);
            runtime.Enable();
            var view = (TreeDebugView)runtime;
            Assert.Single(view.SubtreeInstances);
            Assert.Equal("skill", view.SubtreeInstances[0].ReferencedTreeId);
            Assert.Equal("skill", view.NodeSourceTree!["sub.w"]);
            Assert.Equal("w", view.NodeSourceNode!["sub.w"]);
        }

        [Fact]
        public void Expand_IsDeterministic_AndSnapshotCompatible()
        {
            var skill = LeafTree("skill", BuiltInNodeTypes.Wait, "w");
            var parent = new TreeBuilder()
                .Node("root", BuiltInNodeTypes.Sequence, "sub")
                .Node("sub", BuiltInNodeTypes.Subtree)
                .Root("root");
            parent.Nodes[1].Properties.Set(SubtreeNode.TreeIdProperty, PropertyValue.Of("skill"));

            var resolver = new DictionaryResolver();
            resolver.Add(skill);
            var a = TreeCompiler.ExpandReferences(parent, resolver).Definition;
            var b = TreeCompiler.ExpandReferences(parent, resolver).Definition;
            Assert.Equal(a.ComputeDefinitionHash(), b.ComputeDefinitionHash());
            var runtime = TreeRuntime.Create(parent, CreateRegistry(), null, new TreeRunOptions { Seed = 7 }, resolver);
            runtime.Enable();
            runtime.Update(1, Fixed64.Zero);
            var snapshot = runtime.CaptureState();
            var restored = TreeRuntime.Create(parent, CreateRegistry(), null, new TreeRunOptions { Seed = 7 }, resolver);
            restored.Enable();
            restored.RestoreState(snapshot);
            Assert.Equal(runtime.RootNodeState, restored.RootNodeState);
        }
    }
}
