using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AbilityKit.Benchmarking;

namespace AbilityKit.Runtime.Benchmarks.Compare;

/// <summary>
/// 性能回归基线比对器。读 golden baseline 与当前 BenchmarkReport，按 descriptor.id 配对，
/// 对选定时间指标 + 分配指标按阈值判 drift，产出 verdict + JSON 报告 + CI 退出码。
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var opts = ParseArgs(args);
        if (opts is null) return 2;

        if (!File.Exists(opts.BaselinePath)) { Console.Error.WriteLine($"baseline 不存在：{opts.BaselinePath}"); return 2; }
        if (!File.Exists(opts.CurrentPath)) { Console.Error.WriteLine($"current 不存在：{opts.CurrentPath}"); return 2; }

        var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var baseline = JsonSerializer.Deserialize<BenchmarkReport>(File.ReadAllText(opts.BaselinePath), jsonOpts);
        var current = JsonSerializer.Deserialize<BenchmarkReport>(File.ReadAllText(opts.CurrentPath), jsonOpts);
        if (baseline is null || current is null) { Console.Error.WriteLine("无法解析 BenchmarkReport"); return 2; }

        var result = Compare(baseline, current, opts);

        PrintConsole(result, opts);
        if (opts.OutPath is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(opts.OutPath))!);
            File.WriteAllText(opts.OutPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"报告已写入：{opts.OutPath}");
        }

        return result.Verdict == "PASS" ? 0 : 1;
    }

    private static CompareReport Compare(BenchmarkReport baseline, BenchmarkReport current, Options opts)
    {
        var baseById = baseline.Results.ToDictionary(r => r.Descriptor.Id);
        var curById = current.Results.ToDictionary(r => r.Descriptor.Id);

        var cases = new List<CompareCase>();
        foreach (var id in curById.Keys.OrderBy(k => k))
        {
            if (!baseById.TryGetValue(id, out var b))
            {
                cases.Add(new CompareCase(id, Module: curById[id].Descriptor.Module, Status: "new",
                    TimeBaseline: null, TimeCurrent: Metric(curById[id].Summary, opts.Metric),
                    TimeDeltaPct: null, AllocBaseline: null, AllocCurrent: curById[id].Summary.MeanAllocatedBytesPerOperation,
                    AllocDeltaPct: null, DigestChanged: false));
                continue;
            }
            var c = curById[id];
            var bt = Metric(b.Summary, opts.Metric);
            var ct = Metric(c.Summary, opts.Metric);
            var ba = b.Summary.MeanAllocatedBytesPerOperation;
            var ca = c.Summary.MeanAllocatedBytesPerOperation;
            double? tDPct = bt > 0 ? (ct - bt) / bt * 100.0 : null;
            double? aDPct = ba > 0 ? (ca - ba) / ba * 100.0 : null;

            bool timeRegress = tDPct is { } tp && tp > opts.TimeThresholdPct;
            bool allocRegress = aDPct is { } ap && ap > opts.AllocThresholdPct;
            bool digestChanged = !string.IsNullOrEmpty(b.DeterminismDigest)
                                 && b.DeterminismDigest != c.DeterminismDigest;

            string status;
            if (timeRegress || allocRegress) status = "regression";
            else if (opts.StrictDigest && digestChanged) status = "digest-drift";
            else status = "ok";

            cases.Add(new CompareCase(id, c.Descriptor.Module, status, bt, ct, tDPct, ba, ca, aDPct, digestChanged));
        }

        var missing = baseById.Keys.Where(k => !curById.ContainsKey(k)).OrderBy(k => k).ToList();
        var regressions = cases.Where(x => x.Status == "regression").ToList();
        var verdict = regressions.Count == 0 && !(opts.StrictDigest && cases.Any(x => x.DigestChanged))
            ? "PASS" : "FAIL";

        return new CompareReport(
            Schema: "abilitykit.perf-baseline-compare.v1",
            BaselineCommit: baseline.Environment.Commit,
            CurrentCommit: current.Environment.Commit,
            Metric: opts.Metric,
            TimeThresholdPct: opts.TimeThresholdPct,
            AllocThresholdPct: opts.AllocThresholdPct,
            ComparedCaseCount: cases.Count,
            RegressionCount: regressions.Count,
            NewCaseCount: cases.Count(x => x.Status == "new"),
            MissingCaseCount: missing.Count,
            Verdict: verdict,
            Cases: cases,
            MissingCaseIds: missing);
    }

    private static double Metric(BenchmarkSummary s, string metric) => metric switch
    {
        "mean" => s.MeanNanosecondsPerOperation,
        "median" => s.MedianNanosecondsPerOperation,
        "p95" => s.P95NanosecondsPerOperation,
        "p99" => s.P99NanosecondsPerOperation,
        "max" => s.MaxNanosecondsPerOperation,
        _ => s.MedianNanosecondsPerOperation,
    };

    private static void PrintConsole(CompareReport r, Options opts)
    {
        Console.WriteLine($"=== Perf 基线比对：{r.Verdict} ===");
        Console.WriteLine($"  metric={r.Metric}  时间阈值+{r.TimeThresholdPct}%  分配阈值+{r.AllocThresholdPct}%");
        Console.WriteLine($"  对比 {r.ComparedCaseCount} 项 | 回归 {r.RegressionCount} | 新增 {r.NewCaseCount} | 缺失 {r.MissingCaseCount}");
        Console.WriteLine($"  baseline commit={r.BaselineCommit}  current commit={r.CurrentCommit}");

        foreach (var c in r.Cases.Where(x => x.Status == "regression").OrderByDescending(x => x.TimeDeltaPct ?? 0))
        {
            var parts = new List<string> { $"时间 {c.TimeDeltaPct:+0.0;-0.0;0.0}%" };
            if (c.AllocDeltaPct is { } ap) parts.Add($"分配 {ap:+0.0;-0.0;0.0}%");
            Console.WriteLine($"  ⚠ REGRESS  {c.Id}  [{string.Join(", ", parts)}]");
        }
        if (r.MissingCaseIds.Count > 0)
            Console.WriteLine($"  缺失(基线有/当前无)：{string.Join(", ", r.MissingCaseIds)}");
    }

    private sealed record Options(
        string BaselinePath, string CurrentPath, double TimeThresholdPct,
        double AllocThresholdPct, string Metric, bool StrictDigest, string? OutPath);

    private static Options? ParseArgs(string[] args)
    {
        string? baseline = null, current = null, metric = "median", outPath = null;
        double timeThr = 15.0, allocThr = 20.0;
        bool strictDigest = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--baseline": baseline = ArgVal(args, ref i); break;
                case "--current": current = ArgVal(args, ref i); break;
                case "--metric": metric = ArgVal(args, ref i); break;
                case "--out": outPath = ArgVal(args, ref i); break;
                case "--threshold-pct": timeThr = double.Parse(ArgVal(args, ref i)); break;
                case "--alloc-threshold-pct": allocThr = double.Parse(ArgVal(args, ref i)); break;
                case "--strict-digest": strictDigest = true; break;
                case "-h":
                case "--help":
                    Console.WriteLine("用法: --baseline <path> --current <path> [--metric mean|median|p95|p99|max] [--threshold-pct 15] [--alloc-threshold-pct 20] [--strict-digest] [--out <path>]");
                    return null;
            }
        }
        if (baseline is null || current is null)
        {
            Console.Error.WriteLine("必须提供 --baseline 与 --current。--help 查看用法。");
            return null;
        }
        return new Options(baseline, current, timeThr, allocThr, metric, strictDigest, outPath);
    }

    private static string ArgVal(string[] args, ref int i)
    {
        i++;
        return i < args.Length ? args[i] : string.Empty;
    }
}

public sealed record CompareReport(
    string Schema,
    string BaselineCommit,
    string CurrentCommit,
    string Metric,
    double TimeThresholdPct,
    double AllocThresholdPct,
    int ComparedCaseCount,
    int RegressionCount,
    int NewCaseCount,
    int MissingCaseCount,
    string Verdict,
    IReadOnlyList<CompareCase> Cases,
    IReadOnlyList<string> MissingCaseIds);

public sealed record CompareCase(
    string Id,
    string Module,
    string Status,           // ok | regression | new | digest-drift
    double? TimeBaseline,
    double? TimeCurrent,
    double? TimeDeltaPct,
    double? AllocBaseline,
    double? AllocCurrent,
    double? AllocDeltaPct,
    bool DigestChanged);
