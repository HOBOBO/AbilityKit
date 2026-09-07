using System;
using System.IO;
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
            System.Console.Error.WriteLine("用法: <scenario.json> [result.json] | --batch <battleflow目录>");
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
                    output += $"\n  [{(c.Passed ? "PASS" : "FAIL")}] {c.CaseId}: {c.Summary}";
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

        try
        {
            var scenario = ScenarioCodec.Load(args[0]);
            var outcome = MobaBattleFlowScenarioRunner.RunDetailed(scenario);
            var result = outcome.Result;
            var output = (result.Passed ? "PASSED" : "FAILED") + "\n" + result.Summary;
            if (args.Length > 1)
            {
                File.WriteAllText(args[1], output);
                var tracePath = args[1] + ".trace.json";
                File.WriteAllText(tracePath, JsonConvert.SerializeObject(outcome.TraceNodes, Formatting.Indented));
            }
            else
            {
                System.Console.WriteLine(output);
            }
            return 0;
        }
        catch (Exception ex)
        {
            System.Console.Error.WriteLine("运行失败: " + ex);
            return 2;
        }
    }
}
