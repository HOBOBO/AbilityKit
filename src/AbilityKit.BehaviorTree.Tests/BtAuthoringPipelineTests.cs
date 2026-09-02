using System.Collections.Generic;
using System.IO;
using AbilityKit.BehaviorTree.Authoring;
using Xunit;
using static AbilityKit.BehaviorTree.Tests.TestNodeTypes;

namespace AbilityKit.BehaviorTree.Tests
{
    /// <summary>内容管线：模板、批量扇出导出、增量跳过、校验门禁与 TreeId 唯一性。</summary>
    public sealed class BtAuthoringPipelineTests
    {
        private static BtNodeRegistry BuiltinRegistry()
        {
            var registry = new BtNodeRegistry();
            BtBuiltInNodes.RegisterAll(registry);
            return registry;
        }

        [Fact]
        public void Templates_AllValidateCleanAndExport()
        {
            var registry = BuiltinRegistry();
            var index = 0;
            foreach (var (displayName, build) in BtAuthoringTemplates.Catalog())
            {
                var document = build();
                document.Tree.TreeId = "tpl_" + index++;
                var json = BtTreeExporter.Export(document, registry, out var errors);
                Assert.True(errors.Count == 0, $"模板 '{displayName}' 校验失败: {string.Join("; ", errors)}");
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
                var document = BtAuthoringTemplates.BuildReactiveLoop();
                document.Tree.TreeId = "pipeline_demo";
                var trees = new List<KeyValuePair<string, BtAuthoringSourceDocument>> { new("pipeline_demo", document) };

                var report = BtAuthoringExportPipeline.ExportAll(trees, new[] { targetA, targetB }, registry, root);
                Assert.Equal(2, report.Count);
                Assert.All(report, e => Assert.Equal(BtExportStatus.Exported, e.Status));
                Assert.True(File.Exists(Path.Combine(targetA, "pipeline_demo.json")));
                Assert.True(File.Exists(Path.Combine(targetB, "pipeline_demo.json")));

                // 二次导出：内容一致 -> Unchanged，不重写
                var report2 = BtAuthoringExportPipeline.ExportAll(trees, new[] { targetA, targetB }, registry, root);
                Assert.All(report2, e => Assert.Equal(BtExportStatus.Unchanged, e.Status));

                // 只改编辑态显示信息：运行时产物不变 -> Unchanged
                document.GetOrCreateNodeMetadata("root").DisplayName = "改名";
                var report3 = BtAuthoringExportPipeline.ExportAll(trees, new[] { targetA }, registry, root);
                Assert.All(report3, e => Assert.Equal(BtExportStatus.Unchanged, e.Status));

                // 修改运行时属性后重导 -> Exported
                var action = document.Tree.Nodes.Find(n => n.Id == "act")!;
                action.Properties.Set(BtWaitNode.DurationSecondsProperty,
                    BtPropertyValue.Of(AbilityKit.Deterministic.Fixed64.One));
                var report4 = BtAuthoringExportPipeline.ExportAll(trees, new[] { targetA }, registry, root);
                Assert.All(report4, e => Assert.Equal(BtExportStatus.Exported, e.Status));
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
                var document = BtAuthoringTemplates.BuildEmpty();
                document.Tree.TreeId = "bad_tree";

                // 先导出一份有效内容
                BtAuthoringExportPipeline.ExportAll(
                    new List<KeyValuePair<string, BtAuthoringSourceDocument>> { new("bad_tree", document) },
                    new[] { target }, registry, root);
                var filePath = Path.Combine(target, "bad_tree.json");
                var goodJson = File.ReadAllText(filePath);

                // 破坏结构再导：Error 且旧产物未被清空
                document.Tree.RootNodeId = "missing";
                var report = BtAuthoringExportPipeline.ExportAll(
                    new List<KeyValuePair<string, BtAuthoringSourceDocument>> { new("bad_tree", document) },
                    new[] { target }, registry, root);
                Assert.Equal(BtExportStatus.Error, report[0].Status);
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
            var document = BtAuthoringTemplates.BuildEmpty();
            document.Tree.TreeId = "orphan";

            var report = BtAuthoringExportPipeline.ExportAll(
                new List<KeyValuePair<string, BtAuthoringSourceDocument>> { new("orphan", document) },
                new string[0], registry, ".");
            Assert.Single(report);
            Assert.Equal(BtExportStatus.SkippedNoTargets, report[0].Status);
        }

        [Fact]
        public void RelativeTargets_ResolveAgainstRepositoryRoot()
        {
            var resolved = BtAuthoringExportPipeline.ResolveDirectory("Configs/moba/bt", "C:/repo/root");
            Assert.Equal(Path.GetFullPath("C:/repo/root/Configs/moba/bt"), resolved);

            var absolute = BtAuthoringExportPipeline.ResolveDirectory("C:/elsewhere", "C:/repo/root");
            Assert.Equal(Path.GetFullPath("C:/elsewhere"), absolute);
        }

        [Fact]
        public void DuplicateTreeIds_AreDetected()
        {
            var errors = BtAuthoringExportPipeline.ValidateUniqueTreeIds(new[] { "a", "b", "a", "" });
            Assert.Contains(errors, e => e.Contains("'a' 重复"));
            Assert.Contains(errors, e => e.Contains("空 TreeId"));
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

                // 源：一份授权文档的运行时导出（模拟"从 JSON 目录导入再导出"）
                var document = BtAuthoringTemplates.BuildReactiveLoop();
                document.Tree.TreeId = "project_tree";
                File.WriteAllText(Path.Combine(sourceDir, "project_tree.json"),
                    BtTreeExporter.Export(document, registry, out _)!);

                var manifest = new BtAuthoringProjectManifest
                {
                    Trees = { "project_tree" },
                    SourceDirectory = sourceDir,
                    ExportTargets = { targetA, targetB },
                };

                var report = BtAuthoringExportPipeline.ExportProject(manifest, registry, root);
                Assert.Equal(2, report.Count);
                Assert.All(report, e => Assert.Equal(BtExportStatus.Exported, e.Status));

                // 导出结果与源定义哈希一致（round-trip 等价）
                var exported = BtTreeJson.Load(File.ReadAllText(Path.Combine(targetA, "project_tree.json")));
                Assert.Equal(document.Tree.ComputeDefinitionHash(), exported.ComputeDefinitionHash());

                // 二次导出：Unchanged（增量）
                var report2 = BtAuthoringExportPipeline.ExportProject(manifest, registry, root);
                Assert.All(report2, e => Assert.Equal(BtExportStatus.Unchanged, e.Status));
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
                var manifest = new BtAuthoringProjectManifest
                {
                    Trees = { "missing_tree" },
                    SourceDirectory = Path.Combine(root, "none"),
                    ExportTargets = { target },
                };
                var report = BtAuthoringExportPipeline.ExportProject(manifest, BuiltinRegistry(), root);
                Assert.Single(report);
                Assert.Equal(BtExportStatus.Error, report[0].Status);
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
                var document = BtAuthoringTemplates.BuildEmpty();
                document.Tree.TreeId = "authored_tree";
                document.GetOrCreateNodeMetadata("root").DisplayName = "策划显示名";
                document.GetOrCreateNodeMetadata("root").Comment = "仅编辑器可见";
                File.WriteAllText(Path.Combine(sourceDir, "authored_tree.json"), BtAuthoringJson.Save(document));

                var manifest = new BtAuthoringProjectManifest
                {
                    SourceDirectory = sourceDir,
                    SourceKind = BtAuthoringSourceKind.AuthoringDocument,
                    Trees = { "authored_tree" },
                    ExportTargets = { target },
                };

                var report = BtAuthoringExportPipeline.ExportProject(manifest, BuiltinRegistry(), root);
                Assert.Single(report);
                Assert.Equal(BtExportStatus.Exported, report[0].Status);

                var source = File.ReadAllText(Path.Combine(sourceDir, "authored_tree.json"));
                var runtime = File.ReadAllText(Path.Combine(target, "authored_tree.json"));
                Assert.Contains("策划显示名", source);
                Assert.Contains("仅编辑器可见", source);
                Assert.DoesNotContain("策划显示名", runtime);
                Assert.DoesNotContain("仅编辑器可见", runtime);
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
            // golden 模板与 golden 例子同源：定义哈希一致（模板漂移哨兵）
            var fromTemplate = BtAuthoringTemplates.Catalog()[2].Build();
            var fromGolden = BtAuthoringGoldenExamples.BuildHeroCombat();
            Assert.Equal(
                fromGolden.Tree.ComputeDefinitionHash(),
                fromTemplate.Tree.ComputeDefinitionHash());
        }

        [Fact]
        public void GraphOperations_RejectCyclesMultipleParentsAndCapacityOverflow()
        {
            var tree = new TreeBuilder()
                .Node("root", BtBuiltInNodeTypes.Sequence, "left", "right")
                .Node("left", BtBuiltInNodeTypes.Sequence, "leaf")
                .Node("right", BtBuiltInNodeTypes.Sequence)
                .Node("leaf", BtBuiltInNodeTypes.Succeed)
                .Node("orphan", BtBuiltInNodeTypes.Succeed)
                .Root("root");

            Assert.False(BtAuthoringGraphOperations.CanConnect(tree, "leaf", "root", -1, out var cycle));
            Assert.Contains("形成环", cycle);
            Assert.False(BtAuthoringGraphOperations.CanConnect(tree, "right", "leaf", -1, out var multipleParents));
            Assert.Contains("已属于父节点", multipleParents);
            Assert.False(BtAuthoringGraphOperations.CanConnect(tree, "right", "root", 0, out var capacity));
            Assert.Contains("最多允许", capacity);
            Assert.True(BtAuthoringGraphOperations.CanConnect(tree, "right", "orphan", -1, out _));
        }

        [Fact]
        public void GraphOperations_MoveChildChangesExecutionOrderOnly()
        {
            var tree = new TreeBuilder()
                .Node("root", BtBuiltInNodeTypes.Sequence, "a", "b", "c")
                .Node("a", BtBuiltInNodeTypes.Succeed)
                .Node("b", BtBuiltInNodeTypes.Succeed)
                .Node("c", BtBuiltInNodeTypes.Succeed)
                .Root("root");

            Assert.True(BtAuthoringGraphOperations.MoveChild(tree, "root", 2, 0));
            Assert.Equal(new[] { "c", "a", "b" }, tree.Nodes[0].ChildIds);
            Assert.False(BtAuthoringGraphOperations.MoveChild(tree, "root", 0, -1));
        }
    }
}
