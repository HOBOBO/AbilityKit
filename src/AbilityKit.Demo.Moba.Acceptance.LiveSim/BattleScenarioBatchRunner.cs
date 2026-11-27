using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.BattleScenario;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Scenario;

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
        => RunDirectory(directory, null, null, null);

    internal static BattleScenarioBatchResult RunDirectory(string directory, IReadOnlyCollection<string>? caseFiles,
        Action<BattleScenarioCaseRun>? publish, bool? captureEvidence)
    {
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException($"流程目录不存在: {directory}");

        var result = new BattleScenarioBatchResult();
        var files = Directory.GetFiles(directory, "*.battlescenario")
            .Where(path => caseFiles == null || caseFiles.Contains(Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (caseFiles != null && caseFiles.Any(name => !files.Any(path =>
                string.Equals(name, Path.GetFileNameWithoutExtension(path), StringComparison.OrdinalIgnoreCase))))
            throw new FileNotFoundException("有 --case 指定的 .battlescenario 文件不在输入目录中。");
        var caseIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var run = new BattleScenarioCaseRun { Path = file, CaseId = Path.GetFileNameWithoutExtension(file) };
            try
            {
                var doc = BattleScenarioCodec.Load(file);
                run.CaseId = doc.CaseId;
                if (publish != null && !caseIds.Add(doc.CaseId))
                    throw new InvalidDataException($"重复的场景 ID：{doc.CaseId}");
                run.Scenario = BattleScenarioCompiler.Compile(doc, reference =>
                    BattleScenarioCodec.LoadScene(BattleScenarioCodec.ResolveScenePath(file, reference)));
                run.Outcome = captureEvidence == true
                    ? MobaBattleScenarioWorldRunner.RunDetailed(run.Scenario, null,
                        snapshot => run.Snapshot = snapshot, error => run.EvidenceError = error.Message)
                    : MobaBattleScenarioWorldRunner.RunDetailed(run.Scenario);
            }
            catch (Exception ex)
            {
                run.Error = ex.Message;
            }

            var outcome = run.Outcome;
            result.Cases.Add(new BattleScenarioCaseResult
            {
                CaseId = outcome == null && publish == null ? Path.GetFileNameWithoutExtension(file) : run.CaseId,
                Passed = outcome?.Result.Passed ?? false,
                Summary = outcome?.Result.Summary ?? "加载/运行失败: " + run.Error,
                DeterminismFingerprint = outcome?.DeterminismFingerprint ?? string.Empty,
                NetworkTraceCount = outcome?.NetworkTrace.Length ?? 0,
            });
            publish?.Invoke(run);
        }

        result.Total = result.Cases.Count;
        result.Passed = result.Cases.Count(c => c.Passed);
        result.Failed = result.Total - result.Passed;
        return result;
    }
}

internal sealed class BattleScenarioCaseRun
{
    internal string Path = string.Empty;
    internal string CaseId = string.Empty;
    internal string? Error;
    internal string? EvidenceError;
    internal TestScenario? Scenario;
    internal MobaBattleScenarioRunOutcome? Outcome;
    internal BattleDiagnosticSessionSnapshot? Snapshot;
}
