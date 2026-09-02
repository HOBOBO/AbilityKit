#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using AbilityKit.BattleFlow;
using AbilityKit.Scenario;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Demo.Moba.Editor.BattleFlow
{
    /// <summary>
    /// MOBA 的战斗流程运行器（项目扩展）：把编译出的 <see cref="TestScenario"/> shell-out 到 .NET headless 命令跑出 verdict + trace。
    /// 编辑器（Unity）不能进程内 boot console 世界，故序列化场景 → 写临时文件 → 调 .NET runner → 读结果文件。
    /// </summary>
    [InitializeOnLoad]
    public sealed class MobaBattleFlowRunner : IBattleFlowRunner
    {
        static MobaBattleFlowRunner()
        {
            BattleFlowRunnerRegistry.Runner = new MobaBattleFlowRunner();
        }

        public BattleFlowRunResult Run(TestScenario scenario)
        {
            var tmpDir = Path.Combine(Path.GetTempPath(), "abilitykit-battleflow");
            Directory.CreateDirectory(tmpDir);
            var scenarioPath = Path.Combine(tmpDir, "scenario-" + Guid.NewGuid().ToString("N") + ".json");
            var resultPath = Path.Combine(tmpDir, "result-" + Guid.NewGuid().ToString("N") + ".txt");
            ScenarioCodec.Save(scenarioPath, scenario);

            // 仓库根 = Unity 工程目录的上级（Application.dataPath 是 <repo>/Unity/Assets）。
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var runnerProject = Path.Combine(repoRoot, "src", "AbilityKit.Demo.Moba.BattleFlow.Runner", "AbilityKit.Demo.Moba.BattleFlow.Runner.csproj");

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --project \"" + runnerProject + "\" -- \"" + scenarioPath + "\" \"" + resultPath + "\"",
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
            return new BattleFlowRunResult { Passed = passed, Summary = summary };
        }
    }
}
#endif
