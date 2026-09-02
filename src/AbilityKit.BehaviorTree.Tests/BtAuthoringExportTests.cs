using AbilityKit.BehaviorTree.Authoring;
using Newtonsoft.Json.Linq;
using Xunit;
using static AbilityKit.BehaviorTree.Tests.TestNodeTypes;

namespace AbilityKit.BehaviorTree.Tests
{
    /// <summary>授权文档 → 运行时 IR 的导出管线：布局剥离、golden 稳定、校验门禁、roundtrip。</summary>
    public sealed class BtAuthoringExportTests
    {
        private static BtAuthoringSourceDocument AuthoringDocument()
        {
            var definition = new TreeBuilder()
                .Blackboard("test.result", BtValueType.Int64)
                .Node("root", BtBuiltInNodeTypes.Sequence, "a", "b")
                .Node("a", ScriptedAction)
                .Node("b", BtBuiltInNodeTypes.Succeed)
                .Root("root");

            var document = new BtAuthoringSourceDocument { Tree = definition };
            document.Metadata.Author = "hobobo";
            document.NodeMetadata.Add(new BtAuthoringNodeMetadata
                { NodeId = "root", DisplayName = "Root Sequence", Comment = "editor only" });
            document.Layout.Add(new BtNodeLayoutData { NodeId = "root", X = 10, Y = 20 });
            document.Layout.Add(new BtNodeLayoutData { NodeId = "a", X = 100, Y = 30 });
            document.Layout.Add(new BtNodeLayoutData { NodeId = "b", X = 200, Y = 40 });
            document.Groups.Add(new BtAuthoringGroupData
            {
                Id = "g1",
                Title = "战斗意图",
                X = 0,
                Y = 0,
                Width = 300,
                Height = 200,
                NodeIds = { "a", "b" },
            });
            document.Notes.Add(new BtAuthoringNoteData
            {
                Id = "note-1",
                Text = "仅供策划阅读",
                X = 24,
                Y = 48,
                Width = 220,
                Height = 120,
            });
            return document;
        }

        [Fact]
        public void Export_StripsLayoutFromRuntimeIr()
        {
            var json = BtTreeExporter.Export(AuthoringDocument(), CreateRegistry(), out var errors);
            Assert.Empty(errors);

            // 运行时 IR 不含布局/分组，也不含授权元数据
            Assert.DoesNotContain("\"layout\"", json);
            Assert.DoesNotContain("\"groups\"", json);
            Assert.DoesNotContain("\"notes\"", json);
            Assert.DoesNotContain("\"author\"", json);
            Assert.DoesNotContain("\"nodeMetadata\"", json);
            Assert.DoesNotContain("\"displayName\"", json);
            Assert.DoesNotContain("\"comment\"", json);
            Assert.DoesNotContain("仅供策划阅读", json);
            Assert.DoesNotContain("\"x\"", json);
            Assert.DoesNotContain("abilitykit-bt-authoring", json);
            foreach (var node in (JArray)JObject.Parse(json)["nodes"]!)
            {
                Assert.Null(node["name"]);
                Assert.Null(node["comment"]);
            }
        }

        [Fact]
        public void Export_ProducesStableGoldenOutput()
        {
            var first = BtTreeExporter.Export(AuthoringDocument(), CreateRegistry(), out _);
            var second = BtTreeExporter.Export(AuthoringDocument(), CreateRegistry(), out _);
            Assert.Equal(first, second);
        }

        [Fact]
        public void Export_RejectsInvalidTree_WithoutClearingErrors()
        {
            var document = AuthoringDocument();
            document.Tree.RootNodeId = "missing";

            var json = BtTreeExporter.Export(document, CreateRegistry(), out var errors);
            Assert.Null(json);
            Assert.Contains(errors, e => e.Contains("Root node"));
        }

        [Fact]
        public void Export_UnknownNodeType_Fails()
        {
            var document = AuthoringDocument();
            document.Tree.Nodes[1].Type = "nope.unknown";

            var json = BtTreeExporter.Export(document, CreateRegistry(), out var errors);
            Assert.Null(json);
            Assert.Contains(errors, e => e.Contains("unknown type"));
        }

        [Fact]
        public void ToRuntimeDefinition_ReturnsDetachedCopy()
        {
            var document = AuthoringDocument();
            var definition = BtTreeExporter.ToRuntimeDefinition(document);

            // 修改返回值不影响编辑态文档
            definition.Nodes[1].Type = "polluted.type";
            Assert.NotEqual("polluted.type", document.Tree.Nodes[1].Type);
        }

        [Fact]
        public void AuthoringJson_Roundtrip_PreservesLayout()
        {
            var document = AuthoringDocument();
            var json = BtAuthoringJson.Save(document);
            var loaded = BtAuthoringJson.Load(json);

            Assert.Equal(3, loaded.Layout.Count);
            Assert.Single(loaded.Groups);
            Assert.Single(loaded.Notes);
            Assert.Equal("战斗意图", loaded.Groups[0].Title);
            Assert.Equal("仅供策划阅读", loaded.Notes[0].Text);
            Assert.Equal(24f, loaded.Notes[0].X);
            Assert.Equal(10f, loaded.Layout[0].X);
            Assert.Equal("Root Sequence", loaded.NodeMetadata[0].DisplayName);
            Assert.Equal("editor only", loaded.NodeMetadata[0].Comment);
            Assert.Equal(document.Tree.ComputeDefinitionHash(), loaded.Tree.ComputeDefinitionHash());
        }

        [Fact]
        public void AuthoringJson_MigratesLegacyNodeNameAndComment()
        {
            var root = JObject.Parse(BtAuthoringJson.Save(AuthoringDocument()));
            root["version"] = BtAuthoringSchema.LegacyVersion;
            root.Remove("nodeMetadata");
            var node = (JObject)root["tree"]!["nodes"]![0]!;
            node["name"] = "Legacy Root";
            node["comment"] = "legacy note";

            var loaded = BtAuthoringJson.Load(root.ToString());
            Assert.Equal(BtAuthoringSchema.Version, loaded.Version);
            Assert.True(loaded.TryGetNodeMetadata("root", out var metadata));
            Assert.Equal("Legacy Root", metadata.DisplayName);
            Assert.Equal("legacy note", metadata.Comment);

            var upgraded = BtAuthoringJson.Save(loaded);
            Assert.Contains("\"nodeMetadata\"", upgraded);
            var upgradedRoot = JObject.Parse(upgraded);
            Assert.Null(upgradedRoot["tree"]!["nodes"]![0]!["name"]);
            Assert.Null(upgradedRoot["tree"]!["nodes"]![0]!["comment"]);
        }

        [Fact]
        public void Import_ProducesEditableDocumentWithEmptyLayout()
        {
            var definition = new TreeBuilder()
                .Node("root", BtBuiltInNodeTypes.Succeed)
                .Root("root");

            var document = BtTreeExporter.Import(definition);
            Assert.Single(document.Tree.Nodes);
            Assert.Single(document.Layout);
            Assert.Single(document.NodeMetadata);
            Assert.Equal("root", document.Layout[0].NodeId);
            Assert.Equal("root", document.NodeMetadata[0].DisplayName);
            Assert.Equal(0f, document.Layout[0].X);
        }

        [Fact]
        public void ExportedJson_LoadsAndRuns()
        {
            var json = BtTreeExporter.Export(AuthoringDocument(), CreateRegistry(), out _);
            var runtime = BtTreeRuntime.Create(BtTreeJson.Load(json), CreateRegistry());
            runtime.Enable();
            runtime.Blackboard.SetInt64("test.result", 1);
            runtime.Update(1, AbilityKit.Deterministic.Fixed64.Zero);
            Assert.Equal(BtNodeState.Success, runtime.RootNodeState);
        }

        [Fact]
        public void GoldenExamples_ValidateCleanAndExport()
        {
            foreach (var document in BtAuthoringGoldenExamples.BuildAll())
            {
                var json = BtTreeExporter.Export(document, CreateRegistry(), out var errors);
                Assert.Empty(errors);
                Assert.NotNull(json);
                Assert.Contains("\"golden.hero_combat\"", json);
            }
        }

        [Fact]
        public void GoldenExamples_ExportIsStable()
        {
            foreach (var document in BtAuthoringGoldenExamples.BuildAll())
            {
                var first = BtTreeExporter.Export(document, CreateRegistry(), out _);
                var second = BtTreeExporter.Export(document, CreateRegistry(), out _);
                Assert.Equal(first, second);
            }
        }

        [Fact]
        public void GoldenHeroCombat_RunsAndCompletesCast()
        {
            var document = BtAuthoringGoldenExamples.BuildHeroCombat();
            var json = BtTreeExporter.Export(document, CreateRegistry(), out _);

            var runtime = BtTreeRuntime.Create(BtTreeJson.Load(json), CreateRegistry());
            runtime.Enable();
            runtime.Blackboard.SetBool("self.hasTarget", true);
            runtime.Blackboard.SetBool("self.canCast", true);

            runtime.Update(1, AbilityKit.Deterministic.Fixed64.Zero);
            Assert.Equal(BtNodeState.Running, runtime.RootNodeState);   // castWait 等待中
            runtime.Update(2, AbilityKit.Deterministic.Fixed64.FromRatio(1, 2));
            Assert.Equal(BtNodeState.Success, runtime.RootNodeState);
            // castWait 成功 → Selector 完成 → hold 未运行，out.hold 保持默认 true
            Assert.True(runtime.Blackboard.GetBool("out.hold"));
        }
    }
}
