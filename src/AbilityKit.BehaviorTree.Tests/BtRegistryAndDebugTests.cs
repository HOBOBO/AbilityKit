using System.Linq;
using AbilityKit.Deterministic;
using Xunit;
using static AbilityKit.BehaviorTree.Tests.TestNodeTypes;

namespace AbilityKit.BehaviorTree.Tests
{
    /// <summary>注册中心：登记、冲突、扫描与跨程序集扩展。</summary>
    public sealed class BtNodeRegistryTests
    {
        [Fact]
        public void BuiltInNodes_RegisterAllDescriptors()
        {
            var registry = new BtNodeRegistry();
            BtBuiltInNodes.RegisterAll(registry);

            Assert.True(registry.Contains(BtBuiltInNodeTypes.Sequence));
            Assert.True(registry.Contains(BtBuiltInNodeTypes.Selector));
            Assert.True(registry.Contains(BtBuiltInNodeTypes.Parallel));
            Assert.True(registry.Contains(BtBuiltInNodeTypes.Timeout));
            Assert.True(registry.Contains(BtBuiltInNodeTypes.Cooldown));
            Assert.True(registry.Contains(BtBuiltInNodeTypes.BlackboardCompare));
            Assert.True(registry.Contains(BtBuiltInNodeTypes.Wait));
            Assert.True(registry.Contains(BtBuiltInNodeTypes.SetBlackboard));
            Assert.True(registry.Contains(BtBuiltInNodeTypes.Subtree));
            Assert.Equal(24, registry.Descriptors.Count());
        }

        [Fact]
        public void RegisterAll_IsIdempotent()
        {
            var registry = new BtNodeRegistry();
            BtBuiltInNodes.RegisterAll(registry);
            BtBuiltInNodes.RegisterAll(registry);   // 覆盖语义，不抛异常
            Assert.True(registry.Contains(BtBuiltInNodeTypes.Sequence));
        }

        [Fact]
        public void Register_DuplicateThrows()
        {
            var registry = new BtNodeRegistry();
            var descriptor = new BtNodeDescriptor(
                "test.dup", "重复", "测试", BtNodeKind.Action, 0, 0, () => new BtSucceedNode());
            registry.Register(descriptor);
            Assert.Throws<System.InvalidOperationException>(() => registry.Register(descriptor));
        }

        [Fact]
        public void Descriptors_CarryPropertySchemaForEditor()
        {
            var registry = new BtNodeRegistry();
            BtBuiltInNodes.RegisterAll(registry);

            var descriptor = registry.Descriptors.Single(d => d.TypeId == BtBuiltInNodeTypes.Timeout);
            Assert.Equal(BtNodeKind.Decorator, descriptor.Kind);
            Assert.Equal(1, descriptor.MinChildren);
            Assert.Equal(1, descriptor.MaxChildren);
            Assert.Contains(descriptor.PropertySchema, f => f.Name == BtTimeoutNode.DurationSecondsProperty);
        }

        [Fact]
        public void CreateNode_UnknownTypeThrows()
        {
            var registry = new BtNodeRegistry();
            Assert.Throws<System.InvalidOperationException>(() => registry.CreateNode("nope"));
        }

        // 跨程序集扩展验证：测试程序集内定义带 attribute 的节点，扫描后可用
        [BtNodeType("test.scan.action", "扫描动作", "扫描测试", BtNodeKind.Action)]
        public sealed class ScannedActionNode : BtActionNodeBase
        {
            public override BtNodeState OnTick(BtExecutionContext context) => BtNodeState.Success;
        }

        [BtNodeType("test.scan.condition", "扫描条件", "扫描测试", BtNodeKind.Condition)]
        public sealed class ScannedConditionNode : BtConditionNodeBase
        {
            protected override bool Validate(BtExecutionContext context) => true;
        }

        [Fact]
        public void ScanAssembly_RegistersAttributedNodes_CrossAssembly()
        {
            var registry = new BtNodeRegistry();
            var count = registry.ScanAssembly(typeof(BtNodeRegistryTests).Assembly);

            Assert.True(count >= 2);
            Assert.True(registry.Contains("test.scan.action"));
            Assert.True(registry.Contains("test.scan.condition"));
            Assert.IsType<ScannedActionNode>(registry.CreateNode("test.scan.action"));
        }

        [Fact]
        public void ScannedNode_RunsInsideTree()
        {
            var registry = new BtNodeRegistry();
            registry.ScanAssembly(typeof(BtNodeRegistryTests).Assembly);

            var definition = new TreeBuilder()
                .Node("root", "test.scan.action")
                .Root("root");

            var runtime = BtTreeRuntime.Create(definition, registry);
            runtime.Enable();
            runtime.Update(1, Fixed64.Zero);
            Assert.Equal(BtNodeState.Success, runtime.RootNodeState);
        }
    }

    /// <summary>调试注册中心：登记/注销/编辑器拉取。</summary>
    public sealed class BtDebugRegistryTests
    {
        [Fact]
        public void DebugName_RegistersIntoRegistry_AndDisposeUnregisters()
        {
            BtDebugRegistry.ClearForTests();
            var definition = new TreeBuilder()
                .Blackboard("test.result", BtValueType.Int64)
                .Node("root", BtBuiltInNodeTypes.Wait)
                .Root("root");

            using (var runtime = BtTreeRuntime.Create(definition, TestNodeTypes.CreateRegistry(), null,
                new BtTreeRunOptions { DebugName = "观察我", DebugOwnerLabel = "hero-1" }))
            {
                runtime.Enable();
                Assert.Equal(1, BtDebugRegistry.Count);
                runtime.Update(1, Fixed64.Zero);

                var view = BtDebugRegistry.GetViews().Single(v => v.DisplayName == "观察我");
                Assert.Equal("test.tree", view.TreeId);
                Assert.Equal("hero-1", view.OwnerLabel);
                Assert.Equal(1, view.NodeCount);

                var nodes = view.GetNodeStates();
                Assert.Single(nodes);
                Assert.Equal(BtNodeState.Running, nodes[0].State);
                Assert.Equal(BtBuiltInNodeTypes.Wait, nodes[0].TypeId);
            }

            Assert.Equal(0, BtDebugRegistry.Count);
        }

        [Fact]
        public void NoDebugName_DoesNotRegister()
        {
            BtDebugRegistry.ClearForTests();
            var definition = new TreeBuilder()
                .Node("root", BtBuiltInNodeTypes.Succeed)
                .Root("root");

            var runtime = BtTreeRuntime.Create(definition, TestNodeTypes.CreateRegistry());
            runtime.Enable();
            Assert.Equal(0, BtDebugRegistry.Count);
            runtime.Update(1, Fixed64.Zero);
        }

        [Fact]
        public void RegistryEntries_AreOrderedByInstanceSequence()
        {
            BtDebugRegistry.ClearForTests();
            var definition = new TreeBuilder()
                .Node("root", BtBuiltInNodeTypes.Succeed)
                .Root("root");

            var first = BtTreeRuntime.Create(definition, TestNodeTypes.CreateRegistry(), null,
                new BtTreeRunOptions { DebugName = "a" });
            first.Enable();
            var second = BtTreeRuntime.Create(definition, TestNodeTypes.CreateRegistry(), null,
                new BtTreeRunOptions { DebugName = "b" });
            second.Enable();

            var entries = BtDebugRegistry.GetEntries();
            Assert.Equal(2, entries.Count);
            // 注册序号严格递增，观察端据此按实例 ID 区分与选择
            Assert.True(entries[0].Id < entries[1].Id);
            Assert.Equal("a", entries[0].View.DisplayName);
            Assert.Equal("b", entries[1].View.DisplayName);

            first.Dispose();
            var afterDispose = BtDebugRegistry.GetEntries();
            Assert.Single(afterDispose);
            Assert.Equal("b", afterDispose[0].View.DisplayName);
        }

        [Fact]
        public void DebugView_ExposesDefinitionAndFrameForObservation()
        {
            BtDebugRegistry.ClearForTests();
            var definition = new TreeBuilder()
                .Node("root", BtBuiltInNodeTypes.Wait)
                .Root("root");

            var runtime = BtTreeRuntime.Create(definition, TestNodeTypes.CreateRegistry(), null,
                new BtTreeRunOptions { DebugName = "obs" });
            runtime.Enable();

            var view = BtDebugRegistry.GetViews().Single(v => v.DisplayName == "obs");
            // 树定义暴露给观察端（渲染图/跳子树），且与实例定义同源
            Assert.NotNull(view.TreeDefinition);
            Assert.Equal("root", view.TreeDefinition.RootNodeId);
            Assert.Single(view.TreeDefinition.Nodes);
            Assert.Equal(0, view.LastFrame);

            runtime.Update(7, Fixed64.Zero);
            runtime.Update(9, Fixed64.One);
            Assert.Equal(9, view.LastFrame);

            runtime.Dispose();
        }

        [Fact]
        public void DebugView_RevealsRunningPath()
        {
            BtDebugRegistry.ClearForTests();
            var definition = new TreeBuilder()
                .Blackboard("test.result", BtValueType.Int64)
                .Node("root", BtBuiltInNodeTypes.Sequence, "a", "b")
                .Node("a", ScriptedAction)
                .Node("b", CountingAction)
                .Root("root");
            definition.Nodes[2].Properties.Set("resultKey", BtPropertyValue.Of("test.result"));

            var runtime = BtTreeRuntime.Create(definition, TestNodeTypes.CreateRegistry(), null,
                new BtTreeRunOptions { DebugName = "path" });
            runtime.Enable();
            runtime.Blackboard.SetInt64("test.result", 2);   // a 卡 Running
            runtime.Update(1, Fixed64.Zero);

            var view = BtDebugRegistry.GetViews().Single();
            var running = view.GetNodeStates().Where(n => n.OnStackCount > 0).Select(n => n.NodeId).ToList();
            Assert.Contains("a", running);
            Assert.Contains("root", running);
            Assert.DoesNotContain("b", running);

            runtime.Dispose();
        }
    }

    /// <summary>黑板：类型化读写与错误路径。</summary>
    public sealed class BtBlackboardTests
    {
        [Fact]
        public void TypedAccess_WorksPerSchema()
        {
            var definition = new TreeBuilder()
                .Blackboard("b", BtValueType.Bool)
                .Blackboard("f", BtValueType.Fixed64, BtPropertyValue.Of(Fixed64.FromInt32(3)))
                .Node("root", BtBuiltInNodeTypes.Succeed)
                .Root("root");

            var runtime = BtTreeRuntime.Create(definition, TestNodeTypes.CreateRegistry());
            runtime.Enable();

            Assert.Equal(Fixed64.FromInt32(3), runtime.Blackboard.GetFixed64("f"));   // 默认值生效
            runtime.Blackboard.SetBool("b", true);
            Assert.True(runtime.Blackboard.GetBool("b"));
        }

        [Fact]
        public void UndeclaredKey_Throws()
        {
            var definition = new TreeBuilder()
                .Blackboard("b", BtValueType.Bool)
                .Node("root", BtBuiltInNodeTypes.Succeed)
                .Root("root");

            var runtime = BtTreeRuntime.Create(definition, TestNodeTypes.CreateRegistry());
            runtime.Enable();
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
                () => runtime.Blackboard.GetBool("missing"));
        }

        [Fact]
        public void TypeMismatch_Throws()
        {
            var definition = new TreeBuilder()
                .Blackboard("b", BtValueType.Bool)
                .Node("root", BtBuiltInNodeTypes.Succeed)
                .Root("root");

            var runtime = BtTreeRuntime.Create(definition, TestNodeTypes.CreateRegistry());
            runtime.Enable();
            Assert.Throws<System.InvalidOperationException>(
                () => runtime.Blackboard.GetInt64("b"));
        }

        [Fact]
        public void TryGet_ToleratesMissingOrMismatchedKeys()
        {
            var definition = new TreeBuilder()
                .Blackboard("b", BtValueType.Bool)
                .Node("root", BtBuiltInNodeTypes.Succeed)
                .Root("root");

            var runtime = BtTreeRuntime.Create(definition, TestNodeTypes.CreateRegistry());
            runtime.Enable();

            Assert.False(runtime.Blackboard.TryGetInt64("missing", out _));
            Assert.False(runtime.Blackboard.TryGetInt64("b", out _));   // 类型不符 → false
            Assert.True(runtime.Blackboard.TryGetBool("b", out var value));
            Assert.False(value);
        }
    }
}
