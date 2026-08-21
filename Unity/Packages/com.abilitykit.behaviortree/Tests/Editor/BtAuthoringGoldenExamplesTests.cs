#if UNITY_EDITOR
using AbilityKit.BehaviorTree.Authoring;
using NUnit.Framework;

namespace AbilityKit.BehaviorTree.Editor.Tests
{
    /// <summary>
    /// Golden 示例验收（与 dotnet 侧 BtAuthoringExportTests 同构）：授权 → 校验 → 导出契约
    /// 的 Unity EditMode 哨兵。任何一侧漂移都会在这里先红。
    /// </summary>
    public sealed class BtAuthoringGoldenExamplesTests
    {
        private static BtNodeRegistry BuiltinRegistry()
        {
            var registry = new BtNodeRegistry();
            BtBuiltInNodes.RegisterAll(registry);
            return registry;
        }

        [Test]
        public void GoldenExamples_ValidateCleanAndExport()
        {
            var registry = BuiltinRegistry();
            foreach (var document in BtAuthoringGoldenExamples.BuildAll())
            {
                var json = BtTreeExporter.Export(document, registry, out var errors);
                Assert.That(errors, Is.Empty);
                Assert.That(json, Is.Not.Null);
                Assert.That(json, Does.Contain("golden.hero_combat"));
            }
        }

        [Test]
        public void GoldenExamples_ExportIsStable()
        {
            var registry = BuiltinRegistry();
            foreach (var document in BtAuthoringGoldenExamples.BuildAll())
            {
                var first = BtTreeExporter.Export(document, registry, out _);
                var second = BtTreeExporter.Export(document, registry, out _);
                Assert.That(first, Is.EqualTo(second));
            }
        }

        [Test]
        public void AuthoringJson_RoundtripsThroughAssetModel()
        {
            var document = BtAuthoringGoldenExamples.BuildHeroCombat();
            var json = BtAuthoringJson.Save(document);
            var loaded = BtAuthoringJson.Load(json);

            Assert.That(loaded.Layout, Has.Count.EqualTo(document.Layout.Count));
            Assert.That(loaded.Groups, Has.Count.EqualTo(document.Groups.Count));
            Assert.That(
                loaded.Tree.ComputeDefinitionHash(),
                Is.EqualTo(document.Tree.ComputeDefinitionHash()));
        }
    }
}
#endif
