#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.BehaviorTree.Authoring;
using NUnit.Framework;

using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Editor.Tests
{
    /// <summary>
    /// Golden 示例验收（与 dotnet 侧 BtAuthoringExportTests 同构）：授权 → 校验 → 导出契约
    /// 的 Unity EditMode 哨兵。任何一侧漂移都会在这里先红。
    /// </summary>
    public sealed class AuthoringGoldenExamplesTests
    {
        private static NodeRegistry BuiltinRegistry()
        {
            var registry = new NodeRegistry();
            BuiltInNodes.RegisterAll(registry);
            return registry;
        }

        [Test]
        public void GoldenExamples_ValidateCleanAndExport()
        {
            var registry = BuiltinRegistry();
            foreach (var document in AuthoringGoldenExamples.BuildAll())
            {
                var json = TreeExporter.Export(document, registry, out var errors);
                Assert.That(errors, Is.Empty);
                Assert.That(json, Is.Not.Null);
                Assert.That(json, Does.Contain("golden.hero_combat"));
            }
        }

        [Test]
        public void GoldenExamples_ExportIsStable()
        {
            var registry = BuiltinRegistry();
            foreach (var document in AuthoringGoldenExamples.BuildAll())
            {
                var first = TreeExporter.Export(document, registry, out _);
                var second = TreeExporter.Export(document, registry, out _);
                Assert.That(first, Is.EqualTo(second));
            }
        }

        [Test]
        public void AuthoringJson_RoundtripsThroughAssetModel()
        {
            var document = AuthoringGoldenExamples.BuildHeroCombat();
            var json = AuthoringJson.Save(document);
            var loaded = AuthoringJson.Load(json);

            Assert.That(loaded.Layout, Has.Count.EqualTo(document.Layout.Count));
            Assert.That(loaded.Groups, Has.Count.EqualTo(document.Groups.Count));
            Assert.That(loaded.Notes, Has.Count.EqualTo(document.Notes.Count));
            Assert.That(
                loaded.Tree.ComputeDefinitionHash(),
                Is.EqualTo(document.Tree.ComputeDefinitionHash()));
        }

        [Test]
        public void ProjectAndDirectExport_ProduceIdenticalRuntimeBytesAndIncrementalUnchanged()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "AbilityKit.BtGolden." + Guid.NewGuid().ToString("N"));
            var sourceDirectory = Path.Combine(root, "source");
            var directDirectory = Path.Combine(root, "direct");
            var projectDirectory = Path.Combine(root, "project");
            Directory.CreateDirectory(sourceDirectory);

            try
            {
                var document = AuthoringGoldenExamples.BuildHeroCombat();
                var treeId = document.Tree.TreeId;
                File.WriteAllText(
                    Path.Combine(sourceDirectory, treeId + ".json"),
                    AuthoringJson.Save(document));

                var registry = BuiltinRegistry();
                var directReport = ExportPipeline.ExportAll(
                    new[] { new KeyValuePair<string, AuthoringSourceDocument>(treeId, document) },
                    new[] { directDirectory },
                    registry,
                    root);
                var manifest = new ProjectManifest
                {
                    SourceDirectory = sourceDirectory,
                    SourceKind = SourceKind.AuthoringDocument,
                    Trees = new List<string> { treeId },
                    ExportTargets = new List<string> { projectDirectory }
                };
                var projectReport = ExportPipeline.ExportProject(manifest, registry, root);

                Assert.That(directReport, Has.Count.EqualTo(1));
                Assert.That(projectReport, Has.Count.EqualTo(1));
                Assert.That(directReport[0].Status, Is.EqualTo(ExportStatus.Exported));
                Assert.That(projectReport[0].Status, Is.EqualTo(ExportStatus.Exported));
                Assert.That(
                    File.ReadAllBytes(Path.Combine(directDirectory, treeId + ".json")),
                    Is.EqualTo(File.ReadAllBytes(Path.Combine(projectDirectory, treeId + ".json"))));

                var repeated = ExportPipeline.ExportProject(manifest, registry, root);
                Assert.That(repeated[0].Status, Is.EqualTo(ExportStatus.Unchanged));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }
    }
}
#endif
