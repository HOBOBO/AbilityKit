using System;
using System.IO;
using AbilityKit.BattleScenario;
using AbilityKit.Demo.Moba.BattleScenario;
using AbilityKit.Scenario;
using Newtonsoft.Json;

namespace AbilityKit.Demo.Moba.BattleScenario.Runner;

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
                "Usage: <scenario.json|case.battlescenario> [result.txt] | " +
                "--determinism <scenario.json|case.battlescenario> [result.txt] | " +
                "--batch <battlescenario-directory> [result.txt] [--output-dir <directory> " +
                "[--evidence none|failed|all] [--case <filename-without-extension>]... [--receipt <file>]] | " +
                "--statesync-jinx-presentation <output-directory> | --statesync-annie-skill <output-directory>");
            return 1;
        }

        if (args[0] is "--statesync-jinx-presentation" or "--statesync-annie-skill")
        {
            if (args.Length != 2)
            {
                System.Console.Error.WriteLine($"{args[0]} requires an output directory.");
                return 1;
            }

            try
            {
                if (args[0] == "--statesync-jinx-presentation")
                {
                    JinxProjectilePresentationReplay.Export(args[1]);
                    System.Console.WriteLine("PASS: two local projectile presentation variants and repeatability.");
                }
                else
                {
                    AnnieSkillStateReplay.Export(args[1]);
                    System.Console.WriteLine("PASS: local Annie W/E skill confirmation and baseline replay.");
                }
                return 0;
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine("Local state-sync replay failed: " + ex);
                return 2;
            }
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
                if (!TryParseBatchOptions(args, out var resultPath, out var outputRoot, out var evidence, out var cases, out var receipt))
                {
                    System.Console.Error.WriteLine("批跑参数无效；--evidence、--case 和 --receipt 需要 --output-dir。");
                    return 1;
                }
                var batch = outputRoot == null ? BattleScenarioBatchRunner.RunDirectory(args[1])
                    : RunArchivedBatch(args[1], outputRoot, evidence, cases, receipt);
                var output = $"total={batch.Total} passed={batch.Passed} failed={batch.Failed}";
                foreach (var c in batch.Cases)
                    output += $"\n  [{(c.Passed ? "PASS" : "FAIL")}] {c.CaseId}: " +
                              $"networkTrace={c.NetworkTraceCount}, {c.Summary}";
                System.Console.WriteLine(output);
                if (resultPath != null) File.WriteAllText(resultPath, output);
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
                System.Console.Error.WriteLine("--determinism requires a scenario JSON or .battlescenario path.");
                return 1;
            }

            try
            {
                var scenario = LoadScenario(args[1]);
                var verification = MobaBattleScenarioWorldRunner.VerifyDeterminism(scenario);
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
            var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario);
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

    private static BattleScenarioBatchResult RunArchivedBatch(string input, string output, string mode, List<string> cases, string? receipt)
    {
        var result = BattleScenarioRunArchive.Run(input, output, cases.Count == 0 ? null : cases, mode, out var path);
        System.Console.WriteLine("Batch archive: " + path);
        if (receipt != null)
        {
            using var writer = new StreamWriter(new FileStream(receipt, FileMode.CreateNew));
            writer.WriteLine(Path.GetFileName(path));
        }
        return result;
    }

    private static bool TryParseBatchOptions(string[] args, out string? resultPath, out string? outputRoot,
        out string evidence, out List<string> cases, out string? receipt)
    {
        resultPath = null;
        outputRoot = null;
        evidence = "failed";
        cases = new List<string>();
        receipt = null;
        var hasEvidenceOption = false;
        var index = 2;
        if (index < args.Length && !args[index].StartsWith("--", StringComparison.Ordinal)) resultPath = args[index++];
        while (index < args.Length)
        {
            var option = args[index++];
            if (index == args.Length || string.IsNullOrWhiteSpace(args[index]) || args[index].StartsWith("--", StringComparison.Ordinal)) return false;
            var value = args[index++];
            switch (option)
            {
                case "--output-dir" when outputRoot == null: outputRoot = value; break;
                case "--evidence" when !hasEvidenceOption && value is "none" or "failed" or "all":
                    evidence = value; hasEvidenceOption = true; break;
                case "--case": cases.Add(value); break;
                case "--receipt" when receipt == null: receipt = value; break;
                default: return false;
            }
        }
        return outputRoot != null || (!hasEvidenceOption && cases.Count == 0 && receipt == null);
    }

    private static void WriteArtifacts(string resultPath, MobaBattleScenarioRunOutcome outcome)
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
        if (!string.Equals(Path.GetExtension(path), ".battlescenario", StringComparison.OrdinalIgnoreCase))
            return ScenarioCodec.Load(path);

        return BattleScenarioCompiler.CompileFile(path);
    }
}
