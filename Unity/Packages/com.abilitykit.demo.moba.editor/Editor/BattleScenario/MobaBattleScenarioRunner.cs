#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AbilityKit.BattleScenario;
using AbilityKit.BattleScenario.Editor;
using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.Game.Battle.Testing;
using AbilityKit.Scenario;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Demo.Moba.Editor.BattleScenario
{
    /// <summary>
    /// MOBA 的战斗流程运行器（项目扩展）：把编译出的 <see cref="TestScenario"/> shell-out 到 .NET headless 命令跑出 verdict + trace。
    /// 编辑器（Unity）不能进程内 boot console 世界，故序列化场景 → 写临时文件 → 调 .NET runner → 读结果文件。
    /// </summary>
    [InitializeOnLoad]
    public sealed class MobaBattleScenarioRunner : IBattleScenarioRunner, IBattleScenarioBatchRunner
    {
        static MobaBattleScenarioRunner()
        {
            var runner = new MobaBattleScenarioRunner();
            BattleScenarioRunnerRegistry.Runner = runner;
            BattleScenarioRunnerRegistry.BatchRunner = runner;
            BattleScenarioRunnerRegistry.DslParser = MobaBattleScenarioDslParser.Parse;
            BattleScenarioRunnerRegistry.DocumentDslParser = MobaBattleScenarioDslParser.ParseDocument;
        }

        internal sealed class ArchiveBatchExecution
        {
            internal string OutputRoot;
            internal string LogDirectory;
            internal string RunId;
            internal string Error;
            internal int? ExitCode;
        }

        /// <summary>
        /// 默认从仓库已保存的技能表现预设选用例。
        /// </summary>
        internal static string DefaultArchivePresetDirectory() =>
            Path.Combine(Path.GetDirectoryName(ResolveRunnerProject()), "Flows", "SkillPresentation");

        /// <summary>
        /// 在编辑器后台启动唯一的 .NET CLI；不经场景编辑器的混合宿主路由。
        /// </summary>
        internal static Task<ArchiveBatchExecution> ExecuteArchiveBatch(string presetDirectory, string outputRoot,
            string mode, IReadOnlyList<string> selectedCases)
        {
            var projectPath = ResolveRunnerProject();
            if (!File.Exists(projectPath))
                throw new FileNotFoundException("未找到仓库内的 MOBA Battle Scenario Runner。", projectPath);
            var repository = Directory.GetParent(Path.GetDirectoryName(projectPath)).Parent.FullName;
            var logDirectory = Path.Combine(repository, "local", "Logs", "moba-scenario-editor",
                $"{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
            return Task.Run(() => RunArchiveBatch(projectPath, repository, presetDirectory, outputRoot, mode, selectedCases, logDirectory));
        }

        private static ArchiveBatchExecution RunArchiveBatch(string projectPath, string repository, string presetDirectory,
            string outputRoot, string mode, IReadOnlyList<string> selectedCases, string logDirectory)
        {
            var result = new ArchiveBatchExecution { OutputRoot = outputRoot, LogDirectory = logDirectory };
            try
            {
                Directory.CreateDirectory(logDirectory);
                Directory.CreateDirectory(outputRoot);
                if ((File.GetAttributes(outputRoot) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("分析库不能是重解析点。");
                var receipt = Path.Combine(logDirectory, "run-id.txt");
                var arguments = new List<string> { "run", "--project", projectPath, "--", "--batch", presetDirectory,
                    "--output-dir", outputRoot, "--evidence", mode, "--receipt", receipt };
                foreach (var caseFile in selectedCases) { arguments.Add("--case"); arguments.Add(caseFile); }
                var start = new ProcessStartInfo
                {
                    FileName = "dotnet", Arguments = string.Join(" ", arguments.ConvertAll(QuoteProcessArgument)),
                    WorkingDirectory = repository, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                };
                start.EnvironmentVariables["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
                File.WriteAllText(Path.Combine(logDirectory, "command.txt"), start.FileName + " " + start.Arguments);
                using var stdout = File.Create(Path.Combine(logDirectory, "stdout.log"));
                using var stderr = File.Create(Path.Combine(logDirectory, "stderr.log"));
                using var process = Process.Start(start) ?? throw new InvalidOperationException("无法启动 dotnet。");
                var copyStdout = process.StandardOutput.BaseStream.CopyToAsync(stdout);
                var copyStderr = process.StandardError.BaseStream.CopyToAsync(stderr);
                process.WaitForExit();
                Task.WaitAll(copyStdout, copyStderr);
                result.ExitCode = process.ExitCode;
                if (File.Exists(receipt)) result.RunId = File.ReadAllText(receipt).Trim();
                if (string.IsNullOrEmpty(result.RunId)) result.Error = "Console Runner 没有返回已发布批次。";
            }
            catch (Exception error) { result.Error = error.Message; }
            return result;
        }

        private static string QuoteProcessArgument(string argument) =>
            "\"" + Regex.Replace(argument, "(\\\\*)(\"|$)", match =>
                match.Groups[1].Value + match.Groups[1].Value + (match.Groups[2].Length == 0 ? "" : "\\\"")) + "\"";

        /// <summary>
        /// 在编辑器主线程定位仓库，再后台运行本地弹体表现验收；不打开窗口或修改场景。
        /// </summary>
        internal static Task<LocalStateSyncReplay> CaptureProjectilePresentationReplay(bool accepted)
        {
            var projectPath = ResolveRunnerProject();
            if (!File.Exists(projectPath))
                throw new FileNotFoundException("未找到仓库内的 MOBA Battle Scenario runner。", projectPath);
            var repository = Directory.GetParent(Path.GetDirectoryName(projectPath)).Parent.FullName;
            return Task.Run(() => RunProjectilePresentationReplay(repository, projectPath, accepted));
        }

        private static LocalStateSyncReplay RunProjectilePresentationReplay(string repository, string projectPath, bool accepted)
        {
            const string caseId = "jinx-w-local-presentation";
            var variant = accepted ? "confirmed" : "rejected";
            var output = Path.Combine(repository, "local", "Logs", "moba-acceptance",
                $"{DateTime.Now:yyyyMMdd-HHmmss}-projectile-{variant}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(output);
            var start = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --no-restore --project \"{projectPath}\" -- --statesync-jinx-presentation \"{output}\"",
                WorkingDirectory = repository,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            start.EnvironmentVariables["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            File.WriteAllText(Path.Combine(output, "command.txt"), start.FileName + " " + start.Arguments);
            var logPath = Path.Combine(output, "runner.log");
            try
            {
                using var process = Process.Start(start) ?? throw new InvalidOperationException("无法启动本地 runner。");
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(180_000))
                {
                    if (!process.HasExited) process.Kill();
                    throw new TimeoutException("本地 runner 超过 180 秒。");
                }
                if (!Task.WaitAll(new Task[] { stdout, stderr }, 5_000))
                    throw new TimeoutException("本地 runner 已退出，但日志输出未及时关闭。");
                File.WriteAllText(logPath, stdout.GetAwaiter().GetResult() + Environment.NewLine + stderr.GetAwaiter().GetResult());
                if (process.ExitCode != 0)
                    throw new InvalidOperationException($"本地 runner 退出码 {process.ExitCode}。请检查本地构建和运行日志。");

                var replay = LocalStateSyncReplay.Parse(File.ReadAllText(Path.Combine(output, $"{caseId}.{variant}.statesync-replay.json")));
                if (replay.CaseId != caseId || replay.Variant != variant)
                    throw new InvalidDataException("本地 runner 返回了不匹配的样例。");
                return replay;
            }
            catch (Exception error)
            {
                File.AppendAllText(logPath, Environment.NewLine + error);
                throw new InvalidOperationException($"{error.Message} 日志：{logPath}", error);
            }
        }

        public BattleScenarioRunResult Run(TestScenario scenario)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            var backend = MobaPredictionBackendIds.Normalize(
                (scenario.Expectations as MobaBattleScenarioAssertions)?.PredictionBackend ??
                MobaPredictionBackendIds.Headless);
            if (backend == MobaPredictionBackendIds.UnityRoute)
                return RunUnityRoute(scenario);
            var tmpDir = Path.Combine(Path.GetTempPath(), "abilitykit-battlescenario");
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
                    return new BattleScenarioRunResult { Passed = false, Summary = "runner 退出码 " + process.ExitCode };
            }

            if (!File.Exists(resultPath))
                return new BattleScenarioRunResult { Passed = false, Summary = "runner 未产出结果文件" };

            var lines = File.ReadAllLines(resultPath);
            var passed = lines.Length > 0 && lines[0] == "PASSED";
            var summary = lines.Length > 1 ? string.Join("\n", lines, 1, lines.Length - 1) : string.Empty;
            return new BattleScenarioRunResult { Passed = passed, Summary = summary, Trace = ReadTrace(resultPath) };
        }

        public string RunDirectory(string directory)
        {
            // Keep Unity-only cases in this process, including mixed-backend batches.
            var files = Directory.GetFiles(directory, "*.battlescenario");
            Array.Sort(files, StringComparer.Ordinal);
            var scenarios = new List<TestScenario>();
            var loadFailures = new List<string>();
            var requiresUnity = false;
            foreach (var file in files)
            {
                try
                {
                    var scenario = BattleScenarioCompiler.CompileFile(file);
                    scenarios.Add(scenario);
                    requiresUnity |= (scenario.Expectations as MobaBattleScenarioAssertions)?.PredictionBackend
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
            var tmpDir = Path.Combine(Path.GetTempPath(), "abilitykit-battlescenario");
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
        private static BattleScenarioRunResult RunUnityRoute(TestScenario scenario)
        {
            var assertions = scenario.Expectations as MobaBattleScenarioAssertions;
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
            var observation = MobaBattleScenarioPredictionRunner.Run(
                scenario, new UnityRouteMobaPredictionScenarioBackend());
            var verdict = MobaBattleScenarioPredictionRunner.Verify(
                assertions?.Prediction, observation);
            return new BattleScenarioRunResult
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
            const string relativeProjectPath = "src/AbilityKit.Demo.Moba.BattleScenario.Runner/AbilityKit.Demo.Moba.BattleScenario.Runner.csproj";
            for (var directory = new DirectoryInfo(repoRoot); directory != null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, relativeProjectPath);
                if (File.Exists(candidate)) return candidate;
            }
            return Path.Combine(repoRoot, relativeProjectPath);
        }

        /// <summary>读取 .NET runner 顺带回传的 trace 树 JSON（缺失/损坏不致命）。</summary>
        private static List<BattleScenarioTraceNode> ReadTrace(string resultPath)
        {
            var tracePath = resultPath + ".trace.json";
            if (!File.Exists(tracePath)) return new List<BattleScenarioTraceNode>();
            try
            {
                return JsonConvert.DeserializeObject<List<BattleScenarioTraceNode>>(File.ReadAllText(tracePath))
                       ?? new List<BattleScenarioTraceNode>();
            }
            catch
            {
                return new List<BattleScenarioTraceNode>();
            }
        }
    }
}
#endif
