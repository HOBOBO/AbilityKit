using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AbilityKit.Game.Test.UnitTest;

namespace AbilityKit.Demo.Moba.Acceptance;

/// <summary>
/// dotnet 验收批量 runner —— 镜像 <c>MobaAcceptanceRunner.RunExpectationDirectory</c>，但纯 dotnet：
/// 扫描期望目录，每用例经 <see cref="ITraceSource"/> 取观测 trace → <see cref="AcceptanceVerifier"/> 判定，
/// 汇总成生产同款 <see cref="MobaAcceptanceBatchSummary"/> 并落盘 batch_summary.json。
/// 这是「dsl-regression」门禁的核心：一个 dotnet-test step（按 Gate trait 过滤）即可在 CI 跑整套验收，
/// 不依赖 Unity、不依赖模拟器去 Unity 化（trace 由 <see cref="ITraceSource"/> 提供）。
/// </summary>
public static class AcceptanceBatchRunner
{
    /// <summary>
    /// 跑一个期望目录。
    /// </summary>
    /// <param name="expectationDirectory">含 *.expected.json 的目录。</param>
    /// <param name="artifactDirectory">产物输出目录（null 则不写盘，仅返回）。</param>
    /// <param name="traceSource">trace 来源（文件 / 未来 live-sim）。</param>
    /// <param name="exportArtifacts">是否写 per-case summary + batch_summary.json。</param>
    public static MobaAcceptanceBatchSummary RunDirectory(
        string expectationDirectory,
        string? artifactDirectory,
        ITraceSource traceSource,
        bool exportArtifacts = true)
    {
        if (!Directory.Exists(expectationDirectory))
            throw new DirectoryNotFoundException($"期望目录不存在：{expectationDirectory}");

        var batchSw = Stopwatch.StartNew();
        var startedUtc = DateTimeOffset.UtcNow.ToString("o");
        if (exportArtifacts && !string.IsNullOrEmpty(artifactDirectory))
            Directory.CreateDirectory(artifactDirectory);

        var files = Directory.GetFiles(expectationDirectory, "*.expected.json")
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();

        var results = new List<MobaAcceptanceCaseRunResult>(files.Length);
        foreach (var file in files)
        {
            results.Add(RunOne(file, artifactDirectory, traceSource, exportArtifacts));
        }

        batchSw.Stop();
        var passed = results.Count(r => r.passed && string.IsNullOrEmpty(r.errorType));
        var failed = results.Count(r => !r.passed && r.errorType != "needs-trace");
        var needsTrace = results.Count(r => r.errorType == "needs-trace");

        string? batchPath = null;
        var batch = new MobaAcceptanceBatchSummary
        {
            expectationDirectory = expectationDirectory,
            artifactDirectory = artifactDirectory,
            recursive = false,
            startedUtc = startedUtc,
            durationMs = batchSw.ElapsedMilliseconds,
            total = results.Count,
            passed = passed,
            failed = failed,
            allPassed = failed == 0,
            results = results.ToArray(),
            batchSummaryJsonPath = null,
        };

        if (exportArtifacts && !string.IsNullOrEmpty(artifactDirectory))
        {
            batchPath = Path.Combine(artifactDirectory, "batch_summary.json");
            batch.batchSummaryJsonPath = NormalizePath(batchPath);
            File.WriteAllText(batchPath, AcceptanceJsonCodec.Serialize(batch));
        }

        // needs-trace 是覆盖度信息，不计入 failed；控制台提示便于发现「还没接 trace 的用例」。
        if (needsTrace > 0)
            Console.WriteLine($"[AcceptanceBatchRunner] {needsTrace}/{results.Count} 用例缺 trace（needs-trace，未判定）。");
        Console.WriteLine($"[AcceptanceBatchRunner] total={results.Count} passed={passed} failed={failed} needsTrace={needsTrace} allPassed={batch.allPassed}");

        return batch;
    }

    private static MobaAcceptanceCaseRunResult RunOne(
        string expectationPath, string? artifactDirectory, ITraceSource traceSource, bool exportArtifacts)
    {
        var sw = Stopwatch.StartNew();
        var startedUtc = DateTimeOffset.UtcNow.ToString("o");
        string? errorType = null;
        string? errorMessage = null;
        MobaAcceptanceSummary? summary = null;
        bool passed;

        try
        {
            var expectation = AcceptanceJsonCodec.LoadExpectation(expectationPath);
            var caseId = string.IsNullOrEmpty(expectation.caseId) ? Path.GetFileNameWithoutExtension(expectationPath) : expectation.caseId;

            if (!traceSource.TryGetRecords(caseId, out var records))
            {
                passed = false;
                errorType = "needs-trace";
                errorMessage = $"无 trace 源提供 caseId={caseId} 的观测 trace";
            }
            else
            {
                var summaryPath = !string.IsNullOrEmpty(artifactDirectory)
                    ? Path.Combine(artifactDirectory, caseId + "_summary.json") : null;
                summary = AcceptanceVerifier.Verify(expectation, records, expectationPath: NormalizePath(expectationPath), summaryJsonPath: NormalizePath(summaryPath));
                passed = summary.result.passed;
                if (!passed)
                {
                    errorType = "verdict-failed";
                    errorMessage = summary.coverage.missingTraceNodes;
                }
                if (exportArtifacts && summaryPath != null)
                    AcceptanceJsonCodec.WriteSummary(summaryPath, summary);
            }
        }
        catch (Exception ex)
        {
            passed = false;
            errorType = "load-error";
            errorMessage = ex.GetType().Name + ": " + ex.Message;
        }

        sw.Stop();
        return new MobaAcceptanceCaseRunResult
        {
            caseId = summary?.caseId ?? Path.GetFileNameWithoutExtension(expectationPath),
            expectationPath = NormalizePath(expectationPath),
            passed = passed,
            errorType = errorType,
            errorMessage = errorMessage,
            startedUtc = startedUtc,
            durationMs = sw.ElapsedMilliseconds,
            summary = summary,
        };
    }

    private static string? NormalizePath(string? path) => string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
}
