using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.BattleFlow;

namespace AbilityKit.Demo.Moba.BattleFlow;

/// <summary>批量运行结果。</summary>
public sealed class BattleFlowBatchResult
{
    public int Total { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public List<BattleFlowCaseResult> Cases { get; set; } = new List<BattleFlowCaseResult>();
}

/// <summary>单个流程的批量运行结果。</summary>
public sealed class BattleFlowCaseResult
{
    public string CaseId { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Summary { get; set; } = string.Empty;
}

/// <summary>批量运行一个目录下的 .battleflow：逐个 加载 → 编译 → headless 跑 → 汇总。</summary>
public static class BattleFlowBatchRunner
{
    public static BattleFlowBatchResult RunDirectory(string directory)
    {
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException($"流程目录不存在: {directory}");

        var result = new BattleFlowBatchResult();
        var files = Directory.GetFiles(directory, "*.battleflow");
        foreach (var file in files)
        {
            try
            {
                var doc = BattleFlowCodec.Load(file);
                var scenario = BattleFlowCompiler.Compile(doc.CaseId, doc.Blocks);
                var run = MobaBattleFlowScenarioRunner.Run(scenario);
                result.Cases.Add(new BattleFlowCaseResult { CaseId = doc.CaseId, Passed = run.Passed, Summary = run.Summary });
            }
            catch (Exception ex)
            {
                result.Cases.Add(new BattleFlowCaseResult { CaseId = Path.GetFileNameWithoutExtension(file), Passed = false, Summary = "加载/运行失败: " + ex.Message });
            }
        }

        result.Total = result.Cases.Count;
        result.Passed = result.Cases.Count(c => c.Passed);
        result.Failed = result.Total - result.Passed;
        return result;
    }
}
