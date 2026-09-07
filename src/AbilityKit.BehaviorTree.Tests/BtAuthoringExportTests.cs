using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Serialization;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;
using Newtonsoft.Json.Linq;
using Xunit;
using static AbilityKit.BehaviorTree.Tests.TestNodeTypes;

namespace AbilityKit.BehaviorTree.Tests
{
    /// <summary>鎺堟潈鏂囨。 鈫?杩愯鏃?IR 鐨勫鍑虹绾匡細甯冨眬鍓ョ銆乬olden 绋冲畾銆佹牎楠岄棬绂併€乺oundtrip銆?/summary>
    public sealed class BtAuthoringExportTests
    {
        private static AuthoringSourceDocument AuthoringDocument()
        {
            var definition = new ApiTreeBuilder()
                .Blackboard("test.result", ValueType.Int64)
                .Node("root", BuiltInNodeTypes.Sequence, "a", "b")
                .Node("a", ScriptedAction)
                .Node("b", BuiltInNodeTypes.Succeed)
                .Root("root");

            var document = new AuthoringSourceDocument { Tree = definition };
            document.Metadata.Author = "hobobo";
            document.NodeMetadata.Add(new AuthoringNodeMetadata
                { NodeId = "root", DisplayName = "Root Sequence", Comment = "editor only" });
            document.Layout.Add(new NodeLayoutData { NodeId = "root", X = 10, Y = 20 });
            document.Layout.Add(new NodeLayoutData { NodeId = "a", X = 100, Y = 30 });
            document.Layout.Add(new NodeLayoutData { NodeId = "b", X = 200, Y = 40 });
            document.Groups.Add(new AuthoringGroupData
            {
                Id = "g1",
                Title = "鎴樻枟鎰忓浘",
                X = 0,
                Y = 0,
                Width = 300,
                Height = 200,
                NodeIds = { "a", "b" },
            });
            document.Notes.Add(new AuthoringNoteData
            {
                Id = "note-1",
                Text = "浠呬緵绛栧垝闃呰",
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
            var json = TreeExporter.Export(AuthoringDocument(), CreateApiRegistry(), out var errors);
            Assert.Empty(errors);

            // 杩愯鏃?IR 涓嶅惈甯冨眬/鍒嗙粍锛屼篃涓嶅惈鎺堟潈鍏冩暟鎹?            Assert.DoesNotContain("\"layout\"", json);
            Assert.DoesNotContain("\"groups\"", json);
            Assert.DoesNotContain("\"notes\"", json);
            Assert.DoesNotContain("\"author\"", json);
            Assert.DoesNotContain("\"nodeMetadata\"", json);
            Assert.DoesNotContain("\"displayName\"", json);
            Assert.DoesNotContain("\"comment\"", json);
            Assert.DoesNotContain("浠呬緵绛栧垝闃呰", json);
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
            var first = TreeExporter.Export(AuthoringDocument(), CreateApiRegistry(), out _);
            var second = TreeExporter.Export(AuthoringDocument(), CreateApiRegistry(), out _);
            Assert.Equal(first, second);
        }

        [Fact]
        public void Export_RejectsInvalidTree_WithoutClearingErrors()
        {
            var document = AuthoringDocument();
            document.Tree.RootNodeId = "missing";

            var json = TreeExporter.Export(document, CreateApiRegistry(), out var errors);
            Assert.Null(json);
            Assert.Contains(errors, e => e.Contains("Root node"));
        }

        [Fact]
        public void Export_UnknownNodeType_Fails()
        {
            var document = AuthoringDocument();
            document.Tree.Nodes[1].Type = "nope.unknown";

            var json = TreeExporter.Export(document, CreateApiRegistry(), out var errors);
            Assert.Null(json);
            Assert.Contains(errors, e => e.Contains("unknown type"));
        }

        [Fact]
        public void ToRuntimeDefinition_ReturnsDetachedCopy()
        {
            var document = AuthoringDocument();
            var definition = TreeExporter.ToRuntimeDefinition(document);

            // 淇敼杩斿洖鍊间笉褰卞搷缂栬緫鎬佹枃妗?            definition.Nodes[1].Type = "polluted.type";
            Assert.NotEqual("polluted.type", document.Tree.Nodes[1].Type);
        }

        [Fact]
        public void AuthoringJson_Roundtrip_PreservesLayout()
        {
            var document = AuthoringDocument();
            var json = AuthoringJson.Save(document);
            var loaded = AuthoringJson.Load(json);

            Assert.Equal(3, loaded.Layout.Count);
            Assert.Single(loaded.Groups);
            Assert.Single(loaded.Notes);
            Assert.Equal("鎴樻枟鎰忓浘", loaded.Groups[0].Title);
            Assert.Equal("浠呬緵绛栧垝闃呰", loaded.Notes[0].Text);
            Assert.Equal(24f, loaded.Notes[0].X);
            Assert.Equal(10f, loaded.Layout[0].X);
            Assert.Equal("Root Sequence", loaded.NodeMetadata[0].DisplayName);
            Assert.Equal("editor only", loaded.NodeMetadata[0].Comment);
            Assert.Equal(document.Tree.ComputeDefinitionHash(), loaded.Tree.ComputeDefinitionHash());
        }

        [Fact]
        public void AuthoringJson_MigratesLegacyNodeNameAndComment()
        {
            var root = JObject.Parse(AuthoringJson.Save(AuthoringDocument()));
            root["version"] = AuthoringSchema.LegacyVersion;
            root.Remove("nodeMetadata");
            var node = (JObject)root["tree"]!["nodes"]![0]!;
            node["name"] = "Legacy Root";
            node["comment"] = "legacy note";

            var loaded = AuthoringJson.Load(root.ToString());
            Assert.Equal(AuthoringSchema.Version, loaded.Version);
            Assert.True(loaded.TryGetNodeMetadata("root", out var metadata));
            Assert.Equal("Legacy Root", metadata.DisplayName);
            Assert.Equal("legacy note", metadata.Comment);

            var upgraded = AuthoringJson.Save(loaded);
            Assert.Contains("\"nodeMetadata\"", upgraded);
            var upgradedRoot = JObject.Parse(upgraded);
            Assert.Null(upgradedRoot["tree"]!["nodes"]![0]!["name"]);
            Assert.Null(upgradedRoot["tree"]!["nodes"]![0]!["comment"]);
        }

        [Fact]
        public void Import_ProducesEditableDocumentWithEmptyLayout()
        {
            var definition = new ApiTreeBuilder()
                .Node("root", BuiltInNodeTypes.Succeed)
                .Root("root");

            var document = TreeExporter.Import(definition);
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
            var json = TreeExporter.Export(AuthoringDocument(), CreateApiRegistry(), out _);
            var runtime = TreeRuntime.Create(TreeJson.Load(json), CreateApiRegistry());
            runtime.Enable();
            runtime.Blackboard.SetInt64("test.result", 1);
            runtime.Update(1, AbilityKit.Deterministic.Fixed64.Zero);
            Assert.Equal(NodeState.Success, runtime.RootNodeState);
        }

        [Fact]
        public void GoldenExamples_ValidateCleanAndExport()
        {
            foreach (var document in AuthoringGoldenExamples.BuildAll())
            {
                var json = TreeExporter.Export(document, CreateApiRegistry(), out var errors);
                Assert.Empty(errors);
                Assert.NotNull(json);
                Assert.Contains("\"golden.hero_combat\"", json);
            }
        }

        [Fact]
        public void GoldenExamples_ExportIsStable()
        {
            foreach (var document in AuthoringGoldenExamples.BuildAll())
            {
                var first = TreeExporter.Export(document, CreateApiRegistry(), out _);
                var second = TreeExporter.Export(document, CreateApiRegistry(), out _);
                Assert.Equal(first, second);
            }
        }

        [Fact]
        public void GoldenHeroCombat_RunsAndCompletesCast()
        {
            var document = AuthoringGoldenExamples.BuildHeroCombat();
            var json = TreeExporter.Export(document, CreateApiRegistry(), out _);

            var runtime = TreeRuntime.Create(TreeJson.Load(json), CreateApiRegistry());
            runtime.Enable();
            runtime.Blackboard.SetBool("self.hasTarget", true);
            runtime.Blackboard.SetBool("self.canCast", true);

            runtime.Update(1, AbilityKit.Deterministic.Fixed64.Zero);
            Assert.Equal(NodeState.Running, runtime.RootNodeState);
            runtime.Update(2, AbilityKit.Deterministic.Fixed64.FromRatio(1, 2));
            Assert.Equal(NodeState.Success, runtime.RootNodeState);
            // castWait 鎴愬姛 鈫?Selector 瀹屾垚 鈫?hold 鏈繍琛岋紝out.hold 淇濇寔榛樿 true
            Assert.True(runtime.Blackboard.GetBool("out.hold"));
        }
    }
}


