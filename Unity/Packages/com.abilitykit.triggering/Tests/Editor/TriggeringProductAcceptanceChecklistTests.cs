using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.UI;

namespace AbilityKit.Triggering.Tests
{
    public sealed class TriggeringProductAcceptanceChecklistTests
    {
        [Test]
        public void Documentation_ReferencesFormalSampleAndDiagnostics()
        {
            var packageRoot = FindPackageRoot();
            var productGuide = File.ReadAllText(Path.Combine(packageRoot, "Documentation~", "ProductAcceptanceGuide.md"));
            var stageClosureGuide = File.ReadAllText(Path.Combine(packageRoot, "Documentation~", "StageClosureGuide.md"));
            var packageReadme = File.ReadAllText(Path.Combine(packageRoot, "Documentation~", "README.md"));

            Assert.That(Directory.Exists(Path.Combine(packageRoot, "Document")), Is.False);
            Assert.That(File.Exists(Path.Combine(packageRoot, "Document.meta")), Is.False);
            StringAssert.Contains("Samples~/FormalMainline/FormalTriggeringMainlineExample.cs", productGuide);
            StringAssert.Contains("TriggeringDiagnosticCollector", productGuide);
            StringAssert.Contains("dotnet test src/AbilityKit.Triggering.Tests/AbilityKit.Triggering.Tests.csproj", productGuide);
            StringAssert.Contains("TriggeringRuntimePoolingHotspotScanTests", productGuide);
            StringAssert.Contains("new ActionSchedulerPools", stageClosureGuide);
            StringAssert.Contains("(ProductAcceptanceGuide.md#固定验证流程)", stageClosureGuide);
            StringAssert.Contains("(StageClosureGuide.md)", packageReadme);
            StringAssert.Contains("AbilityKit/Triggering/Diagnostics/Write Runtime Report", packageReadme);
            StringAssert.Contains("AbilityKit/Triggering/Diagnostics/Write Product Acceptance Report", packageReadme);
        }

        /// <summary>
        /// 保证即时样例不会重新混入加载适配器、任务调度和旧入口。
        /// </summary>
        [Test]
        public void FormalSample_UsesOnlyImmediateMainlineEntries()
        {
            var packageRoot = FindPackageRoot();
            var samplePath = Path.Combine(packageRoot, "Samples~", "FormalMainline", "FormalTriggeringMainlineExample.cs");
            var text = File.ReadAllText(samplePath);

            StringAssert.Contains("TriggerRunner<ExampleContext>", text);
            StringAssert.Contains("TriggerPlanFactory.When<DamageEvent>", text);
            StringAssert.Contains("ThrowIfInvalidRuntimeBoundary", text);
            Assert.That(text, Does.Not.Contain("TriggerPlanJsonDatabase"));
            Assert.That(text, Does.Not.Contain("RuleSchedulerRegistry"));
            Assert.That(text, Does.Not.Contain("ActionSchedulerManager"));
            Assert.That(text, Does.Not.Contain(".Tick("));
            Assert.That(text, Does.Not.Contain(".Update("));
            Assert.That(text, Does.Not.Contain("Runtime.Scheduler"));
            Assert.That(text, Does.Not.Contain("TriggerDispatcherHub"));
            Assert.That(text, Does.Not.Contain("Runtime.Executable"));
        }

        /// <summary>
        /// 验证包管理器只公开受支持的样例，且旧目录不再包含示例源码。
        /// </summary>
        [Test]
        public void PackageManager_OffersOnlySupportedSamples()
        {
            const string packageName = "com.abilitykit.triggering";
            var package = PackageInfo.FindForAssetPath($"Packages/{packageName}/package.json");
            Assert.That(package, Is.Not.Null);
            var samples = Sample.FindByPackage(packageName, package.version).ToArray();

            var expectedScripts = new System.Collections.Generic.Dictionary<string, string>
            {
                ["Triggering Mainline"] = "FormalTriggeringMainlineExample.cs",
                ["Triggering Queued Events"] = "QueuedEventBusExample.cs"
            };
            Assert.That(samples.Select(sample => sample.displayName), Is.EquivalentTo(expectedScripts.Keys));
            foreach (var sample in samples)
            {
                Assert.That(File.Exists(Path.Combine(sample.resolvedPath, expectedScripts[sample.displayName])), Is.True, sample.displayName);
            }

            var oldSamples = Path.Combine(package.resolvedPath, "Samples");
            if (Directory.Exists(oldSamples))
            {
                Assert.That(Directory.EnumerateFiles(oldSamples, "*.cs", SearchOption.AllDirectories), Is.Empty);
            }
        }

        [Test]
        public void CommercialChecklist_MarksProductizationItemsAsLanded()
        {
            var packageRoot = FindPackageRoot();
            var checklist = File.ReadAllText(Path.Combine(packageRoot, "Documentation~", "Triggering-Commercial-Remediation-Checklist.md"));

            StringAssert.Contains("收敛正式主线 API（已落地）", checklist);
            StringAssert.Contains("统一调度体系职责（已落地第一轮）", checklist);
            StringAssert.Contains("建立正式 API 边界规范（已落地）", checklist);
            StringAssert.Contains("建立商业化验收基线（已落地产品验收指南与回归入口）", checklist);
            StringAssert.Contains("完善编辑器工具链（已完成诊断与验收报告入口）", checklist);
            StringAssert.Contains("增强测试矩阵（已落地产品化清单回归）", checklist);
            StringAssert.Contains("收敛命名与目录结构（已完成文档边界与样板收口）", checklist);
            StringAssert.Contains("规划下线 legacy 入口（已完成调用方防回流与下线跟踪）", checklist);
        }

        [Test]
        public void ProductAcceptanceReport_SummarizesChecklistOneToFive()
        {
            var packageRoot = FindPackageRoot();
            var menuPath = Path.Combine(packageRoot, "Editor", "Diagnostics", "TriggeringProductAcceptanceMenu.cs");
            var reportSource = File.ReadAllText(menuPath);

            StringAssert.Contains("## Checklist 1-5", reportSource);
            StringAssert.Contains("1. Mainline API convergence: completed.", reportSource);
            StringAssert.Contains("2. Scheduling responsibility split: completed.", reportSource);
            StringAssert.Contains("3. Formal API boundary policy: completed.", reportSource);
            StringAssert.Contains("4. Commercial acceptance baseline: completed.", reportSource);
            StringAssert.Contains("5. Editor diagnostics and acceptance reports: completed.", reportSource);
            StringAssert.Contains("Documentation~/FormalApiBoundary.md", reportSource);
        }

        private static string FindPackageRoot()
        {
            var candidates = new[]
            {
                Path.Combine("Packages", "com.abilitykit.triggering"),
                Path.Combine("Unity", "Packages", "com.abilitykit.triggering"),
                Path.Combine("..", "Packages", "com.abilitykit.triggering"),
                Path.Combine("..", "Unity", "Packages", "com.abilitykit.triggering")
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                var fullPath = Path.GetFullPath(candidates[i]);
                if (Directory.Exists(fullPath))
                    return fullPath;
            }

            Assert.Fail("Unable to locate com.abilitykit.triggering package root.");
            return null;
        }
    }
}
