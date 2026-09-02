using System;
using System.IO;
using AbilityKit.Demo.Moba.BattleFlow;
using AbilityKit.Scenario;

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
            System.Console.Error.WriteLine("用法: <scenario.json> [result.json]");
            return 1;
        }

        try
        {
            var scenario = ScenarioCodec.Load(args[0]);
            var result = MobaBattleFlowScenarioRunner.Run(scenario);
            var output = (result.Passed ? "PASSED" : "FAILED") + "\n" + result.Summary;
            if (args.Length > 1) File.WriteAllText(args[1], output);
            else System.Console.WriteLine(output);
            return 0;
        }
        catch (Exception ex)
        {
            System.Console.Error.WriteLine("运行失败: " + ex);
            return 2;
        }
    }
}
