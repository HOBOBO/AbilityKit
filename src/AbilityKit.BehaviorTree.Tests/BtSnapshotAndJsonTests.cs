using System.Collections.Generic;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Deterministic;
using Newtonsoft.Json;
using Xunit;
using static AbilityKit.BehaviorTree.Tests.TestNodeTypes;

namespace AbilityKit.BehaviorTree.Tests
{
    /// <summary>快照往返、定义哈希拒绝、JSON roundtrip 与 golden 稳定性。</summary>
    public sealed class BtSnapshotAndJsonTests
    {
        private static BtTreeDefinition RunningTree()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", BtValueType.Int64)
                .Blackboard("test.startCount", BtValueType.Int64)
                .Node("root", BtBuiltInNodeTypes.Sequence, "wait", "counter")
                .Node("wait", BtBuiltInNodeTypes.Wait)
                .Node("counter", CountingAction)
                .Root("root");
            definition.Nodes[1].Properties.Set("durationSeconds", BtPropertyValue.Of(Fixed64.FromInt32(2)));
            definition.Nodes[2].Properties.Set("startCounterKey", BtPropertyValue.Of("test.startCount"));
            definition.Nodes[2].Properties.Set("resultKey", BtPropertyValue.Of("test.result"));
            return definition;
        }

        [Fact]
        public void SnapshotRoundtrip_ContinuesIdentically()
        {
            var options = new BtTreeRunOptions { Seed = 42 };
            var original = BtTreeRuntime.Create(RunningTree(), CreateRegistry(), null, options);
            original.Enable(0, Fixed64.Zero);
            original.Blackboard.SetInt64("test.result", 2);
            original.Update(1, Fixed64.Zero);
            original.Update(2, Fixed64.One);

            var snapshotJson = BtTreeJson.SaveSnapshot(original.CaptureState());
            var restored = BtTreeRuntime.Create(RunningTree(), CreateRegistry(), null, options);
            restored.Enable(0, Fixed64.Zero);
            restored.RestoreState(BtTreeJson.LoadSnapshot(snapshotJson));

            // 双实例继续执行相同序列，黑板演化一致
            for (var frame = 3; frame <= 6; frame++)
            {
                var time = Fixed64.FromInt32(frame - 2);
                original.Update(frame, time);
                restored.Update(frame, time);
                Assert.Equal(original.RootNodeState, restored.RootNodeState);
                Assert.Equal(original.TreeState, restored.TreeState);
            }

            // 放开运行中的动作，双实例都应完成，计数一致
            original.Blackboard.SetInt64("test.result", 1);
            restored.Blackboard.SetInt64("test.result", 1);
            original.Update(7, Fixed64.FromInt32(5));
            restored.Update(7, Fixed64.FromInt32(5));
            Assert.Equal(BtNodeState.Success, original.RootNodeState);
            Assert.Equal(original.RootNodeState, restored.RootNodeState);
            Assert.Equal(original.Blackboard.GetInt64("test.startCount"), restored.Blackboard.GetInt64("test.startCount"));
        }

        [Fact]
        public void Restore_RejectsModifiedDefinition()
        {
            var original = BtTreeRuntime.Create(RunningTree(), CreateRegistry());
            original.Enable();
            var snapshot = original.CaptureState();

            var modified = RunningTree();
            modified.Nodes[1].Properties.Set("durationSeconds", BtPropertyValue.Of(Fixed64.FromInt32(5)));

            var other = BtTreeRuntime.Create(modified, CreateRegistry());
            other.Enable();
            Assert.Throws<System.InvalidOperationException>(() => other.RestoreState(snapshot));
        }

        [Fact]
        public void Restore_RejectsVersionMismatch()
        {
            var original = BtTreeRuntime.Create(RunningTree(), CreateRegistry());
            original.Enable();
            var snapshot = original.CaptureState();
            snapshot.SnapshotVersion = 99;
            Assert.Throws<System.InvalidOperationException>(() => original.RestoreState(snapshot));
        }

        [Fact]
        public void Capture_RequiresEnable()
        {
            var runtime = BtTreeRuntime.Create(RunningTree(), CreateRegistry());
            Assert.Throws<System.InvalidOperationException>(() => runtime.CaptureState());
        }

        [Fact]
        public void JsonRoundtrip_PreservesDefinitionAndBehavior()
        {
            var definition = RunningTree();
            var json = BtTreeJson.Save(definition);
            var loaded = BtTreeJson.Load(json);

            Assert.Equal(definition.ComputeDefinitionHash(), loaded.ComputeDefinitionHash());
            Assert.Equal(definition.RootNodeId, loaded.RootNodeId);
            Assert.Equal(2, loaded.Blackboard.Keys.Count);

            var runtime = BtTreeRuntime.Create(loaded, CreateRegistry());
            runtime.Enable();
            runtime.Blackboard.SetInt64("test.result", 1);
            runtime.Update(1, Fixed64.Zero);
            runtime.Update(2, Fixed64.FromInt32(2));
            Assert.Equal(BtNodeState.Success, runtime.RootNodeState);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.startCount"));
        }

        [Fact]
        public void JsonSave_IsStableAcrossSaves()
        {
            var first = BtTreeJson.Save(RunningTree());
            var second = BtTreeJson.Save(BtTreeJson.Load(first));
            Assert.Equal(first, second);
        }

        [Fact]
        public void JsonFormat_UsesStableCamelCaseAndNoClrTypes()
        {
            var json = BtTreeJson.Save(RunningTree());
            Assert.Contains("\"formatVersion\"", json);
            Assert.Contains("\"rootNodeId\"", json);
            Assert.Contains("\"builtin.sequence\"", json);
            Assert.DoesNotContain("$type", json);
            Assert.DoesNotContain("\"comment\"", json);
            Assert.DoesNotContain("AbilityKit.BehaviorTree.Bt", json);   // 无 CLR 类型名
            foreach (var node in (Newtonsoft.Json.Linq.JArray)Newtonsoft.Json.Linq.JObject.Parse(json)["nodes"]!)
            {
                Assert.Null(node["name"]);
                Assert.Null(node["comment"]);
            }
        }

        [Fact]
        public void DefinitionHash_IgnoresTreeIdChanges()
        {
            var a = RunningTree();
            var b = RunningTree();
            b.TreeId = "renamed";
            Assert.Equal(a.ComputeDefinitionHash(), b.ComputeDefinitionHash());
        }

        [Fact]
        public void RuntimeJson_RejectsAuthoringDocument()
        {
            var document = BtTreeExporter.Import(RunningTree());
            var exception = Assert.Throws<JsonSerializationException>(
                () => BtTreeJson.Load(BtAuthoringJson.Save(document)));
            Assert.Contains("authoring JSON", exception.Message);
        }

        [Fact]
        public void Runtime_OwnsDefinitionSnapshot()
        {
            var source = RunningTree();
            var runtime = BtTreeRuntime.Create(source, CreateRegistry());
            var originalHash = runtime.Definition.ComputeDefinitionHash();

            source.Nodes[1].Properties.Set("durationSeconds", BtPropertyValue.Of(Fixed64.FromInt32(99)));
            source.RootNodeId = "counter";
            var exposedCopy = runtime.Definition;
            exposedCopy.RootNodeId = "counter";

            Assert.Equal(originalHash, runtime.Definition.ComputeDefinitionHash());
            Assert.Equal("root", runtime.Definition.RootNodeId);

            runtime.Enable();
            runtime.Blackboard.SetInt64("test.result", 1);
            runtime.Update(1, Fixed64.Zero);
            runtime.Update(2, Fixed64.FromInt32(2));
            Assert.Equal(BtNodeState.Success, runtime.RootNodeState);
        }

        [Fact]
        public void DefinitionHash_ChangesOnStructuralEdit()
        {
            var a = RunningTree();
            var b = RunningTree();
            b.Nodes[1].Properties.Set("durationSeconds", BtPropertyValue.Of(Fixed64.FromInt32(9)));
            Assert.NotEqual(a.ComputeDefinitionHash(), b.ComputeDefinitionHash());
        }

        [Fact]
        public void BlackboardSnapshot_RestoresAllTypes()
        {
            var definition = new TreeBuilder()
                .Blackboard("b", BtValueType.Bool)
                .Blackboard("i", BtValueType.Int64)
                .Blackboard("f", BtValueType.Fixed64)
                .Blackboard("s", BtValueType.String)
                .Node("root", BtBuiltInNodeTypes.Succeed)
                .Root("root");

            var runtime = BtTreeRuntime.Create(definition, CreateRegistry());
            runtime.Enable();
            runtime.Blackboard.SetBool("b", true);
            runtime.Blackboard.SetInt64("i", -7);
            runtime.Blackboard.SetFixed64("f", Fixed64.FromRatio(1, 3));
            runtime.Blackboard.SetString("s", "v");

            var snapshot = runtime.Blackboard.CaptureValues();
            runtime.Blackboard.SetBool("b", false);
            runtime.Blackboard.SetInt64("i", 0);
            runtime.Blackboard.SetFixed64("f", Fixed64.Zero);
            runtime.Blackboard.SetString("s", "");

            runtime.Blackboard.RestoreValues(snapshot);
            Assert.True(runtime.Blackboard.GetBool("b"));
            Assert.Equal(-7, runtime.Blackboard.GetInt64("i"));
            Assert.Equal(Fixed64.FromRatio(1, 3), runtime.Blackboard.GetFixed64("f"));
            Assert.Equal("v", runtime.Blackboard.GetString("s"));
        }
    }
}
