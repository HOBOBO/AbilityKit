using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.BattleScenario;

namespace AbilityKit.Demo.Moba.BattleScenario;

/// <summary>批量运行结果。</summary>
public sealed class BattleScenarioBatchResult
{
    public int Total { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public List<BattleScenarioCaseResult> Cases { get; set; } = new List<BattleScenarioCaseResult>();
}

/// <summary>单个流程的批量运行结果。</summary>
public sealed class BattleScenarioCaseResult
{
    public string CaseId { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string DeterminismFingerprint { get; set; } = string.Empty;
    public int NetworkTraceCount { get; set; }
}

/// <summary>批量运行一个目录下的 .battlescenario：逐个 加载 → 编译 → headless 跑 → 汇总。</summary>
public static class BattleScenarioBatchRunner
{
    public static BattleScenarioBatchResult RunDirectory(string directory)
    {
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException($"流程目录不存在: {directory}");

        var result = new BattleScenarioBatchResult();
        var files = Directory.GetFiles(directory, "*.battlescenario")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        foreach (var file in files)
        {
            try
            {
                var doc = BattleScenarioCodec.Load(file);
                var scenario = BattleScenarioCompiler.CompileFile(file);
                var outcome = MobaBattleScenarioWorldRunner.RunDetailed(scenario);
                result.Cases.Add(new BattleScenarioCaseResult
                {
                    CaseId = doc.CaseId,
                    Passed = outcome.Result.Passed,
                    Summary = outcome.Result.Summary,
                    DeterminismFingerprint = outcome.DeterminismFingerprint,
                    NetworkTraceCount = outcome.NetworkTrace.Length,
                });
            }
            catch (Exception ex)
            {
                result.Cases.Add(new BattleScenarioCaseResult { CaseId = Path.GetFileNameWithoutExtension(file), Passed = false, Summary = "加载/运行失败: " + ex.Message });
            }
        }

        result.Total = result.Cases.Count;
        result.Passed = result.Cases.Count(c => c.Passed);
        result.Failed = result.Total - result.Passed;
        return result;
    }
}
