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

                // 修改后重导 -> Exported
                document.Tree.Nodes[0].Name = "改名";
                var report3 = BtAuthoringExportPipeline.ExportAll(trees, new[] { targetA }, registry, root);
                Assert.All(report3, e => Assert.Equal(BtExportStatus.Exported, e.Status));
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
        public void GoldenHeroCombatTemplate_MatchesGoldenExample()
        {
            // golden 模板与 golden 例子同源：定义哈希一致（模板漂移哨兵）
            var fromTemplate = BtAuthoringTemplates.Catalog()[2].Build();
            var fromGolden = BtAuthoringGoldenExamples.BuildHeroCombat();
            Assert.Equal(
                fromGolden.Tree.ComputeDefinitionHash(),
                fromTemplate.Tree.ComputeDefinitionHash());
        }
    }
}
