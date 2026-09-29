using System;
using System.IO;
using AbilityKit.BattleScenario;
using AbilityKit.Demo.Moba.EnvironmentModel;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Editor.BattleScenario.Tests
{
    public sealed class MobaBattleScenarioSyncBackendTests
    {
        [Test]
        public void EditorRunner_UsesUnityBackendAndReportsAssertionFailure()
        {
            var scenario = BattleScenarioCompiler.Compile("editor-sync", MobaBattleScenarioDslParser.Parse(@"
sync-backend unity-route
network packet inbound opcode=5202 seq=1 frame=1 at=0
network packet inbound opcode=5202 seq=2 frame=2 at=34
assert-sync finalHash eq 999
"));
            var result = new MobaBattleScenarioRunner().Run(scenario);
            Assert.That(result.Passed, Is.False);
            StringAssert.Contains("syncBackend=unity-route", result.Summary);
            StringAssert.Contains("prediction.finalHash", result.Summary);
        }

        [Test]
        public void EditorRunner_RejectsUnsupportedGameplayInsteadOfPassing()
        {
            var scenario = BattleScenarioCompiler.Compile("unsupported", MobaBattleScenarioDslParser.Parse(@"
sync-backend unity-route
spawn caster hero=1001
"));
            Assert.Throws<NotSupportedException>(() => new MobaBattleScenarioRunner().Run(scenario));
        }

        [Test]
        public void EditorBatch_UnityCasesAndMalformedFilesAreReportedIndividually()
        {
            var directory = Path.Combine(Path.GetTempPath(), "abilitykit-sync-batch-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                BattleScenarioCodec.Save(Path.Combine(directory, "valid.battlescenario"), new BattleScenarioDocument
                {
                    CaseId = "unity-batch",
                    Blocks = new System.Collections.Generic.List<BattleBlock>(
                        MobaBattleScenarioDslParser.Parse("sync-backend unity-route\nassert-sync finalHash eq 0")),
                });
                File.WriteAllText(Path.Combine(directory, "bad.battlescenario"), "not json");
                var report = new MobaBattleScenarioRunner().RunDirectory(directory);
                StringAssert.Contains("total=2, passed=1, failed=1", report);
                StringAssert.Contains("bad: FAILED", report);
                StringAssert.Contains("syncBackend=unity-route", report);
            }
            finally
            {
                File.Delete(Path.Combine(directory, "valid.battlescenario"));
                File.Delete(Path.Combine(directory, "bad.battlescenario"));
                Directory.Delete(directory);
            }
        }
    }
}
