using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AbilityKit.BattleScenario;
using AbilityKit.BattleScenario.Editor;
using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.Scenario;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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

    public sealed class MobaBattleScenarioWorkspaceTests
    {
        [Test]
        public void CaseLibrary_FindsRepositoryScenarioByHeroSlotAndTag()
        {
            var window = ScriptableObject.CreateInstance<BattleScenarioWindow>();
            try
            {
                var matches = window.FilterCases("金克丝 W skill-presentation").Select(entry => Path.GetFileName(entry.Path)).ToArray();
                Assert.That(matches, Is.EqualTo(new[] { "jinx-w.battlescenario" }));
                Assert.That(window.FilterCases("金克丝W jinx-w").Count(), Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

        [Test]
        public void Workspace_RestoresSelectedCaseFromSerializedWindowState()
        {
            var window = ScriptableObject.CreateInstance<BattleScenarioWindow>();
            var restored = ScriptableObject.CreateInstance<BattleScenarioWindow>();
            try
            {
                var path = window.FilterCases("金克丝W jinx-w").Single().Path;
                Assert.IsTrue(window.LoadCaseFromPath(path));
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(window), restored);
                restored.RestoreSelectedCase();
                Assert.IsTrue(restored.TryGetSavedCasePath(out var restoredPath));
                Assert.That(restoredPath, Is.EqualTo(path));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                UnityEngine.Object.DestroyImmediate(restored);
            }
        }

        [Test]
        public void Workspace_DispatchesBothActionsFromSavedCaseAndRejectsDrift()
        {
            var window = ScriptableObject.CreateInstance<BattleScenarioWindow>();
            var oldRunner = BattleScenarioRunnerRegistry.Runner;
            var oldPreview = BattleScenarioEditorIntegration.PlaySavedCase;
            var directory = Path.Combine(Path.GetTempPath(), "abilitykit-workspace-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "case.battlescenario");
            var scenePath = Path.Combine(directory, "arena.battlescene");
            try
            {
                var original = Path.Combine(BattleScenarioEditorIntegration.CaseDirectories.Single(), "SkillPresentation", "jinx-w.battlescenario");
                var document = BattleScenarioCodec.Load(original);
                BattleScenarioCodec.SaveScene(scenePath, new BattleSceneDocument
                {
                    SceneId = "arena",
                    Sections = new BattleScenarioSections { Setup = document.Sections.Setup },
                });
                document.Sections.Setup = new System.Collections.Generic.List<BattleBlock>();
                document.ScenarioRef = "arena.battlescene";
                BattleScenarioCodec.Save(path, document);

                var runner = new RecordingRunner();
                BattleScenarioRunnerRegistry.Runner = runner;
                var previews = new System.Collections.Generic.List<(string Path, bool PreviewOnly)>();
                BattleScenarioEditorIntegration.PlaySavedCase = (file, previewOnly) => { previews.Add((file, previewOnly)); return true; };

                Assert.IsTrue(window.LoadCaseFromPath(path));
                window.RunSavedAssertions();
                window.PlaySavedCase(previewOnly: true);
                window.PlaySavedCase(previewOnly: false);
                Assert.That(runner.Calls, Is.EqualTo(1));
                Assert.That(runner.Scenario?.CaseId, Is.EqualTo("jinx-w"));
                Assert.That(runner.Scenario?.Actors.Count, Is.EqualTo(2));
                Assert.That(previews, Is.EqualTo(new[] { (path, true), (path, false) }));

                typeof(BattleScenarioWindow).GetField("_displayName", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(window, "未保存的编辑");
                Assert.That(window.TryGetSavedCasePath(out _), Is.False);
                window.RunSavedAssertions();
                window.PlaySavedCase(previewOnly: true);
                Assert.That(runner.Calls, Is.EqualTo(1));
                Assert.That(previews.Count, Is.EqualTo(2));

                Assert.IsTrue(window.LoadCaseFromPath(path));
                File.AppendAllText(scenePath, "\n");
                Assert.That(window.TryGetSavedCasePath(out _), Is.False);
                Assert.IsTrue(window.LoadCaseFromPath(path));
                Assert.IsTrue(window.TryGetSavedCasePath(out _));
                File.AppendAllText(path, "\n");
                Assert.That(window.TryGetSavedCasePath(out _), Is.False);
            }
            finally
            {
                BattleScenarioRunnerRegistry.Runner = oldRunner;
                BattleScenarioEditorIntegration.PlaySavedCase = oldPreview;
                UnityEngine.Object.DestroyImmediate(window);
                File.Delete(path);
                File.Delete(scenePath);
                Directory.Delete(directory);
            }
        }

        private sealed class RecordingRunner : IBattleScenarioRunner
        {
            public int Calls;
            public TestScenario Scenario;

            public BattleScenarioRunResult Run(TestScenario scenario)
            {
                Calls++;
                Scenario = scenario;
                return new BattleScenarioRunResult { Passed = true };
            }
        }
    }
}
