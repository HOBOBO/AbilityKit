using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Test.UnitTest
{
    public static class MobaAcceptanceWebCommand
    {
        private const string ScenarioArgument = "-mobaAcceptanceScenario";
        private const string OutputArgument = "-mobaAcceptanceOutput";
        private const string ExpectationDirArgument = "-mobaAcceptanceExpectationDir";

        private static readonly Dictionary<string, string> ExpectationPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["lianpo-skill1-dash"] = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/Expectations/skill_10010101_scenario.expected.json",
            ["lianpo-skill2-area"] = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/Expectations/skill_10010201_scenario.expected.json",
            ["lianpo-skill3-combo"] = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/Expectations/skill_10010301_scenario.expected.json",
            ["xiaoqiao-skill1-projectile"] = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/Expectations/skill_10020101_scenario.expected.json",
            ["xiaoqiao-skill2-area"] = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/Expectations/skill_10020201_scenario.expected.json",
            ["xiaoqiao-skill3-ultimate"] = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/Expectations/skill_10020301_scenario.expected.json"
        };

        public static void RunFromCommandLine()
        {
            var scenarioId = GetArgumentValue(ScenarioArgument);
            var outputDirectory = GetArgumentValue(OutputArgument);
            try
            {
                if (string.IsNullOrWhiteSpace(scenarioId) || !ExpectationPaths.TryGetValue(scenarioId, out var expectationPath))
                {
                    throw new InvalidOperationException("Unknown Web acceptance scenario: " + scenarioId);
                }
                if (string.IsNullOrWhiteSpace(outputDirectory))
                {
                    throw new InvalidOperationException("Missing required output directory.");
                }

                var summary = MobaAcceptanceRunner.RunSkillExpectationFile(expectationPath, outputDirectory, exportArtifacts: true);
                if (summary == null || summary.result == null || !summary.result.passed)
                {
                    throw new InvalidOperationException("Acceptance DSL scenario did not pass: " + scenarioId);
                }

                Debug.Log("[MobaAcceptanceWebCommand] Completed DSL scenario " + scenarioId + " caseId=" + summary.caseId);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// 批量捕获入口：跑整个期望目录，把每个用例的 <c>&lt;caseId&gt;_trace.jsonl</c> 落盘到 <c>-mobaAcceptanceOutput</c>。
        /// 用于给 dotnet 验收判定层（<c>AbilityKit.Demo.Moba.Acceptance</c>）灌真实 trace 基线——
        /// 由 <c>tools/capture_moba_acceptance_traces.ps1</c> 经 <c>-executeMethod</c> 调起。
        /// 退出码：allPassed→0，否则→1；但无论 verdict 如何，已完成用例的 trace 都会落盘（供收集）。
        /// </summary>
        public static void RunDirectoryFromCommandLine()
        {
            var outputDirectory = GetArgumentValue(OutputArgument);
            var expectationDir = GetArgumentValue(ExpectationDirArgument);
            try
            {
                if (string.IsNullOrWhiteSpace(outputDirectory))
                {
                    throw new InvalidOperationException("Missing required output directory (-mobaAcceptanceOutput).");
                }
                if (string.IsNullOrWhiteSpace(expectationDir))
                {
                    expectationDir = MobaAcceptanceRunner.DefaultExpectationDirectory;
                }

                var batch = MobaAcceptanceRunner.RunExpectationDirectory(expectationDir, outputDirectory, exportArtifacts: true);
                var traceCount = Directory.Exists(outputDirectory)
                    ? Directory.GetFiles(outputDirectory, "*_trace.jsonl").Length
                    : 0;
                Debug.Log("[MobaAcceptanceWebCommand] Directory run total=" + batch.total
                    + " passed=" + batch.passed + " failed=" + batch.failed
                    + " allPassed=" + batch.allPassed + " tracesEmitted=" + traceCount
                    + " artifactDir=" + outputDirectory);
                EditorApplication.Exit(batch.allPassed ? 0 : 1);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static string GetArgumentValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) return args[index + 1];
            }

            return null;
        }
    }
}
