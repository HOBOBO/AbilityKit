using System.Collections.Generic;
using AbilityKit.BehaviorTree.Authoring;
using ApiTreeJson = AbilityKit.BehaviorTree.Serialization.TreeJson;
using AbilityKit.Deterministic;
using Newtonsoft.Json;
using Xunit;
using static AbilityKit.BehaviorTree.Tests.TestNodeTypes;

namespace AbilityKit.BehaviorTree.Tests
{
    /// <summary>蹇収寰€杩斻€佸畾涔夊搱甯屾嫆缁濄€丣SON roundtrip 涓?golden 绋冲畾鎬с€?/summary>
    public sealed class BtSnapshotAndJsonTests
    {
        private static TreeDefinition RunningTree()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", TreeValueType.Int64)
                .Blackboard("test.startCount", TreeValueType.Int64)
                .Node("root", BuiltInNodeTypes.Sequence, "wait", "counter")
                .Node("wait", BuiltInNodeTypes.Wait)
                .Node("counter", CountingAction)
                .Root("root");
            definition.Nodes[1].Properties.Set("durationSeconds", PropertyValue.Of(Fixed64.FromInt32(2)));
            definition.Nodes[2].Properties.Set("startCounterKey", PropertyValue.Of("test.startCount"));
            definition.Nodes[2].Properties.Set("resultKey", PropertyValue.Of("test.result"));
            return definition;
        }

        [Fact]
        public void SnapshotRoundtrip_ContinuesIdentically()
        {
            var options = new TreeRunOptions { Seed = 42 };
            var original = TreeRuntime.Create(RunningTree(), CreateRegistry(), null, options);
            original.Enable(0, Fixed64.Zero);
            original.Blackboard.SetInt64("test.result", 2);
            original.Update(1, Fixed64.Zero);
            original.Update(2, Fixed64.One);

            var snapshotJson = TreeJson.SaveSnapshot(original.CaptureState());
            var restored = TreeRuntime.Create(RunningTree(), CreateRegistry(), null, options);
            restored.Enable(0, Fixed64.Zero);
            restored.RestoreState(TreeJson.LoadSnapshot(snapshotJson));
            for (var frame = 3; frame <= 6; frame++)
            {
                var time = Fixed64.FromInt32(frame - 2);
                original.Update(frame, time);
                restored.Update(frame, time);
                Assert.Equal(original.RootNodeState, restored.RootNodeState);
                Assert.Equal(original.TreeState, restored.TreeState);
            }
            original.Blackboard.SetInt64("test.result", 1);
            restored.Blackboard.SetInt64("test.result", 1);
            original.Update(7, Fixed64.FromInt32(5));
            restored.Update(7, Fixed64.FromInt32(5));
            Assert.Equal(NodeState.Success, original.RootNodeState);
            Assert.Equal(original.RootNodeState, restored.RootNodeState);
            Assert.Equal(original.Blackboard.GetInt64("test.startCount"), restored.Blackboard.GetInt64("test.startCount"));
        }

        [Fact]
        public void Restore_RejectsModifiedDefinition()
        {
            var original = TreeRuntime.Create(RunningTree(), CreateRegistry());
            original.Enable();
            var snapshot = original.CaptureState();

            var modified = RunningTree();
            modified.Nodes[1].Properties.Set("durationSeconds", PropertyValue.Of(Fixed64.FromInt32(5)));

            var other = TreeRuntime.Create(modified, CreateRegistry());
            other.Enable();
            Assert.Throws<System.InvalidOperationException>(() => other.RestoreState(snapshot));
        }

        [Fact]
        public void Restore_RejectsVersionMismatch()
        {
            var original = TreeRuntime.Create(RunningTree(), CreateRegistry());
            original.Enable();
            var snapshot = original.CaptureState();
            snapshot.SnapshotVersion = 99;
            Assert.Throws<System.InvalidOperationException>(() => original.RestoreState(snapshot));
        }

        [Fact]
        public void Capture_RequiresEnable()
        {
            var runtime = TreeRuntime.Create(RunningTree(), CreateRegistry());
            Assert.Throws<System.InvalidOperationException>(() => runtime.CaptureState());
        }

        [Fact]
        public void JsonRoundtrip_PreservesDefinitionAndBehavior()
        {
            var definition = RunningTree();
            var json = TreeJson.Save(definition);
            var loaded = TreeJson.Load(json);

            Assert.Equal(definition.ComputeDefinitionHash(), loaded.ComputeDefinitionHash());
            Assert.Equal(definition.RootNodeId, loaded.RootNodeId);
            Assert.Equal(2, loaded.Blackboard.Keys.Count);

            var runtime = TreeRuntime.Create(loaded, CreateRegistry());
            runtime.Enable();
            runtime.Blackboard.SetInt64("test.result", 1);
            runtime.Update(1, Fixed64.Zero);
            runtime.Update(2, Fixed64.FromInt32(2));
            Assert.Equal(NodeState.Success, runtime.RootNodeState);
            Assert.Equal(1, runtime.Blackboard.GetInt64("test.startCount"));
        }

        [Fact]
        public void JsonSave_IsStableAcrossSaves()
        {
            var first = TreeJson.Save(RunningTree());
            var second = TreeJson.Save(TreeJson.Load(first));
            Assert.Equal(first, second);
        }

        [Fact]
        public void JsonFormat_UsesStableCamelCaseAndNoClrTypes()
        {
            var json = TreeJson.Save(RunningTree());
            Assert.Contains("\"formatVersion\"", json);
            Assert.Contains("\"rootNodeId\"", json);
            Assert.Contains("\"builtin.sequence\"", json);
            Assert.DoesNotContain("$type", json);
            Assert.DoesNotContain("\"comment\"", json);
            Assert.DoesNotContain("AbilityKit.BehaviorTree.Bt", json);
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
            var document = TreeExporter.Import(ApiTreeJson.Load(TreeJson.Save(RunningTree())));
            var exception = Assert.Throws<JsonSerializationException>(
                () => TreeJson.Load(AuthoringJson.Save(document)));
            Assert.Contains("authoring JSON", exception.Message);
        }

        [Fact]
        public void Runtime_OwnsDefinitionSnapshot()
        {
            var source = RunningTree();
            var runtime = TreeRuntime.Create(source, CreateRegistry());
            var originalHash = runtime.Definition.ComputeDefinitionHash();

            source.Nodes[1].Properties.Set("durationSeconds", PropertyValue.Of(Fixed64.FromInt32(99)));
            source.RootNodeId = "counter";
            var exposedCopy = runtime.Definition;
            exposedCopy.RootNodeId = "counter";

            Assert.Equal(originalHash, runtime.Definition.ComputeDefinitionHash());
            Assert.Equal("root", runtime.Definition.RootNodeId);

            runtime.Enable();
            runtime.Blackboard.SetInt64("test.result", 1);
            runtime.Update(1, Fixed64.Zero);
            runtime.Update(2, Fixed64.FromInt32(2));
            Assert.Equal(NodeState.Success, runtime.RootNodeState);
        }

        [Fact]
        public void DefinitionHash_ChangesOnStructuralEdit()
        {
            var a = RunningTree();
            var b = RunningTree();
            b.Nodes[1].Properties.Set("durationSeconds", PropertyValue.Of(Fixed64.FromInt32(9)));
            Assert.NotEqual(a.ComputeDefinitionHash(), b.ComputeDefinitionHash());
        }

        [Fact]
        public void BlackboardSnapshot_RestoresAllTypes()
        {
            var definition = new TreeBuilder()
                .Blackboard("b", TreeValueType.Bool)
                .Blackboard("i", TreeValueType.Int64)
                .Blackboard("f", TreeValueType.Fixed64)
                .Blackboard("s", TreeValueType.String)
                .Node("root", BuiltInNodeTypes.Succeed)
                .Root("root");

            var runtime = TreeRuntime.Create(definition, CreateRegistry());
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
