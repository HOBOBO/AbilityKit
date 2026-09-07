using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AbilityKit.Deterministic;
using Xunit;
using static AbilityKit.BehaviorTree.Tests.TestNodeTypes;

namespace AbilityKit.BehaviorTree.Tests
{
    public sealed class BtRuntimeFoundationsTests
    {
        private sealed class StatefulActionNode : ActionNodeBase, NodeStateful
        {
            public string Payload = "initial";
            public string? ThrowOnPayload;
            public readonly List<string> RestoredPayloads = new();

            public override NodeState OnTick(ExecutionContext context) => NodeState.Running;

            public string CaptureState() => Payload;

            public void RestoreState(string payload)
            {
                RestoredPayloads.Add(payload);
                if (payload == ThrowOnPayload)
                {
                    throw new InvalidOperationException("custom restore failed");
                }
                Payload = payload;
            }
        }

        private sealed class StopReasonNode : ActionNodeBase
        {
            public readonly List<NodeStopReason> Reasons = new();
            public bool ThrowOnStop;

            public override NodeState OnTick(ExecutionContext context) => NodeState.Running;

            public override void OnStop(ExecutionContext context)
            {
                Reasons.Add(context.StopReason);
                if (ThrowOnStop)
                {
                    throw new InvalidOperationException("stop failed");
                }
            }
        }

        private sealed class GeneratedModule : NodeRegistryModule
        {
            public void Register(NodeRegistry registry)
            {
                registry.RegisterOrReplace(new NodeDescriptor(
                    "test.generated",
                    "Generated",
                    "Test",
                    NodeKind.Action,
                    0,
                    0,
                    () => new SucceedNode()));
            }
        }

        private sealed class VersionZeroMigrator : RuntimeSnapshotMigrator
        {
            public int FromVersion => 0;
            public int ToVersion => 1;

            public TreeRuntimeSnapshot Migrate(TreeRuntimeSnapshot snapshot)
            {
                snapshot.SnapshotVersion = 1;
                return snapshot;
            }
        }

        [Fact]
        public void Restore_InvalidStack_FailsBeforeMutatingRuntime()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", TreeValueType.Int64)
                .Node("root", ScriptedAction)
                .Root("root");
            var runtime = TreeRuntime.Create(definition, CreateRegistry());
            runtime.Enable();
            runtime.Blackboard.SetInt64("test.result", 2);
            runtime.Update(1, Fixed64.Zero);
            var before = TreeJson.SaveSnapshot(runtime.CaptureState());

            var invalid = runtime.CaptureState();
            invalid.RunStacks[0].NodeIndexes.Add(99);

            Assert.Throws<InvalidOperationException>(() => runtime.RestoreState(invalid));
            Assert.Equal(before, TreeJson.SaveSnapshot(runtime.CaptureState()));
        }

        [Fact]
        public void Restore_EmptyCustomState_IsPassedToStatefulNode()
        {
            var node = new StatefulActionNode { Payload = "before" };
            var registry = CreateRegistry();
            registry.Register(new NodeDescriptor(
                "test.stateful.empty",
                "Stateful",
                "Test",
                NodeKind.Action,
                0,
                0,
                () => node));
            var runtime = TreeRuntime.Create(
                new TreeBuilder().Node("root", "test.stateful.empty").Root("root"),
                registry);
            runtime.Enable();

            var snapshot = runtime.CaptureState();
            snapshot.Nodes.Single().CustomState = "";
            runtime.RestoreState(snapshot);

            Assert.Contains("", node.RestoredPayloads);
            Assert.Equal("", node.Payload);
        }

        [Fact]
        public void Restore_CustomStateFailure_RollsBackPriorState()
        {
            var node = new StatefulActionNode { Payload = "good", ThrowOnPayload = "bad" };
            var registry = CreateRegistry();
            registry.Register(new NodeDescriptor(
                "test.stateful.throw",
                "Stateful",
                "Test",
                NodeKind.Action,
                0,
                0,
                () => node));
            var runtime = TreeRuntime.Create(
                new TreeBuilder().Node("root", "test.stateful.throw").Root("root"),
                registry);
            runtime.Enable();
            var before = TreeJson.SaveSnapshot(runtime.CaptureState());
            var bad = runtime.CaptureState();
            bad.Nodes.Single().State = NodeState.Failure;
            bad.Nodes.Single().CustomState = "bad";

            Assert.Throws<InvalidOperationException>(() => runtime.RestoreState(bad));

            Assert.Equal("good", node.Payload);
            Assert.Equal(before, TreeJson.SaveSnapshot(runtime.CaptureState()));
        }

        [Fact]
        public void Lifecycle_StopReason_IsVisible_AndStopExceptionPolicyCanCapture()
        {
            var node = new StopReasonNode { ThrowOnStop = true };
            var registry = CreateRegistry();
            registry.Register(new NodeDescriptor(
                "test.stop.reason",
                "Stop",
                "Test",
                NodeKind.Action,
                0,
                0,
                () => node));
            var runtime = TreeRuntime.Create(
                new TreeBuilder().Node("root", "test.stop.reason").Root("root"),
                registry,
                options: new TreeRunOptions
                {
                    LifecycleExceptionPolicy = LifecycleExceptionPolicy.CaptureAndContinue,
                });

            runtime.Enable();
            runtime.Disable();

            Assert.False(runtime.IsEnabled);
            Assert.Equal(NodeStopReason.Disabled, node.Reasons.Single());
            Assert.Equal("root", runtime.LastLifecycleException!.NodeId);
            Assert.Equal("OnStop", runtime.LastLifecycleException.Callback);
        }

        [Fact]
        public void ValidatorDiagnostics_PreserveMessageCompatibility_AndExposeStructure()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", TreeValueType.Int64)
                .Node("root", ScriptedAction)
                .Root("root");
            definition.Nodes[0].Properties.Set("nope", PropertyValue.Of(1L));

            var messages = TreeValidator.Validate(definition, CreateRegistry());
            var diagnostics = TreeValidator.ValidateDiagnostics(definition, CreateRegistry());

            Assert.Contains(messages, message => message.Contains("unknown property"));
            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("BT0503", diagnostic.Code);
            Assert.Equal("root", diagnostic.NodeId);
            Assert.Equal("nope", diagnostic.PropertyName);
            Assert.Equal(messages.Single(), diagnostic.Message);
        }

        [Fact]
        public void Topology_ReusesCompiledNodeIdMap()
        {
            var definition = new TreeBuilder()
                .Node("root", BuiltInNodeTypes.Sequence, "a", "b")
                .Node("a", BuiltInNodeTypes.Succeed)
                .Node("b", BuiltInNodeTypes.Fail)
                .Root("root");
            var runtime = TreeRuntime.Create(definition, CreateRegistry());

            Assert.Equal(3, runtime.Topology.NodeCount);
            Assert.True(runtime.TryGetNodeIndex("b", out var index));
            Assert.Equal(2, index);
            Assert.True(runtime.Topology.TryGetNodeDefinition("b", out var node));
            Assert.Equal(BuiltInNodeTypes.Fail, node.Type);

            runtime.Enable();
            runtime.Disable();
            runtime.Enable();
            Assert.True(runtime.TryGetNodeIndex("b", out var secondIndex));
            Assert.Equal(index, secondIndex);
        }

        [Fact]
        public void DebugDelta_ReturnsFullThenOnlyChangedNodes()
        {
            var definition = new TreeBuilder()
                .Node("root", BuiltInNodeTypes.Wait)
                .Root("root");
            definition.Nodes[0].Properties.Set("durationSeconds", PropertyValue.Of(Fixed64.One));
            var runtime = TreeRuntime.Create(definition, CreateRegistry());
            runtime.Enable(0, Fixed64.Zero);
            var debug = (TreeDebugDeltaView)runtime;

            var first = debug.CaptureDebugDelta(0, includeBlackboard: false);
            Assert.True(first.IsFull);
            Assert.Single(first.Nodes);

            var empty = debug.CaptureDebugDelta(first.Sequence, includeBlackboard: false);
            Assert.False(empty.IsFull);
            Assert.Empty(empty.Nodes);

            runtime.Update(2, Fixed64.One);
            var changed = debug.CaptureDebugDelta(first.Sequence, includeBlackboard: false);
            Assert.False(changed.IsFull);
            Assert.Single(changed.Nodes);
            Assert.Equal(NodeState.Success, changed.Nodes[0].State);
        }

        [Fact]
        public void SnapshotMigrationRegistry_CanUpgradeBeforeRestore()
        {
            RuntimeSnapshotMigrationRegistry.Global.Register(new VersionZeroMigrator());
            var runtime = TreeRuntime.Create(
                new TreeBuilder().Node("root", BuiltInNodeTypes.Succeed).Root("root"),
                CreateRegistry());
            runtime.Enable();
            var snapshot = runtime.CaptureState();
            snapshot.SnapshotVersion = 0;

            runtime.RestoreState(snapshot);

            Assert.Equal(NodeState.Running, runtime.RootNodeState);
        }

        [Fact]
        public void GeneratedNodeRegistry_AppliesAotModules()
        {
            var registry = new NodeRegistry();

            GeneratedNodeRegistry.RegisterAll(registry, new[] { new GeneratedModule() });

            Assert.True(registry.Contains("test.generated"));
        }

        [Fact]
        public void RuntimeTopologyLookup_PerformanceBaseline()
        {
            var builder = new TreeBuilder();
            builder.Node("root", BuiltInNodeTypes.Sequence);
            var root = builder.LastNode;
            for (var i = 0; i < 512; i++)
            {
                var id = "n" + i;
                root.ChildIds.Add(id);
                builder.Node(id, BuiltInNodeTypes.Succeed);
            }
            var runtime = TreeRuntime.Create(builder.Root("root"), CreateRegistry());

            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < 100_000; i++)
            {
                Assert.True(runtime.TryGetNodeIndex("n511", out var index));
                Assert.Equal(512, index);
            }
            stopwatch.Stop();

            Assert.True(stopwatch.ElapsedMilliseconds < 1_000, $"Lookup baseline exceeded: {stopwatch.ElapsedMilliseconds} ms.");
        }
    }
}
