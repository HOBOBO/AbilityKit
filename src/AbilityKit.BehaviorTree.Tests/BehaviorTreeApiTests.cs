using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.Deterministic;
using Xunit;
using ApiBlackboard = AbilityKit.BehaviorTree.Blackboard.Blackboard;
using ApiBlackboardKeyDefinition = AbilityKit.BehaviorTree.Definition.BlackboardKeyDefinition;
using ApiBlackboardSchema = AbilityKit.BehaviorTree.Definition.BlackboardSchema;
using ApiBuiltInNodes = AbilityKit.BehaviorTree.Nodes.BuiltInNodes;
using ApiBuiltInNodeTypes = AbilityKit.BehaviorTree.Nodes.BuiltInNodeTypes;
using ApiNodeDefinition = AbilityKit.BehaviorTree.Definition.NodeDefinition;
using ApiNodeKind = AbilityKit.BehaviorTree.Definition.NodeKind;
using ApiNodeBase = AbilityKit.BehaviorTree.Nodes.NodeBase;
using ApiNodeRegistry = AbilityKit.BehaviorTree.Registry.NodeRegistry;
using ApiNodeState = AbilityKit.BehaviorTree.Definition.NodeState;
using ApiPropertyValue = AbilityKit.BehaviorTree.Definition.PropertyValue;
using ApiDebugRegistry = AbilityKit.BehaviorTree.Diagnostics.DebugRegistry;
using ApiTreeDebugDeltaView = AbilityKit.BehaviorTree.Diagnostics.TreeDebugDeltaView;
using ApiTreeDefinition = AbilityKit.BehaviorTree.Definition.TreeDefinition;
using ApiTreeJson = AbilityKit.BehaviorTree.Serialization.TreeJson;
using ApiExecutionContext = AbilityKit.BehaviorTree.Execution.ExecutionContext;
using ApiNodeInitContext = AbilityKit.BehaviorTree.Execution.NodeInitContext;
using ApiRuntimeSnapshotMigrationRegistry = AbilityKit.BehaviorTree.Execution.RuntimeSnapshotMigrationRegistry;
using ApiServiceResolver = AbilityKit.BehaviorTree.Execution.ServiceResolver;
using ApiTreeCompiler = AbilityKit.BehaviorTree.Execution.TreeCompiler;
using ApiTreeRuntimeSnapshot = AbilityKit.BehaviorTree.Execution.TreeRuntimeSnapshot;
using ApiTreeTopology = AbilityKit.BehaviorTree.Execution.TreeTopology;
using ApiTreeRuntime = AbilityKit.BehaviorTree.Execution.TreeRuntime;
using ApiTreeRunOptions = AbilityKit.BehaviorTree.Execution.TreeRunOptions;
using ApiValueType = AbilityKit.BehaviorTree.Definition.ValueType;

namespace AbilityKit.BehaviorTree.Tests
{
    public sealed class BehaviorTreeApiTests
    {
        [Fact]
        public void NewEnums_KeepStableNumericValues()
        {
            Assert.Equal(0, (int)ApiNodeState.Inactive);
            Assert.Equal(1, (int)ApiNodeState.Running);
            Assert.Equal(2, (int)ApiNodeState.Success);
            Assert.Equal(3, (int)ApiNodeState.Failure);

            Assert.Equal(0, (int)ApiNodeKind.Composite);
            Assert.Equal(1, (int)ApiNodeKind.Decorator);
            Assert.Equal(2, (int)ApiNodeKind.Condition);
            Assert.Equal(3, (int)ApiNodeKind.Action);

            Assert.Equal(0, (int)ApiValueType.Bool);
            Assert.Equal(1, (int)ApiValueType.Int64);
            Assert.Equal(2, (int)ApiValueType.Fixed64);
            Assert.Equal(3, (int)ApiValueType.String);
        }

        [Fact]
        public void NewRegistryAndRuntime_RunBuiltInNode()
        {
            var registry = CreateApiRegistry();
            using var runtime = ApiTreeRuntime.Create(SucceedTree(), registry);

            runtime.Enable();
            runtime.Update(1, Fixed64.Zero);

            Assert.Equal(ApiNodeState.Success, runtime.RootNodeState);
            Assert.Equal(ApiNodeState.Success, runtime.TreeState);
            Assert.True(registry.Contains(ApiBuiltInNodeTypes.Succeed));
            Assert.True(runtime.Topology.TryGetNodeIndex("root", out var rootIndex));
            Assert.Equal(0, rootIndex);
        }

        [Fact]
        public void NewRegistry_UsesCanonicalNodeFactories()
        {
            var registry = CreateApiRegistry();
            var descriptor = Assert.Single(registry.Descriptors, d => d.TypeId == ApiBuiltInNodeTypes.Wait);

            Assert.Equal(typeof(Func<ApiNodeBase>), typeof(AbilityKit.BehaviorTree.Registry.NodeDescriptor)
                .GetProperty(nameof(AbilityKit.BehaviorTree.Registry.NodeDescriptor.Factory))!.PropertyType);
            Assert.Equal(typeof(ApiNodeBase), typeof(ApiNodeRegistry)
                .GetMethod(nameof(ApiNodeRegistry.CreateNode), new[] { typeof(string) })!.ReturnType);
            Assert.IsType<AbilityKit.BehaviorTree.Nodes.WaitNode>(descriptor.Factory());
            Assert.IsType<AbilityKit.BehaviorTree.Nodes.WaitNode>(registry.CreateNode(ApiBuiltInNodeTypes.Wait));
            Assert.IsAssignableFrom<ApiNodeBase>(registry.CreateNode(ApiBuiltInNodeTypes.Wait));
        }

        [Fact]
        public void NewSerialization_UsesCanonicalJsonGoldenAndStableTypeIds()
        {
            var definition = SucceedTree();
            var json = ApiTreeJson.Save(definition);

            Assert.Equal(CanonicalSucceedJson, NormalizeLineEndings(json));
            Assert.Contains("\"formatVersion\"", json);
            Assert.Contains("\"rootNodeId\"", json);
            Assert.Contains("\"builtin.succeed\"", json);
            Assert.DoesNotContain("$type", json);
            Assert.DoesNotContain("AbilityKit.BehaviorTree.Bt", json);

            var loaded = ApiTreeJson.Load(json);
            Assert.Equal(definition.ComputeDefinitionHash(), loaded.ComputeDefinitionHash());
            Assert.Equal("root", loaded.RootNodeId);
        }

        [Fact]
        public void NewSnapshotSerialization_RestoresRuntimeState()
        {
            var registry = CreateApiRegistry();
            var definition = WaitTree();
            using var original = ApiTreeRuntime.Create(definition, registry, options: new ApiTreeRunOptions { Seed = 7 });
            original.Enable(0, Fixed64.Zero);
            original.Update(1, Fixed64.Zero);
            Assert.Equal(ApiNodeState.Running, original.RootNodeState);

            var snapshotJson = ApiTreeJson.SaveSnapshot(original.CaptureState());
            Assert.Contains("\"snapshotVersion\"", snapshotJson);
            Assert.DoesNotContain("$type", snapshotJson);
            Assert.DoesNotContain("AbilityKit.BehaviorTree", snapshotJson);

            using var restored = ApiTreeRuntime.Create(definition, registry, options: new ApiTreeRunOptions { Seed = 7 });
            restored.Enable(0, Fixed64.Zero);
            restored.RestoreState(ApiTreeJson.LoadSnapshot(snapshotJson));
            Assert.Equal(original.RootNodeState, restored.RootNodeState);

            restored.Update(2, Fixed64.FromInt32(2));
            Assert.Equal(ApiNodeState.Success, restored.RootNodeState);
        }

        [Fact]
        public void NewBlackboardApi_RoundTripsTypedSnapshot()
        {
            var schema = new ApiBlackboardSchema();
            schema.Keys.Add(new ApiBlackboardKeyDefinition { Name = "flag", Type = ApiValueType.Bool });
            schema.Keys.Add(new ApiBlackboardKeyDefinition { Name = "count", Type = ApiValueType.Int64 });
            schema.Keys.Add(new ApiBlackboardKeyDefinition { Name = "ratio", Type = ApiValueType.Fixed64 });
            schema.Keys.Add(new ApiBlackboardKeyDefinition { Name = "label", Type = ApiValueType.String });
            var blackboard = ApiBlackboard.Create(schema);

            blackboard.SetBool("flag", true);
            blackboard.SetInt64("count", 12);
            blackboard.SetFixed64("ratio", Fixed64.FromRatio(1, 4));
            blackboard.SetString("label", "ready");
            var snapshot = blackboard.CaptureValues();

            blackboard.SetBool("flag", false);
            blackboard.SetInt64("count", 0);
            blackboard.SetFixed64("ratio", Fixed64.Zero);
            blackboard.SetString("label", "");
            blackboard.RestoreValues(snapshot);

            Assert.True(blackboard.GetBool("flag"));
            Assert.Equal(12, blackboard.GetInt64("count"));
            Assert.Equal(Fixed64.FromRatio(1, 4), blackboard.GetFixed64("ratio"));
            Assert.Equal("ready", blackboard.GetString("label"));
        }

        [Fact]
        public void NewBlackboardApi_PreservesSchemaOrderAndRejectsMismatchedSnapshots()
        {
            var schema = new ApiBlackboardSchema();
            schema.Keys.Add(new ApiBlackboardKeyDefinition { Name = "second", Type = ApiValueType.Int64 });
            schema.Keys.Add(new ApiBlackboardKeyDefinition { Name = "first", Type = ApiValueType.Bool });
            var blackboard = ApiBlackboard.Create(schema);

            var snapshot = blackboard.CaptureValues();
            Assert.Equal(new[] { "second", "first" }, snapshot.KeyNames);
            Assert.Equal(new[] { ApiValueType.Int64, ApiValueType.Bool }, snapshot.KeyTypes);

            snapshot.KeyNames[0] = "first";
            var keyMismatch = Assert.Throws<InvalidOperationException>(() => blackboard.RestoreValues(snapshot));
            Assert.Equal("BT blackboard snapshot keys do not match the schema.", keyMismatch.Message);

            var countMismatch = blackboard.CaptureValues();
            countMismatch.BoolValues.RemoveAt(0);
            var arrayMismatch = Assert.Throws<InvalidOperationException>(() => blackboard.RestoreValues(countMismatch));
            Assert.Equal("BT blackboard snapshot value array count does not match the schema.", arrayMismatch.Message);
        }

        [Fact]
        public void NewBlackboardApi_UsesCanonicalSchemaCopyAndTypeChecks()
        {
            var schema = new ApiBlackboardSchema();
            schema.Keys.Add(new ApiBlackboardKeyDefinition
            {
                Name = "count",
                Type = ApiValueType.Int64,
                Default = ApiPropertyValue.Of(5L),
            });
            var blackboard = ApiBlackboard.Create(schema);

            schema.Keys[0].Name = "renamed";
            schema.Keys.Add(new ApiBlackboardKeyDefinition { Name = "added", Type = ApiValueType.Bool });
            var exposedSchema = blackboard.Schema;
            exposedSchema.Keys[0].Name = "alsoRenamed";

            Assert.Equal(5L, blackboard.GetInt64("count"));
            Assert.False(blackboard.TryGetBool("count", out _));
            var typeMismatch = Assert.Throws<InvalidOperationException>(() => blackboard.GetBool("count"));
            Assert.Equal("BT blackboard key 'count' is declared as Int64, accessed as Bool.", typeMismatch.Message);

            var missingKey = Assert.Throws<KeyNotFoundException>(() => blackboard.GetBool("added"));
            Assert.Equal("BT blackboard key 'added' is not declared in the tree schema.", missingKey.Message);
        }

        [Fact]
        public void NewExecutionBlackboardApi_ExposesSharedRuntimeBlackboard()
        {
            var definition = new ApiTreeDefinition
            {
                TreeId = "api.blackboard.bridge",
                RootNodeId = "root",
            };
            definition.Blackboard.Keys.Add(new ApiBlackboardKeyDefinition
            {
                Name = "count",
                Type = ApiValueType.Int64,
            });
            definition.Nodes.Add(new ApiNodeDefinition { Id = "root", Type = ApiBuiltInNodeTypes.Succeed });
            using var runtime = ApiTreeRuntime.Create(definition, CreateApiRegistry());

            var heldFacade = runtime.Blackboard;
            heldFacade.SetInt64("count", 1L);
            Assert.Equal(1L, runtime.Blackboard.GetInt64("count"));

            runtime.Blackboard.SetInt64("count", 2L);
            Assert.Equal(2L, heldFacade.GetInt64("count"));
        }

        [Fact]
        public void NewExecutionApi_DoesNotStoreLegacyExecutionImplementations()
        {
            var canonicalTypes = new[]
            {
                typeof(ApiTreeRuntime),
                typeof(ApiExecutionContext),
                typeof(ApiNodeInitContext),
                typeof(ApiTreeTopology),
                typeof(ApiTreeCompiler),
                typeof(ApiTreeRunOptions),
                typeof(ApiTreeRuntimeSnapshot),
                typeof(ApiRuntimeSnapshotMigrationRegistry),
                typeof(ApiServiceResolver),
            };

            foreach (var type in canonicalTypes)
            {
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    var fieldTypeName = field.FieldType.FullName ?? field.FieldType.Name;
                    Assert.False(
                        fieldTypeName.StartsWith("AbilityKit.BehaviorTree.Bt", StringComparison.Ordinal),
                        $"{type.FullName} stores legacy execution field {field.Name}: {fieldTypeName}");
                }
            }
        }

        [Fact]
        public void NewRuntime_RegistersCanonicalDebugViewDirectly()
        {
            ApiDebugRegistry.ClearForTests();
            using var runtime = ApiTreeRuntime.Create(
                SucceedTree(),
                CreateApiRegistry(),
                options: new ApiTreeRunOptions { DebugName = "canonical-debug" });

            var entry = Assert.Single(ApiDebugRegistry.GetEntries());

            Assert.Same(runtime, entry.View);
            Assert.IsAssignableFrom<ApiTreeDebugDeltaView>(entry.View);
        }

        [Fact]
        public void NewAuthoringApi_ExportsCanonicalRuntimeJsonRoundtrip()
        {
            var registry = CreateApiRegistry();
            var document = AuthoringTemplates.BuildEmpty();
            document.Tree.TreeId = "api.tree";

            var authoringJson = AuthoringJson.Save(document);
            var loadedDocument = AuthoringJson.Load(authoringJson);
            var runtimeJson = TreeExporter.Export(loadedDocument, registry, out var errors);
            var runtimeDefinition = ApiTreeJson.Load(runtimeJson);

            Assert.Empty(errors);
            Assert.Equal(SucceedTree().ComputeDefinitionHash(), runtimeDefinition.ComputeDefinitionHash());
            Assert.Equal(NormalizeLineEndings(runtimeJson), NormalizeLineEndings(ApiTreeJson.Save(runtimeDefinition)));
            Assert.Contains("\"layout\"", authoringJson);
            Assert.DoesNotContain("AbilityKit.BehaviorTree.Bt", authoringJson);
        }

        private static ApiNodeRegistry CreateApiRegistry()
        {
            var registry = new ApiNodeRegistry();
            ApiBuiltInNodes.RegisterAll(registry);
            return registry;
        }

        private static ApiTreeDefinition SucceedTree()
        {
            var definition = new ApiTreeDefinition
            {
                TreeId = "api.tree",
                RootNodeId = "root",
            };
            definition.Nodes.Add(new ApiNodeDefinition { Id = "root", Type = ApiBuiltInNodeTypes.Succeed });
            return definition;
        }

        private static ApiTreeDefinition WaitTree()
        {
            var definition = new ApiTreeDefinition
            {
                TreeId = "api.wait",
                RootNodeId = "wait",
            };
            var wait = new ApiNodeDefinition { Id = "wait", Type = ApiBuiltInNodeTypes.Wait };
            wait.Properties.Set("durationSeconds", ApiPropertyValue.Of(Fixed64.One));
            definition.Nodes.Add(wait);
            return definition;
        }

        private static string NormalizeLineEndings(string value) => value.Replace("\r\n", "\n");

        private const string CanonicalSucceedJson = "{\n" +
            "  \"treeId\": \"api.tree\",\n" +
            "  \"formatVersion\": 1,\n" +
            "  \"rootNodeId\": \"root\",\n" +
            "  \"nodes\": [\n" +
            "    {\n" +
            "      \"id\": \"root\",\n" +
            "      \"type\": \"builtin.succeed\",\n" +
            "      \"properties\": {},\n" +
            "      \"childIds\": []\n" +
            "    }\n" +
            "  ],\n" +
            "  \"blackboard\": {\n" +
            "    \"keys\": []\n" +
            "  }\n" +
            "}";
    }
}
