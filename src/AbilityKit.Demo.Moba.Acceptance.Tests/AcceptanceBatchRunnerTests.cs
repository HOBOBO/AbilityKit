using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AbilityKit.Demo.Moba.Acceptance;
using AbilityKit.Game.Test.UnitTest;
using Xunit;

namespace AbilityKit.Demo.Moba.Acceptance.Tests;

/// <summary>
/// 验证 dotnet 批量 runner：用 FileTraceSource 跑真实期望目录，
/// 配一个合成 trace fixture，断言批量流程端到端可用。
/// [Trait("Gate","MobaAcceptanceDotnet")] —— 可直接被 dsl-regression 门禁的 dotnet-test step 跑。
/// </summary>
[Trait("Gate", "MobaAcceptanceDotnet")]
public class AcceptanceBatchRunnerTests
{
    private static readonly string ExpectationsDir = ResolveExpectationsDir();
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
    private static readonly string TracesDir = Path.Combine(AppContext.BaseDirectory, "Traces");

    // 真实 trace（Traces/，经 capture_moba_acceptance_traces.ps1 捕获）优先；合成 fixture（Fixtures/）兜底。
    private static ITraceSource Source => new CompositeTraceSource(
        new FileTraceSource(TracesDir),
        new FileTraceSource(FixtureDir));

    [Fact]
    public void Batch_runs_real_expectation_directory_with_composite_trace_source()
    {
        var artifactDir = CreateArtifactDirectory();

        var batch = AcceptanceBatchRunner.RunDirectory(ExpectationsDir, artifactDir, Source, exportArtifacts: true);

        // 跑了全部真实期望文件
        Assert.True(batch.total >= 10, $"至少 10 个期望用例，实际 {batch.total}");

        // 至少一个用例被判定（合成 fixture 兜底；真实 trace 落地后数量增长）且全过、无 failed。
        var judged = batch.results.Where(r => r.errorType != "needs-trace").ToList();
        Assert.True(judged.Count >= 1, "至少应有一个用例被判定");
        Assert.All(judged, r => Assert.True(r.passed, $"用例 {r.caseId} 应通过: {r.errorMessage}"));
        Assert.Equal(0, batch.failed);
        Assert.True(batch.allPassed);

        // 合成 fixture 覆盖的那个用例一定在其中并通过
        Assert.Contains(judged, r => r.caseId == "skill_10010101_scenario_dash_hit_damage_knockup");

        // 产物落盘
        Assert.True(File.Exists(Path.Combine(artifactDir, "batch_summary.json")));
    }

    [Fact]
    public void Needs_trace_cases_do_not_fail_the_batch()
    {
        // NullTraceSource：所有用例都 needs-trace，batch 仍 allPassed（覆盖度信息，非性质违反）
        var batch = AcceptanceBatchRunner.RunDirectory(ExpectationsDir, artifactDirectory: null, new NullTraceSource(), exportArtifacts: false);
        Assert.Equal(batch.total, batch.results.Count(r => r.errorType == "needs-trace"));
        Assert.Equal(0, batch.failed);
        Assert.True(batch.allPassed);
    }

    [Theory]
    [InlineData("paired", null)]
    [InlineData("legacy", "trace-incomplete")]
    [InlineData("tampered", "trace-incomplete")]
    [InlineData("truncated", "trace-incomplete")]
    [InlineData("missing", "trace-incomplete")]
    [InlineData("forbidden-retained", "verdict-failed")]
    public void Offline_reverse_assertions_require_paired_trace_evidence(string artifactState, string? errorType)
    {
        const string caseId = "offline-absence";
        var directory = CreateArtifactDirectory();
        var expectation = new MobaAcceptanceExpectation
        {
            caseId = caseId,
            mustNotContain = new[] { new MobaAcceptanceTraceExpectation { kind = "DamageApply", configId = 42 } },
        };
        File.WriteAllText(Path.Combine(directory, caseId + ".expected.json"), AcceptanceJsonCodec.Serialize(expectation));
        var tracePath = Path.Combine(directory, caseId + "_trace.jsonl");
        var jsonOptions = new JsonSerializerOptions(AcceptanceJsonCodec.Options) { WriteIndented = false };
        var traceLine = JsonSerializer.Serialize(new MobaAcceptanceTraceRecord
        {
            caseId = caseId, frame = 5, rootId = 1, nodeId = 1,
            kind = artifactState == "forbidden-retained" ? "DamageApply" : "SkillCast", configId = 42,
        }, jsonOptions) + "\n";

        if (artifactState != "missing") File.WriteAllText(tracePath, traceLine);
        if (artifactState is not ("legacy" or "missing"))
            AcceptanceJsonCodec.WriteSummary(Path.Combine(directory, caseId + "_summary.json"), new MobaAcceptanceSummary
            {
                caseId = caseId,
                traceEvidence = new MobaAcceptanceTraceEvidence
                {
                    isComplete = true, startFrame = 1, endFrame = 10, recordCount = 1,
                    traceSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(traceLine))),
                },
            });

        if (artifactState == "tampered") File.WriteAllText(tracePath, traceLine + "\n");
        if (artifactState == "truncated") File.WriteAllText(tracePath, traceLine[..(traceLine.Length / 2)]);
        if (artifactState == "forbidden-retained") File.AppendAllText(tracePath, "{");

        var source = new CompositeTraceSource(new NullTraceSource(), new FileTraceSource(directory));
        var batch = AcceptanceBatchRunner.RunDirectory(directory, artifactDirectory: null, source, exportArtifacts: false);
        var result = Assert.Single(batch.results);
        Assert.Equal(errorType, result.errorType);
        Assert.Equal(errorType == null, result.passed);
        Assert.Equal(errorType == null, batch.allPassed);
        if (artifactState == "forbidden-retained") Assert.Contains("DamageApply", result.errorMessage);
    }

    private static string CreateArtifactDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (!File.Exists(Path.Combine(dir.FullName, "tools", "run_test_gate.ps1"))) continue;
            return Directory.CreateDirectory(Path.Combine(dir.FullName, "local", "Logs", "moba-acceptance",
                $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}")).FullName;
        }
        throw new DirectoryNotFoundException("Could not locate the AbilityKit repository.");
    }

    private static string ResolveExpectationsDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "Unity", "Packages", "com.abilitykit.demo.moba.view.runtime",
                "Runtime", "Game", "Test", "Expectations");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }
        throw new DirectoryNotFoundException("未定位到 MOBA 验收期望目录。");
    }
}
