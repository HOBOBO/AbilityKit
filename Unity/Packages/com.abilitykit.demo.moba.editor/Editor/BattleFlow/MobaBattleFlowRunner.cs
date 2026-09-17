#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using AbilityKit.BattleFlow;
using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.Game.Battle.Testing;
using AbilityKit.Scenario;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Demo.Moba.Editor.BattleFlow
{
    /// <summary>
    /// MOBA 的战斗流程运行器（项目扩展）：把编译出的 <see cref="TestScenario"/> shell-out 到 .NET headless 命令跑出 verdict + trace。
    /// 编辑器（Unity）不能进程内 boot console 世界，故序列化场景 → 写临时文件 → 调 .NET runner → 读结果文件。
    /// </summary>
    [InitializeOnLoad]
    public sealed class MobaBattleFlowRunner : IBattleFlowRunner, IBattleFlowBatchRunner
    {
        static MobaBattleFlowRunner()
        {
            var runner = new MobaBattleFlowRunner();
            BattleFlowRunnerRegistry.Runner = runner;
            BattleFlowRunnerRegistry.BatchRunner = runner;
            BattleFlowRunnerRegistry.DslParser = MobaBattleFlowDslParser.Parse;
        }

        public BattleFlowRunResult Run(TestScenario scenario)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            var backend = MobaPredictionBackendIds.Normalize(
                (scenario.Expectations as MobaBattleFlowAssertions)?.PredictionBackend ??
                MobaPredictionBackendIds.Headless);
            if (backend == MobaPredictionBackendIds.UnityRoute)
                return RunUnityRoute(scenario);
            var tmpDir = Path.Combine(Path.GetTempPath(), "abilitykit-battleflow");
            Directory.CreateDirectory(tmpDir);
            var scenarioPath = Path.Combine(tmpDir, "scenario-" + Guid.NewGuid().ToString("N") + ".json");
            var resultPath = Path.Combine(tmpDir, "result-" + Guid.NewGuid().ToString("N") + ".txt");
            ScenarioCodec.Save(scenarioPath, scenario);

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --project \"" + ResolveRunnerProject() + "\" -- \"" + scenarioPath + "\" \"" + resultPath + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (var process = Process.Start(psi))
            {
                process!.WaitForExit();
                if (process.ExitCode != 0)
                    return new BattleFlowRunResult { Passed = false, Summary = "runner 退出码 " + process.ExitCode };
            }

            if (!File.Exists(resultPath))
                return new BattleFlowRunResult { Passed = false, Summary = "runner 未产出结果文件" };

            var lines = File.ReadAllLines(resultPath);
            var passed = lines.Length > 0 && lines[0] == "PASSED";
            var summary = lines.Length > 1 ? string.Join("\n", lines, 1, lines.Length - 1) : string.Empty;
            return new BattleFlowRunResult { Passed = passed, Summary = summary, Trace = ReadTrace(resultPath) };
        }

        public string RunDirectory(string directory)
        {
            // Keep Unity-only cases in this process, including mixed-backend batches.
            var files = Directory.GetFiles(directory, "*.battleflow");
            Array.Sort(files, StringComparer.Ordinal);
            var scenarios = new List<TestScenario>();
            var loadFailures = new List<string>();
            var requiresUnity = false;
            foreach (var file in files)
            {
                try
                {
                    var document = BattleFlowCodec.Load(file);
                    var scenario = BattleFlowCompiler.Compile(document.CaseId, document.Blocks);
                    scenarios.Add(scenario);
                    requiresUnity |= (scenario.Expectations as MobaBattleFlowAssertions)?.PredictionBackend
                        == MobaPredictionBackendIds.UnityRoute;
                }
                catch (Exception ex)
                {
                    loadFailures.Add(Path.GetFileNameWithoutExtension(file) + ": FAILED " + ex.Message);
                }
            }
            if (requiresUnity)
            {
                var lines = new List<string>(loadFailures);
                var passed = 0;
                foreach (var scenario in scenarios)
                {
                    try
                    {
                        var result = Run(scenario);
                        if (result.Passed) passed++;
                        lines.Add(scenario.CaseId + ": " + result.Summary);
                    }
                    catch (Exception ex)
                    {
                        lines.Add(scenario.CaseId + ": FAILED " + ex.Message);
                    }
                }
                return $"total={files.Length}, passed={passed}, failed={files.Length - passed}\n" +
                    string.Join("\n", lines);
            }
            var tmpDir = Path.Combine(Path.GetTempPath(), "abilitykit-battleflow");
            Directory.CreateDirectory(tmpDir);
            var reportPath = Path.Combine(tmpDir, "batch-" + Guid.NewGuid().ToString("N") + ".txt");

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --project \"" + ResolveRunnerProject() + "\" -- --batch \"" + directory + "\" \"" + reportPath + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            int exitCode;
            using (var process = Process.Start(psi))
            {
                process!.WaitForExit();
                exitCode = process.ExitCode;
            }

            return File.Exists(reportPath)
                ? File.ReadAllText(reportPath)
                : "runner 未产出报告文件（退出码 " + exitCode + "）";
        }

        /// <summary>仓库根 = Unity 工程目录的上级（Application.dataPath 是 &lt;repo&gt;/Unity/Assets）。</summary>
        private static BattleFlowRunResult RunUnityRoute(TestScenario scenario)
        {
            var assertions = scenario.Expectations as MobaBattleFlowAssertions;
            if (scenario.Actors.Count > 0 || scenario.Setup.Count > 0 || scenario.Timeline.Count > 0 ||
                scenario.Obstacles.Count > 0 || !string.IsNullOrEmpty(scenario.EnvironmentProfileId) ||
                scenario.NetworkWatches.Count > 0 || scenario.WorldParameters.Count > 0 ||
                !string.IsNullOrEmpty(scenario.NavigationProfileId) || scenario.WorldProfileId != "default" ||
                (scenario.Expectations != null && assertions == null) ||
                (assertions?.MustContain.Count ?? 0) > 0 || (assertions?.MustNotContain.Count ?? 0) > 0 ||
                (assertions?.State.Count ?? 0) > 0 || (assertions?.Context.Count ?? 0) > 0 ||
                (assertions?.Relationships.Count ?? 0) > 0)
                throw new NotSupportedException(
                    "unity-route currently supports network and assert-sync scenarios only, not MOBA gameplay content.");
            var observation = MobaBattleFlowPredictionScenarioRunner.Run(
                scenario, new UnityRouteMobaPredictionScenarioBackend());
            var verdict = MobaBattleFlowPredictionScenarioRunner.Verify(
                assertions?.Prediction, observation);
            return new BattleFlowRunResult
            {
                Passed = verdict.Passed,
                Summary = $"syncBackend={observation.BackendId}, prediction={observation.ConfirmedFrame}/{observation.PredictedFrame}, " +
                    $"mismatch={observation.Mismatches}, rollback={observation.Rollbacks}, finalHash={observation.FinalStateHash}, " +
                    $"fingerprint={observation.DeterminismFingerprint}, verdict={(verdict.Passed ? "PASSED" : "FAILED")}" +
                    (verdict.Passed ? string.Empty : ", syncFailures=" + string.Join(" | ", verdict.Failures)),
            };
        }

        private static string ResolveRunnerProject()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            return Path.Combine(repoRoot, "src", "AbilityKit.Demo.Moba.BattleFlow.Runner", "AbilityKit.Demo.Moba.BattleFlow.Runner.csproj");
        }

        /// <summary>读取 .NET runner 顺带回传的 trace 树 JSON（缺失/损坏不致命）。</summary>
        private static List<BattleFlowTraceNode> ReadTrace(string resultPath)
        {
            var tracePath = resultPath + ".trace.json";
            if (!File.Exists(tracePath)) return new List<BattleFlowTraceNode>();
            try
            {
                return JsonConvert.DeserializeObject<List<BattleFlowTraceNode>>(File.ReadAllText(tracePath))
                       ?? new List<BattleFlowTraceNode>();
            }
            catch
            {
                return new List<BattleFlowTraceNode>();
            }
        }
    }
}
#endif
