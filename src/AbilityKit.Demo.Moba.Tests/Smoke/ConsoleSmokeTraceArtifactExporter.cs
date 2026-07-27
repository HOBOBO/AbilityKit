using System.Text.Json;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.AutoTest;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public enum ConsoleSmokeArtifactRetentionPolicy
{
    Never = 0,
    OnFailure = 1,
    Always = 2
}

public sealed class ConsoleSmokeTraceArtifactOptions
{
    public const string PolicyEnvironmentVariable = "ABILITYKIT_MOBA_CONSOLE_SMOKE_ARTIFACTS";
    public const string DirectoryEnvironmentVariable = "ABILITYKIT_MOBA_CONSOLE_SMOKE_ARTIFACT_DIR";
    public const string DefaultArtifactDirectory = "local/Logs/moba-console-smoke";

    public ConsoleSmokeArtifactRetentionPolicy RetentionPolicy { get; init; } = ConsoleSmokeArtifactRetentionPolicy.OnFailure;
    public string ArtifactDirectory { get; init; } = DefaultArtifactDirectory;

    public static ConsoleSmokeTraceArtifactOptions FromEnvironment()
    {
        return new ConsoleSmokeTraceArtifactOptions
        {
            RetentionPolicy = ParsePolicy(Environment.GetEnvironmentVariable(PolicyEnvironmentVariable)),
            ArtifactDirectory = NormalizeDirectory(Environment.GetEnvironmentVariable(DirectoryEnvironmentVariable))
        };
    }

    public static ConsoleSmokeTraceArtifactOptions Always(string artifactDirectory)
    {
        return new ConsoleSmokeTraceArtifactOptions
        {
            RetentionPolicy = ConsoleSmokeArtifactRetentionPolicy.Always,
            ArtifactDirectory = NormalizeDirectory(artifactDirectory)
        };
    }

    private static ConsoleSmokeArtifactRetentionPolicy ParsePolicy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return ConsoleSmokeArtifactRetentionPolicy.OnFailure;

        return value.Trim().ToLowerInvariant() switch
        {
            "never" or "none" or "off" or "false" or "0" => ConsoleSmokeArtifactRetentionPolicy.Never,
            "always" or "all" or "on" or "true" or "1" => ConsoleSmokeArtifactRetentionPolicy.Always,
            "onfailure" or "on-failure" or "failure" or "fail" or "failed" => ConsoleSmokeArtifactRetentionPolicy.OnFailure,
            _ => ConsoleSmokeArtifactRetentionPolicy.OnFailure
        };
    }

    private static string NormalizeDirectory(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DefaultArtifactDirectory : value.Trim();
    }
}

public sealed class ConsoleSmokeTraceArtifact
{
    public ConsoleSmokeTraceArtifact(string caseId, string artifactDirectory, string traceJsonlPath, string summaryJsonPath, string batchSummaryJsonPath, int traceNodeCount)
    {
        CaseId = caseId;
        ArtifactDirectory = artifactDirectory;
        TraceJsonlPath = traceJsonlPath;
        SummaryJsonPath = summaryJsonPath;
        BatchSummaryJsonPath = batchSummaryJsonPath;
        TraceNodeCount = traceNodeCount;
    }

    public string CaseId { get; }
    public string ArtifactDirectory { get; }
    public string TraceJsonlPath { get; }
    public string SummaryJsonPath { get; }
    public string BatchSummaryJsonPath { get; }
    public int TraceNodeCount { get; }
}

public static class ConsoleSmokeTraceArtifactExporter
{
    private const string BatchSummaryFileName = "batch_summary.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions JsonLineOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static bool ShouldRetain(ConsoleSmokeArtifactRetentionPolicy policy, bool passed)
    {
        return policy switch
        {
            ConsoleSmokeArtifactRetentionPolicy.Always => true,
            ConsoleSmokeArtifactRetentionPolicy.OnFailure => !passed,
            _ => false
        };
    }

    public static ConsoleSmokeTraceArtifact? Export(ConsoleBattleBootstrapper bootstrapper, AutoTestResult autoTestResult, ConsoleSmokeTraceArtifactOptions options)
    {
        if (bootstrapper == null) throw new ArgumentNullException(nameof(bootstrapper));
        if (autoTestResult == null) throw new ArgumentNullException(nameof(autoTestResult));
        if (options == null) throw new ArgumentNullException(nameof(options));

        var passed = IsAutoTestPassed(autoTestResult);
        if (!ShouldRetain(options.RetentionPolicy, passed)) return null;

        if (bootstrapper.RuntimeServices == null || !bootstrapper.RuntimeServices.TryResolve<MobaTraceRegistry>(out var trace) || trace == null)
        {
            return null;
        }

        var caseId = BuildCaseId(autoTestResult);
        var artifactDirectory = NormalizePath(options.ArtifactDirectory);
        Directory.CreateDirectory(artifactDirectory);

        var tracePath = Path.Combine(artifactDirectory, caseId + "_trace.jsonl");
        var summaryPath = Path.Combine(artifactDirectory, caseId + "_summary.json");
        var batchPath = Path.Combine(artifactDirectory, BatchSummaryFileName);
        var records = CaptureTraceRecords(trace, autoTestResult, caseId, bootstrapper.Context.LastFrame);
        var summary = BuildSummary(bootstrapper, autoTestResult, options, caseId, passed, records, tracePath, summaryPath);

        using (var writer = new StreamWriter(tracePath, false))
        {
            foreach (var record in records)
            {
                writer.WriteLine(JsonSerializer.Serialize(record, JsonLineOptions));
            }
        }

        File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, JsonOptions));
        WriteBatchSummary(artifactDirectory, batchPath);

        return new ConsoleSmokeTraceArtifact(
            caseId,
            NormalizePath(artifactDirectory),
            NormalizePath(tracePath),
            NormalizePath(summaryPath),
            NormalizePath(batchPath),
            records.Count);
    }

    private static List<ConsoleSmokeTraceRecordDto> CaptureTraceRecords(MobaTraceRegistry trace, AutoTestResult autoTestResult, string caseId, int frame)
    {
        var records = new List<ConsoleSmokeTraceRecordDto>(64);
        var seen = new HashSet<long>();
        var script = autoTestResult.ScriptResult;

        foreach (MobaTraceKind kind in Enum.GetValues(typeof(MobaTraceKind)))
        {
            if (kind == MobaTraceKind.None) continue;

            foreach (var node in trace.GetNodesByKind((int)kind))
            {
                if (!node.IsValid || !seen.Add(node.ContextId)) continue;

                var metadata = node.Metadata;
                records.Add(new ConsoleSmokeTraceRecordDto
                {
                    CaseId = caseId,
                    Frame = frame,
                    TimeMs = script?.TickCount ?? 0,
                    RootId = node.RootId,
                    ParentId = node.ParentId,
                    NodeId = node.ContextId,
                    Kind = ((MobaTraceKind)node.Kind).ToString(),
                    KindValue = node.Kind,
                    ConfigId = metadata?.ConfigId ?? 0,
                    SourceActorId = metadata?.SourceActorId ?? 0,
                    TargetActorId = metadata?.TargetActorId ?? 0,
                    SourceId = metadata?.SourceId ?? 0,
                    TargetId = metadata?.TargetId ?? 0,
                    OriginSourceId = metadata?.OriginSourceId ?? 0,
                    OriginTargetId = metadata?.OriginTargetId ?? 0,
                    OriginSource = metadata?.OriginSource,
                    OriginTarget = metadata?.OriginTarget,
                    IsRoot = node.IsRoot,
                    IsEnded = node.IsEnded,
                    EndedFrame = node.EndedFrame,
                    EndReason = node.EndReason,
                    ChildCount = node.ChildCount
                });
            }
        }

        records.Sort((x, y) =>
        {
            var rootCompare = x.RootId.CompareTo(y.RootId);
            return rootCompare != 0 ? rootCompare : x.NodeId.CompareTo(y.NodeId);
        });

        return records;
    }

    private static ConsoleSmokeSummaryDto BuildSummary(
        ConsoleBattleBootstrapper bootstrapper,
        AutoTestResult autoTestResult,
        ConsoleSmokeTraceArtifactOptions options,
        string caseId,
        bool passed,
        IReadOnlyList<ConsoleSmokeTraceRecordDto> records,
        string tracePath,
        string summaryPath)
    {
        var script = autoTestResult.ScriptResult;
        var plan = bootstrapper.Context.Plan;

        return new ConsoleSmokeSummaryDto
        {
            CaseId = caseId,
            Description = "Console MOBA smoke trace artifact",
            WorldId = plan.WorldId,
            TickRate = plan.TickRate,
            Accelerated = true,
            Scenario = new ConsoleSmokeScenarioDto
            {
                Name = script?.ScriptName ?? string.Empty,
                Description = "Console entry smoke scenario generated by xUnit AutoTestRunner",
                StepCount = script?.StepCount ?? 0,
                TickCount = script?.TickCount ?? 0
            },
            Result = new ConsoleSmokeResultDto
            {
                Passed = passed,
                SkillCastTraceFound = records.Any(r => r.Kind == MobaTraceKind.SkillCast.ToString()),
                EffectExecutionTraceFound = records.Any(r => r.Kind == MobaTraceKind.EffectExecution.ToString()),
                AllExpectedActionsExecuted = true,
                ProjectileLaunched = records.Any(r => r.Kind == MobaTraceKind.ProjectileLaunch.ToString()),
                EffectRootId = records.FirstOrDefault(r => r.Kind == MobaTraceKind.EffectExecution.ToString())?.RootId ?? 0,
                FinalFrame = bootstrapper.Context.LastFrame,
                FinalTimeMs = script?.TickCount ?? 0,
                TraceNodeCount = records.Count,
                ErrorMessage = BuildErrorMessage(autoTestResult)
            },
            TraceCounts = records
                .GroupBy(r => r.Kind ?? string.Empty)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new ConsoleSmokeTraceCountDto { Kind = g.Key, Count = g.Count() })
                .ToArray(),
            Retention = new ConsoleSmokeRetentionDto
            {
                Policy = options.RetentionPolicy.ToString(),
                ExportedAtUtc = DateTime.UtcNow,
                Trigger = passed ? "passed" : "failed"
            },
            TraceJsonlPath = NormalizePath(tracePath),
            SummaryJsonPath = NormalizePath(summaryPath)
        };
    }

    private static void WriteBatchSummary(string artifactDirectory, string batchPath)
    {
        var cases = Directory.EnumerateFiles(artifactDirectory, "*_summary.json", SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(Path.GetFileName(path), BatchSummaryFileName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new ConsoleSmokeBatchCaseDto
            {
                CaseId = Path.GetFileName(path).Replace("_summary.json", string.Empty, StringComparison.OrdinalIgnoreCase),
                SummaryJsonPath = NormalizePath(path),
                TraceJsonlPath = NormalizePath(Path.Combine(artifactDirectory, Path.GetFileName(path).Replace("_summary.json", "_trace.jsonl", StringComparison.OrdinalIgnoreCase)))
            })
            .ToArray();

        var batch = new ConsoleSmokeBatchSummaryDto
        {
            ArtifactKind = "moba-console-smoke",
            GeneratedAtUtc = DateTime.UtcNow,
            CaseCount = cases.Length,
            Cases = cases
        };

        File.WriteAllText(batchPath, JsonSerializer.Serialize(batch, JsonOptions));
    }

    private static bool IsAutoTestPassed(AutoTestResult result)
    {
        return !result.HasUnexpectedError
            && result.ScriptResult?.Completed == true
            && result.InitTest?.Passed == true
            && result.PhaseTest?.Passed == true;
    }

    private static string BuildCaseId(AutoTestResult autoTestResult)
    {
        var scriptName = SanitizeCaseId(autoTestResult.ScriptResult?.ScriptName ?? "unknown");
        var ticks = autoTestResult.StartTime.ToUniversalTime().Ticks;
        return "console_smoke_" + scriptName + "_" + ticks;
    }

    private static string BuildErrorMessage(AutoTestResult result)
    {
        if (result.HasUnexpectedError) return result.ErrorMessage ?? string.Empty;
        if (result.ScriptResult?.Completed == false) return result.ScriptResult.ErrorMessage;
        if (result.InitTest?.Passed == false) return result.InitTest.FailReason;
        if (result.PhaseTest?.Passed == false) return result.PhaseTest.FailReason;
        return string.Empty;
    }

    private static string SanitizeCaseId(string value)
    {
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? char.ToLowerInvariant(ch) : '_').ToArray();
        var sanitized = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private sealed class ConsoleSmokeTraceRecordDto
    {
        public string CaseId { get; set; } = string.Empty;
        public int Frame { get; set; }
        public int TimeMs { get; set; }
        public long RootId { get; set; }
        public long ParentId { get; set; }
        public long NodeId { get; set; }
        public string Kind { get; set; } = string.Empty;
        public int KindValue { get; set; }
        public int ConfigId { get; set; }
        public long SourceActorId { get; set; }
        public long TargetActorId { get; set; }
        public long SourceId { get; set; }
        public long TargetId { get; set; }
        public long OriginSourceId { get; set; }
        public long OriginTargetId { get; set; }
        public string? OriginSource { get; set; }
        public string? OriginTarget { get; set; }
        public bool IsRoot { get; set; }
        public bool IsEnded { get; set; }
        public int EndedFrame { get; set; }
        public int EndReason { get; set; }
        public int ChildCount { get; set; }
    }

    private sealed class ConsoleSmokeSummaryDto
    {
        public string CaseId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string WorldId { get; set; } = string.Empty;
        public int TickRate { get; set; }
        public bool Accelerated { get; set; }
        public ConsoleSmokeScenarioDto Scenario { get; set; } = new();
        public ConsoleSmokeResultDto Result { get; set; } = new();
        public ConsoleSmokeTraceCountDto[] TraceCounts { get; set; } = Array.Empty<ConsoleSmokeTraceCountDto>();
        public ConsoleSmokeRetentionDto Retention { get; set; } = new();
        public string TraceJsonlPath { get; set; } = string.Empty;
        public string SummaryJsonPath { get; set; } = string.Empty;
    }

    private sealed class ConsoleSmokeScenarioDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int StepCount { get; set; }
        public int TickCount { get; set; }
    }

    private sealed class ConsoleSmokeResultDto
    {
        public bool Passed { get; set; }
        public bool SkillCastTraceFound { get; set; }
        public bool EffectExecutionTraceFound { get; set; }
        public bool AllExpectedActionsExecuted { get; set; }
        public bool ProjectileLaunched { get; set; }
        public long EffectRootId { get; set; }
        public int FinalFrame { get; set; }
        public int FinalTimeMs { get; set; }
        public int TraceNodeCount { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    private sealed class ConsoleSmokeTraceCountDto
    {
        public string Kind { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    private sealed class ConsoleSmokeRetentionDto
    {
        public string Policy { get; set; } = string.Empty;
        public DateTime ExportedAtUtc { get; set; }
        public string Trigger { get; set; } = string.Empty;
    }

    private sealed class ConsoleSmokeBatchSummaryDto
    {
        public string ArtifactKind { get; set; } = string.Empty;
        public DateTime GeneratedAtUtc { get; set; }
        public int CaseCount { get; set; }
        public ConsoleSmokeBatchCaseDto[] Cases { get; set; } = Array.Empty<ConsoleSmokeBatchCaseDto>();
    }

    private sealed class ConsoleSmokeBatchCaseDto
    {
        public string CaseId { get; set; } = string.Empty;
        public string SummaryJsonPath { get; set; } = string.Empty;
        public string TraceJsonlPath { get; set; } = string.Empty;
    }
}
