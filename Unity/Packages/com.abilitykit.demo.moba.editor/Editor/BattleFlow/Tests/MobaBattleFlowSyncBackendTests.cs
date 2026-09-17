using System;
using System.IO;
using AbilityKit.BattleFlow;
using AbilityKit.Demo.Moba.EnvironmentModel;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Editor.BattleFlow.Tests
{
    public sealed class MobaBattleFlowSyncBackendTests
    {
        [Test]
        public void EditorRunner_UsesUnityBackendAndReportsAssertionFailure()
        {
            var scenario = BattleFlowCompiler.Compile("editor-sync", MobaBattleFlowDslParser.Parse(@"
sync-backend unity-route
network packet inbound opcode=5202 seq=1 frame=1 at=0
network packet inbound opcode=5202 seq=2 frame=2 at=34
assert-sync finalHash eq 999
"));
            var result = new MobaBattleFlowRunner().Run(scenario);
            Assert.That(result.Passed, Is.False);
            StringAssert.Contains("syncBackend=unity-route", result.Summary);
            StringAssert.Contains("prediction.finalHash", result.Summary);
        }

        [Test]
        public void EditorRunner_RejectsUnsupportedGameplayInsteadOfPassing()
        {
            var scenario = BattleFlowCompiler.Compile("unsupported", MobaBattleFlowDslParser.Parse(@"
sync-backend unity-route
spawn caster hero=1001
"));
            Assert.Throws<NotSupportedException>(() => new MobaBattleFlowRunner().Run(scenario));
        }

        [Test]
        public void EditorBatch_UnityCasesAndMalformedFilesAreReportedIndividually()
        {
            var directory = Path.Combine(Path.GetTempPath(), "abilitykit-sync-batch-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                BattleFlowCodec.Save(Path.Combine(directory, "valid.battleflow"), new BattleFlowDocument
                {
                    CaseId = "unity-batch",
                    Blocks = new System.Collections.Generic.List<BattleBlock>(
                        MobaBattleFlowDslParser.Parse("sync-backend unity-route\nassert-sync finalHash eq 0")),
                });
                File.WriteAllText(Path.Combine(directory, "bad.battleflow"), "not json");
                var report = new MobaBattleFlowRunner().RunDirectory(directory);
                StringAssert.Contains("total=2, passed=1, failed=1", report);
                StringAssert.Contains("bad: FAILED", report);
                StringAssert.Contains("syncBackend=unity-route", report);
            }
            finally
            {
                File.Delete(Path.Combine(directory, "valid.battleflow"));
                File.Delete(Path.Combine(directory, "bad.battleflow"));
                Directory.Delete(directory);
            }
        }
    }
}
