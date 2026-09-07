using System.Collections.Generic;
using System.IO;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;
using Xunit;
using static AbilityKit.BehaviorTree.Tests.TestNodeTypes;

namespace AbilityKit.BehaviorTree.Tests
{
    /// <summary>鍐呭绠＄嚎锛氭ā鏉裤€佹壒閲忔墖鍑哄鍑恒€佸閲忚烦杩囥€佹牎楠岄棬绂佷笌 TreeId 鍞竴鎬с€?/summary>
    public sealed class BtAuthoringPipelineTests
    {
        private static NodeRegistry BuiltinRegistry()
        {
            var registry = new NodeRegistry();
            BuiltInNodes.RegisterAll(registry);
            return registry;
        }

        [Fact]
        public void Templates_AllValidateCleanAndExport()
        {
            var registry = BuiltinRegistry();
            var index = 0;
            foreach (var (displayName, build) in AuthoringTemplates.Catalog())
            {
                var document = build();
                document.Tree.TreeId = "tpl_" + index++;
                var json = TreeExporter.Export(document, registry, out var errors);
                Assert.True(errors.Count == 0, $"妯℃澘 '{displayName}' 鏍￠獙澶辫触: {string.Join("; ", errors)}");
                Assert.NotNull(json);
            }
        }

        [Fact]
        public void ExportAll_FansOutToAllTargets_AndSkipsUnchanged()
        {
            var root = Path.Combine(Path.GetTempPath(), "ak-bt-pipeline-" + System.Guid.NewGuid().ToString("N"));
            var targetA = Path.Combine(root, "a");
            var targetB = Path.Combine(root, "b");
            try
            {
                var registry = BuiltinRegistry();
                var document = AuthoringTemplates.BuildReactiveLoop();
                document.Tree.TreeId = "pipeline_demo";
                var trees = new List<KeyValuePair<string, AuthoringSourceDocument>> { new("pipeline_demo", document) };

                var report = ExportPipeline.ExportAll(trees, new[] { targetA, targetB }, registry, root);
                Assert.Equal(2, report.Count);
                Assert.All(report, e => Assert.Equal(ExportStatus.Exported, e.Status));
                Assert.True(File.Exists(Path.Combine(targetA, "pipeline_demo.json")));
                Assert.True(File.Exists(Path.Combine(targetB, "pipeline_demo.json")));

                // 浜屾瀵煎嚭锛氬唴瀹逛竴鑷?-> Unchanged锛屼笉閲嶅啓
                var report2 = ExportPipeline.ExportAll(trees, new[] { targetA, targetB }, registry, root);
                Assert.All(report2, e => Assert.Equal(ExportStatus.Unchanged, e.Status));

                // 鍙敼缂栬緫鎬佹樉绀轰俊鎭細杩愯鏃朵骇鐗╀笉鍙?-> Unchanged
                document.GetOrCreateNodeMetadata("root").DisplayName = "鏀瑰悕";
                var report3 = ExportPipeline.ExportAll(trees, new[] { targetA }, registry, root);
                Assert.All(report3, e => Assert.Equal(ExportStatus.Unchanged, e.Status));

                // 淇敼杩愯鏃跺睘鎬у悗閲嶅 -> Exported
                var action = document.Tree.Nodes.Find(n => n.Id == "act")!;
                action.Properties.Set("durationSeconds",
                    PropertyValue.Of(AbilityKit.Deterministic.Fixed64.One));
                var report4 = ExportPipeline.ExportAll(trees, new[] { targetA }, registry, root);
                Assert.All(report4, e => Assert.Equal(ExportStatus.Exported, e.Status));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void ExportAll_InvalidTree_KeepsOldArtifact()
        {
            var root = Path.Combine(Path.GetTempPath(), "ak-bt-pipeline-" + System.Guid.NewGuid().ToString("N"));
            var target = Path.Combine(root, "out");
            try
            {
                var registry = BuiltinRegistry();
                var document = AuthoringTemplates.BuildEmpty();
                document.Tree.TreeId = "bad_tree";

                ExportPipeline.ExportAll(
                    new List<KeyValuePair<string, AuthoringSourceDocument>> { new("bad_tree", document) },
                    new[] { target }, registry, root);
                var filePath = Path.Combine(target, "bad_tree.json");
                var goodJson = File.ReadAllText(filePath);

                // 鐮村潖缁撴瀯鍐嶅锛欵rror 涓旀棫浜х墿鏈娓呯┖
                document.Tree.RootNodeId = "missing";
                var report = ExportPipeline.ExportAll(
                    new List<KeyValuePair<string, AuthoringSourceDocument>> { new("bad_tree", document) },
                    new[] { target }, registry, root);
                Assert.Equal(ExportStatus.Error, report[0].Status);
                Assert.Equal(goodJson, File.ReadAllText(filePath));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void ExportAll_NoTargets_IsSkipped()
        {
            var registry = BuiltinRegistry();
            var document = AuthoringTemplates.BuildEmpty();
            document.Tree.TreeId = "orphan";

            var report = ExportPipeline.ExportAll(
                new List<KeyValuePair<string, AuthoringSourceDocument>> { new("orphan", document) },
                new string[0], registry, ".");
            Assert.Single(report);
            Assert.Equal(ExportStatus.SkippedNoTargets, report[0].Status);
        }

        [Fact]
        public void RelativeTargets_ResolveAgainstRepositoryRoot()
        {
            var resolved = ExportPipeline.ResolveDirectory("Configs/moba/bt", "C:/repo/root");
            Assert.Equal(Path.GetFullPath("C:/repo/root/Configs/moba/bt"), resolved);

            var absolute = ExportPipeline.ResolveDirectory("C:/elsewhere", "C:/repo/root");
            Assert.Equal(Path.GetFullPath("C:/elsewhere"), absolute);
        }

        [Fact]
        public void DuplicateTreeIds_AreDetected()
        {
            var errors = ExportPipeline.ValidateUniqueTreeIds(new[] { "a", "b", "a", "" });
            Assert.Contains(errors, e => e.Contains("'a'") && e.Contains("重复"));
            Assert.Contains(errors, e => e.Contains("TreeId"));
        }

        [Fact]
        public void ExportProject_FromManifest_FansOutAndRoundTrips()
        {
            var root = Path.Combine(Path.GetTempPath(), "ak-bt-project-" + System.Guid.NewGuid().ToString("N"));
            var sourceDir = Path.Combine(root, "src");
            var targetA = Path.Combine(root, "a");
            var targetB = Path.Combine(root, "b");
            try
            {
                Directory.CreateDirectory(sourceDir);
                var registry = BuiltinRegistry();

                var document = AuthoringTemplates.BuildReactiveLoop();
                document.Tree.TreeId = "project_tree";
                File.WriteAllText(Path.Combine(sourceDir, "project_tree.json"),
                    TreeExporter.Export(document, registry, out _)!);

                var manifest = new ProjectManifest
                {
                    Trees = { "project_tree" },
                    SourceDirectory = sourceDir,
                    ExportTargets = { targetA, targetB },
                };

                var report = ExportPipeline.ExportProject(manifest, registry, root);
                Assert.Equal(2, report.Count);
                Assert.All(report, e => Assert.Equal(ExportStatus.Exported, e.Status));

                var exported = TreeJson.Load(File.ReadAllText(Path.Combine(targetA, "project_tree.json")));
                Assert.Equal(document.Tree.ComputeDefinitionHash(), exported.ComputeDefinitionHash());

                // 浜屾瀵煎嚭锛歎nchanged锛堝閲忥級
                var report2 = ExportPipeline.ExportProject(manifest, registry, root);
                Assert.All(report2, e => Assert.Equal(ExportStatus.Unchanged, e.Status));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void ExportProject_MissingSource_ReportsErrorPerTarget()
        {
            var root = Path.Combine(Path.GetTempPath(), "ak-bt-project-" + System.Guid.NewGuid().ToString("N"));
            var target = Path.Combine(root, "out");
            try
            {
                var manifest = new ProjectManifest
                {
                    Trees = { "missing_tree" },
                    SourceDirectory = Path.Combine(root, "none"),
                    ExportTargets = { target },
                };
                var report = ExportPipeline.ExportProject(manifest, BuiltinRegistry(), root);
                Assert.Single(report);
                Assert.Equal(ExportStatus.Error, report[0].Status);
                Assert.Contains("源文件不存在", report[0].Message);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void ExportProject_AuthoringSource_PreservesEditorDataButExportsPureRuntimeIr()
        {
            var root = Path.Combine(Path.GetTempPath(), "ak-bt-authoring-project-" + System.Guid.NewGuid().ToString("N"));
            var sourceDir = Path.Combine(root, "src");
            var target = Path.Combine(root, "out");
            try
            {
                Directory.CreateDirectory(sourceDir);
                var document = AuthoringTemplates.BuildEmpty();
                document.Tree.TreeId = "authored_tree";
                document.GetOrCreateNodeMetadata("root").DisplayName = "Designer Display";
                document.GetOrCreateNodeMetadata("root").Comment = "editor only";
                File.WriteAllText(Path.Combine(sourceDir, "authored_tree.json"), AuthoringJson.Save(document));

                var manifest = new ProjectManifest
                {
                    SourceDirectory = sourceDir,
                    SourceKind = SourceKind.AuthoringDocument,
                    Trees = { "authored_tree" },
                    ExportTargets = { target },
                };

                var report = ExportPipeline.ExportProject(manifest, BuiltinRegistry(), root);
                Assert.Single(report);
                Assert.Equal(ExportStatus.Exported, report[0].Status);

                var source = File.ReadAllText(Path.Combine(sourceDir, "authored_tree.json"));
                var runtime = File.ReadAllText(Path.Combine(target, "authored_tree.json"));
                Assert.Contains("Designer Display", source);
                Assert.Contains("editor only", source);
                Assert.DoesNotContain("Designer Display", runtime);
                Assert.DoesNotContain("editor only", runtime);
                Assert.DoesNotContain("nodeMetadata", runtime);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void GoldenHeroCombatTemplate_MatchesGoldenExample()
        {
            var fromTemplate = AuthoringTemplates.Catalog()[2].Build();
            var fromGolden = AuthoringGoldenExamples.BuildHeroCombat();
            Assert.Equal(
                fromGolden.Tree.ComputeDefinitionHash(),
                fromTemplate.Tree.ComputeDefinitionHash());
        }

        [Fact]
        public void GraphOperations_RejectCyclesMultipleParentsAndCapacityOverflow()
        {
            var tree = new ApiTreeBuilder()
                .Node("root", BuiltInNodeTypes.Sequence, "left", "right")
                .Node("left", BuiltInNodeTypes.Sequence, "leaf")
                .Node("right", BuiltInNodeTypes.Sequence)
                .Node("leaf", BuiltInNodeTypes.Succeed)
                .Node("orphan", BuiltInNodeTypes.Succeed)
                .Root("root");

            Assert.False(GraphOperations.CanConnect(tree, "leaf", "root", -1, out var cycle));
            Assert.False(string.IsNullOrWhiteSpace(cycle));
            Assert.False(GraphOperations.CanConnect(tree, "right", "leaf", -1, out var multipleParents));
            Assert.False(string.IsNullOrWhiteSpace(multipleParents));
            Assert.False(GraphOperations.CanConnect(tree, "right", "root", 0, out var capacity));
            Assert.False(string.IsNullOrWhiteSpace(capacity));
            Assert.True(GraphOperations.CanConnect(tree, "right", "orphan", -1, out _));
        }

        [Fact]
        public void GraphOperations_MoveChildChangesExecutionOrderOnly()
        {
            var tree = new ApiTreeBuilder()
                .Node("root", BuiltInNodeTypes.Sequence, "a", "b", "c")
                .Node("a", BuiltInNodeTypes.Succeed)
                .Node("b", BuiltInNodeTypes.Succeed)
                .Node("c", BuiltInNodeTypes.Succeed)
                .Root("root");

            Assert.True(GraphOperations.MoveChild(tree, "root", 2, 0));
            Assert.Equal(new[] { "c", "a", "b" }, tree.Nodes[0].ChildIds);
            Assert.False(GraphOperations.MoveChild(tree, "root", 0, -1));
        }
    }
}

