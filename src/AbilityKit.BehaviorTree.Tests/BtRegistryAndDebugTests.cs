using System.Linq;
using AbilityKit.Deterministic;
using Xunit;
using static AbilityKit.BehaviorTree.Tests.TestNodeTypes;

namespace AbilityKit.BehaviorTree.Tests
{
    /// <summary>娉ㄥ唽涓績锛氱櫥璁般€佸啿绐併€佹壂鎻忎笌璺ㄧ▼搴忛泦鎵╁睍銆?/summary>
    public sealed class BtNodeRegistryTests
    {
        [Fact]
        public void BuiltInNodes_RegisterAllDescriptors()
        {
            var registry = new NodeRegistry();
            BuiltInNodes.RegisterAll(registry);

            Assert.True(registry.Contains(BuiltInNodeTypes.Sequence));
            Assert.True(registry.Contains(BuiltInNodeTypes.Selector));
            Assert.True(registry.Contains(BuiltInNodeTypes.Parallel));
            Assert.True(registry.Contains(BuiltInNodeTypes.Timeout));
            Assert.True(registry.Contains(BuiltInNodeTypes.Cooldown));
            Assert.True(registry.Contains(BuiltInNodeTypes.BlackboardCompare));
            Assert.True(registry.Contains(BuiltInNodeTypes.Wait));
            Assert.True(registry.Contains(BuiltInNodeTypes.SetBlackboard));
            Assert.True(registry.Contains(BuiltInNodeTypes.Subtree));
            Assert.Equal(24, registry.Descriptors.Count());
        }

        [Fact]
        public void RegisterAll_IsIdempotent()
        {
            var registry = new NodeRegistry();
            BuiltInNodes.RegisterAll(registry);
            BuiltInNodes.RegisterAll(registry);
            Assert.True(registry.Contains(BuiltInNodeTypes.Sequence));
        }

        [Fact]
        public void Register_DuplicateThrows()
        {
            var registry = new NodeRegistry();
            var descriptor = new NodeDescriptor(
                "test.dup", "閲嶅", "娴嬭瘯", NodeKind.Action, 0, 0, () => new SucceedNode());
            registry.Register(descriptor);
            Assert.Throws<System.InvalidOperationException>(() => registry.Register(descriptor));
        }

        [Fact]
        public void Descriptors_CarryPropertySchemaForEditor()
        {
            var registry = new NodeRegistry();
            BuiltInNodes.RegisterAll(registry);

            var descriptor = registry.Descriptors.Single(d => d.TypeId == BuiltInNodeTypes.Timeout);
            Assert.Equal(NodeKind.Decorator, descriptor.Kind);
            Assert.Equal(1, descriptor.MinChildren);
            Assert.Equal(1, descriptor.MaxChildren);
            Assert.Contains(descriptor.PropertySchema, f => f.Name == TimeoutNode.DurationSecondsProperty);
        }

        [Fact]
        public void CreateNode_UnknownTypeThrows()
        {
            var registry = new NodeRegistry();
            Assert.Throws<System.InvalidOperationException>(() => registry.CreateNode("nope"));
        }

        [NodeType("test.scan.action", "Scanned Action", "Scan Test", NodeKind.Action)]
        public sealed class ScannedActionNode : ActionNodeBase
        {
            public override NodeState OnTick(ExecutionContext context) => NodeState.Success;
        }

        [NodeType("test.scan.condition", "鎵弿鏉′欢", "鎵弿娴嬭瘯", NodeKind.Condition)]
        public sealed class ScannedConditionNode : ConditionNodeBase
        {
            protected override bool Validate(ExecutionContext context) => true;
        }

        [Fact]
        public void ScanAssembly_RegistersAttributedNodes_CrossAssembly()
        {
            var registry = new NodeRegistry();
            var count = registry.ScanAssembly(typeof(BtNodeRegistryTests).Assembly);

            Assert.True(count >= 2);
            Assert.True(registry.Contains("test.scan.action"));
            Assert.True(registry.Contains("test.scan.condition"));
            Assert.IsType<ScannedActionNode>(registry.CreateNode("test.scan.action"));
        }

        [Fact]
        public void ScannedNode_RunsInsideTree()
        {
            var registry = new NodeRegistry();
            registry.ScanAssembly(typeof(BtNodeRegistryTests).Assembly);

            var definition = new TreeBuilder()
                .Node("root", "test.scan.action")
                .Root("root");

            var runtime = TreeRuntime.Create(definition, registry);
            runtime.Enable();
            runtime.Update(1, Fixed64.Zero);
            Assert.Equal(NodeState.Success, runtime.RootNodeState);
        }
    }

    /// <summary>璋冭瘯娉ㄥ唽涓績锛氱櫥璁?娉ㄩ攢/缂栬緫鍣ㄦ媺鍙栥€?/summary>
    public sealed class BtDebugRegistryTests
    {
        private sealed class LifecycleNode : ActionNodeBase
        {
            public static int Starts;
            public static int Stops;
            public static bool ThrowOnStart;

            public override void OnStart(ExecutionContext context)
            {
                Starts++;
                if (ThrowOnStart) throw new System.InvalidOperationException("start failed");
            }
            public override NodeState OnTick(ExecutionContext context) => NodeState.Running;
            public override void OnStop(ExecutionContext context) => Stops++;
        }

        [Fact]
        public void DebugName_RegistersIntoRegistry_AndDisposeUnregisters()
        {
            DebugRegistry.ClearForTests();
            var definition = new TreeBuilder()
                .Blackboard("test.result", TreeValueType.Int64)
                .Node("root", BuiltInNodeTypes.Wait)
                .Root("root");

            using (var runtime = TreeRuntime.Create(definition, TestNodeTypes.CreateRegistry(), null,
                new TreeRunOptions { DebugName = "observer", DebugOwnerLabel = "hero-1" }))
            {
                runtime.Enable();
                Assert.Equal(1, DebugRegistry.Count);
                runtime.Update(1, Fixed64.Zero);

                var view = DebugRegistry.GetViews().Single(v => v.DisplayName == "observer");
                Assert.Equal("test.tree", view.TreeId);
                Assert.Equal("hero-1", view.OwnerLabel);
                Assert.Equal(1, view.NodeCount);

                var nodes = view.GetNodeStates();
                Assert.Single(nodes);
                Assert.Equal(NodeState.Running, nodes[0].State);
                Assert.Equal(BuiltInNodeTypes.Wait, nodes[0].TypeId);
            }

            Assert.Equal(0, DebugRegistry.Count);
        }

        [Fact]
        public void DebugName_RegistersIntoCanonicalRegistry_AndPreservesDeltaCapability()
        {
            AbilityKit.BehaviorTree.Diagnostics.DebugRegistry.ClearForTests();
            var definition = new TreeBuilder()
                .Node("root", BuiltInNodeTypes.Wait)
                .Root("root");

            using (var runtime = TreeRuntime.Create(definition, TestNodeTypes.CreateRegistry(), null,
                new TreeRunOptions { DebugName = "canonical" }))
            {
                runtime.Enable();

                var entry = AbilityKit.BehaviorTree.Diagnostics.DebugRegistry.GetEntries().Single();
                Assert.Equal("canonical", entry.View.DisplayName);

                var deltaView = Assert.IsAssignableFrom<AbilityKit.BehaviorTree.Diagnostics.TreeDebugDeltaView>(entry.View);
                var delta = deltaView.CaptureDebugDelta(0, includeBlackboard: false);
                Assert.True(delta.IsFull);
                Assert.Equal(1, delta.Sequence);
                Assert.Single(delta.Nodes);
            }

            Assert.Equal(0, AbilityKit.BehaviorTree.Diagnostics.DebugRegistry.Count);
        }

        [Fact]
        public void NoDebugName_DoesNotRegister()
        {
            DebugRegistry.ClearForTests();
            var definition = new TreeBuilder()
                .Node("root", BuiltInNodeTypes.Succeed)
                .Root("root");

            var runtime = TreeRuntime.Create(definition, TestNodeTypes.CreateRegistry());
            runtime.Enable();
            Assert.Equal(0, DebugRegistry.Count);
            runtime.Update(1, Fixed64.Zero);
        }

        [Fact]
        public void RegistryEntries_AreOrderedByInstanceSequence()
        {
            DebugRegistry.ClearForTests();
            var definition = new TreeBuilder()
                .Node("root", BuiltInNodeTypes.Succeed)
                .Root("root");

            var first = TreeRuntime.Create(definition, TestNodeTypes.CreateRegistry(), null,
                new TreeRunOptions { DebugName = "a" });
            first.Enable();
            var second = TreeRuntime.Create(definition, TestNodeTypes.CreateRegistry(), null,
                new TreeRunOptions { DebugName = "b" });
            second.Enable();

            var entries = DebugRegistry.GetEntries();
            Assert.Equal(2, entries.Count);
            // 娉ㄥ唽搴忓彿涓ユ牸閫掑锛岃瀵熺鎹鎸夊疄渚?ID 鍖哄垎涓庨€夋嫨
            Assert.True(entries[0].Id < entries[1].Id);
            Assert.Equal("a", entries[0].View.DisplayName);
            Assert.Equal("b", entries[1].View.DisplayName);

            first.Dispose();
            var afterDispose = DebugRegistry.GetEntries();
            Assert.Single(afterDispose);
            Assert.Equal("b", afterDispose[0].View.DisplayName);
            second.Dispose();
        }

        [Fact]
        public void DisableAndDispose_StopRunningNodesExactlyOnce()
        {
            LifecycleNode.Starts = 0;
            LifecycleNode.Stops = 0;
            LifecycleNode.ThrowOnStart = false;
            var registry = TestNodeTypes.CreateRegistry();
            registry.Register(new NodeDescriptor(
                "test.lifecycle", "Lifecycle", "Test", NodeKind.Action, 0, 0, () => new LifecycleNode()));
            var definition = new TreeBuilder()
                .Node("root", "test.lifecycle")
                .Root("root");
            var runtime = TreeRuntime.Create(definition, registry);

            runtime.Enable();
            runtime.Disable();
            runtime.Disable();
            Assert.Equal(1, LifecycleNode.Starts);
            Assert.Equal(1, LifecycleNode.Stops);

            runtime.Enable();
            runtime.Dispose();
            runtime.Dispose();
            Assert.Equal(2, LifecycleNode.Starts);
            Assert.Equal(2, LifecycleNode.Stops);
            Assert.Throws<System.ObjectDisposedException>(() => runtime.Enable());
        }

        [Fact]
        public void EnableFailure_CleansUpPartiallyStartedPath()
        {
            LifecycleNode.Starts = 0;
            LifecycleNode.Stops = 0;
            LifecycleNode.ThrowOnStart = true;
            var registry = TestNodeTypes.CreateRegistry();
            registry.Register(new NodeDescriptor(
                "test.lifecycle.failure", "Lifecycle", "Test", NodeKind.Action, 0, 0, () => new LifecycleNode()));
            var definition = new TreeBuilder()
                .Node("root", "test.lifecycle.failure")
                .Root("root");
            using var runtime = TreeRuntime.Create(definition, registry);

            Assert.Throws<System.InvalidOperationException>(() => runtime.Enable());
            Assert.False(runtime.IsEnabled);
            Assert.Equal(1, LifecycleNode.Starts);
            Assert.Equal(1, LifecycleNode.Stops);
            LifecycleNode.ThrowOnStart = false;
        }

        [Fact]
        public void DebugView_ExposesDefinitionAndFrameForObservation()
        {
            DebugRegistry.ClearForTests();
            var definition = new TreeBuilder()
                .Node("root", BuiltInNodeTypes.Wait)
                .Root("root");

            var runtime = TreeRuntime.Create(definition, TestNodeTypes.CreateRegistry(), null,
                new TreeRunOptions { DebugName = "obs" });
            runtime.Enable();

            var view = DebugRegistry.GetViews().Single(v => v.DisplayName == "obs");
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
            DebugRegistry.ClearForTests();
            var definition = new TreeBuilder()
                .Blackboard("test.result", TreeValueType.Int64)
                .Node("root", BuiltInNodeTypes.Sequence, "a", "b")
                .Node("a", ScriptedAction)
                .Node("b", CountingAction)
                .Root("root");
            definition.Nodes[2].Properties.Set("resultKey", PropertyValue.Of("test.result"));

            var runtime = TreeRuntime.Create(definition, TestNodeTypes.CreateRegistry(), null,
                new TreeRunOptions { DebugName = "path" });
            runtime.Enable();
            runtime.Blackboard.SetInt64("test.result", 2);   // a 鍗?Running
            runtime.Update(1, Fixed64.Zero);

            var view = DebugRegistry.GetViews().Single();
            var running = view.GetNodeStates().Where(n => n.OnStackCount > 0).Select(n => n.NodeId).ToList();
            Assert.Contains("a", running);
            Assert.Contains("root", running);
            Assert.DoesNotContain("b", running);

            runtime.Dispose();
        }
    }

    /// <summary>榛戞澘锛氱被鍨嬪寲璇诲啓涓庨敊璇矾寰勩€?/summary>
    public sealed class BtBlackboardTests
    {
        [Fact]
        public void TypedAccess_WorksPerSchema()
        {
            var definition = new TreeBuilder()
                .Blackboard("b", TreeValueType.Bool)
                .Blackboard("f", TreeValueType.Fixed64, PropertyValue.Of(Fixed64.FromInt32(3)))
                .Node("root", BuiltInNodeTypes.Succeed)
                .Root("root");

            var runtime = TreeRuntime.Create(definition, TestNodeTypes.CreateRegistry());
            runtime.Enable();

            Assert.Equal(Fixed64.FromInt32(3), runtime.Blackboard.GetFixed64("f"));
            runtime.Blackboard.SetBool("b", true);
            Assert.True(runtime.Blackboard.GetBool("b"));
        }

        [Fact]
        public void UndeclaredKey_Throws()
        {
            var definition = new TreeBuilder()
                .Blackboard("b", TreeValueType.Bool)
                .Node("root", BuiltInNodeTypes.Succeed)
                .Root("root");

            var runtime = TreeRuntime.Create(definition, TestNodeTypes.CreateRegistry());
            runtime.Enable();
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
                () => runtime.Blackboard.GetBool("missing"));
        }

        [Fact]
        public void TypeMismatch_Throws()
        {
            var definition = new TreeBuilder()
                .Blackboard("b", TreeValueType.Bool)
                .Node("root", BuiltInNodeTypes.Succeed)
                .Root("root");

            var runtime = TreeRuntime.Create(definition, TestNodeTypes.CreateRegistry());
            runtime.Enable();
            Assert.Throws<System.InvalidOperationException>(
                () => runtime.Blackboard.GetInt64("b"));
        }

        [Fact]
        public void TryGet_ToleratesMissingOrMismatchedKeys()
        {
            var definition = new TreeBuilder()
                .Blackboard("b", TreeValueType.Bool)
                .Node("root", BuiltInNodeTypes.Succeed)
                .Root("root");

            var runtime = TreeRuntime.Create(definition, TestNodeTypes.CreateRegistry());
            runtime.Enable();

            Assert.False(runtime.Blackboard.TryGetInt64("missing", out _));
            Assert.False(runtime.Blackboard.TryGetInt64("b", out _));   // 绫诲瀷涓嶇 鈫?false
            Assert.True(runtime.Blackboard.TryGetBool("b", out var value));
            Assert.False(value);
        }
    }
}
