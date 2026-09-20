using System;
using System.IO;
using AbilityKit.BattleFlow;
using AbilityKit.Demo.Moba.BattleFlow;
using AbilityKit.Scenario;
using Newtonsoft.Json;

namespace AbilityKit.Demo.Moba.BattleFlow.Runner;

/// <summary>
/// headless 命令行入口：读一个场景 JSON → 跑 MOBA 世界执行 → 写/打印中性结果。
/// 编辑器「运行」按钮 shell-out 调用它（`dotnet run --project ... -- <scenario.json> [result.json]`）。
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            System.Console.Error.WriteLine(
                "Usage: <scenario.json|flow.battleflow> [result.txt] | " +
                "--determinism <scenario.json|flow.battleflow> [result.txt] | " +
                "--batch <battleflow-directory> [result.txt]");
            return 1;
        }

        if (args[0] == "--batch")
        {
            if (args.Length < 2)
            {
                System.Console.Error.WriteLine("--batch 需要目录参数");
                return 1;
            }

            try
            {
                var batch = BattleFlowBatchRunner.RunDirectory(args[1]);
                var output = $"total={batch.Total} passed={batch.Passed} failed={batch.Failed}";
                foreach (var c in batch.Cases)
                    output += $"\n  [{(c.Passed ? "PASS" : "FAIL")}] {c.CaseId}: " +
                              $"networkTrace={c.NetworkTraceCount}, {c.Summary}";
                System.Console.WriteLine(output);
                if (args.Length > 2) File.WriteAllText(args[2], output);
                return batch.Failed == 0 ? 0 : 2;
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine("批量运行失败: " + ex);
                return 2;
            }
        }

        if (args[0] == "--determinism")
        {
            if (args.Length < 2)
            {
                System.Console.Error.WriteLine("--determinism requires a scenario JSON or .battleflow path.");
                return 1;
            }

            try
            {
                var scenario = LoadScenario(args[1]);
                var verification = MobaBattleFlowScenarioRunner.VerifyDeterminism(scenario);
                var output =
                    $"{(verification.Matches ? "DETERMINISTIC" : "NON-DETERMINISTIC")}\n" +
                    $"first={verification.First.DeterminismFingerprint}\n" +
                    $"second={verification.Second.DeterminismFingerprint}\n" +
                    verification.First.Result.Summary;
                System.Console.WriteLine(output);
                if (args.Length > 2)
                {
                    File.WriteAllText(args[2], output);
                    WriteArtifacts(args[2], verification.First);
                }

                return verification.Matches ? 0 : 3;
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine("Determinism verification failed: " + ex);
                return 2;
            }
        }

        try
        {
            var scenario = LoadScenario(args[0]);
            var outcome = MobaBattleFlowScenarioRunner.RunDetailed(scenario);
            var result = outcome.Result;
            var output = (result.Passed ? "PASSED" : "FAILED") + "\n" + result.Summary;
            if (args.Length > 1)
            {
                File.WriteAllText(args[1], output);
                WriteArtifacts(args[1], outcome);
            }
            else
            {
                System.Console.WriteLine(output);
            }
            return result.Passed ? 0 : 3;
        }
        catch (Exception ex)
        {
            System.Console.Error.WriteLine("运行失败: " + ex);
            return 2;
        }
    }

    private static void WriteArtifacts(string resultPath, MobaBattleFlowRunOutcome outcome)
    {
        File.WriteAllText(
            resultPath + ".trace.json",
            JsonConvert.SerializeObject(outcome.TraceNodes, Formatting.Indented));
        File.WriteAllText(
            resultPath + ".network.json",
            JsonConvert.SerializeObject(outcome.NetworkTrace, Formatting.Indented));
    }

    private static TestScenario LoadScenario(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".battleflow", StringComparison.OrdinalIgnoreCase))
            return ScenarioCodec.Load(path);

        return BattleFlowCompiler.CompileFile(path);
    }
}
