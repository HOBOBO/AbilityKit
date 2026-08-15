using System.IO;
using System.Linq;
using AbilityKit.Demo.Moba.Acceptance;
using AbilityKit.Game.Test.UnitTest;
using Xunit;

namespace AbilityKit.Demo.Moba.Acceptance.Tests;

/// <summary>
/// 验证纯 dotnet 验收判定层：用真实 .expected.json 跑 STJ 编解码 + harness-free 判定器。
/// 对应 [Trait("Gate","MobaAcceptanceDotnet")] —— 可直接挂进 test-gates.json 的 CI 门禁。
/// </summary>
[Trait("Gate", "MobaAcceptanceDotnet")]
public class AcceptanceVerifierTests
{
    private static readonly string ExpectationsDir = ResolveExpectationsDir();
    private const string ScenarioCase = "skill_10010101_scenario.expected.json";

    [Fact]
    public void Codec_loads_every_real_expectation_file_via_System_Text_Json()
    {
        var files = Directory.GetFiles(ExpectationsDir, "*.expected.json");
        Assert.True(files.Length >= 10, $"期望目录里至少应有 10 个用例，实际 {files.Length}");
        foreach (var f in files)
        {
            var ex = AcceptanceJsonCodec.LoadExpectation(f);
            Assert.NotNull(ex);
            Assert.False(string.IsNullOrEmpty(ex.caseId), $"{Path.GetFileName(f)} 缺少 caseId");
        }
    }

    [Fact]
    public void Happy_path_trace_is_accepted()
    {
        var (expectation, _) = LoadScenario();
        var summary = AcceptanceVerifier.Verify(expectation, HappyPathTrace(effectRoot: 100));
        Assert.True(summary.result.passed);
        Assert.True(summary.result.allExpectedActionsExecuted);
        Assert.True(summary.coverage.allRequiredTraceNodesMatched);
        Assert.Equal(0, summary.coverage.missingExpectedTraceNodeCount);
    }

    [Fact]
    public void Missing_required_trace_node_is_rejected_with_precise_coverage()
    {
        var (expectation, _) = LoadScenario();
        // 回归：漏掉 DamageApply
        var trace = HappyPathTrace(100).Where(r => r.kind != "DamageApply").ToArray();
        var summary = AcceptanceVerifier.Verify(expectation, trace);

        Assert.False(summary.result.passed);
        Assert.Contains("DamageApply", summary.coverage.missingTraceNodes);
        Assert.Equal(1, summary.coverage.missingExpectedTraceNodeCount);
    }

    [Fact]
    public void Forbidden_trace_node_present_fails()
    {
        var (expectation, _) = LoadScenario();
        expectation.mustNotContain = new[]
        {
            new MobaAcceptanceTraceExpectation { kind = "DamageApply", configId = 10010101 },
        };
        var summary = AcceptanceVerifier.Verify(expectation, HappyPathTrace(100));

        Assert.False(summary.result.passed);
        Assert.False(summary.coverage.allForbiddenTraceNodesAbsent);
    }

    [Fact]
    public void Summary_round_trips_through_System_Text_Json()
    {
        var (expectation, _) = LoadScenario();
        var summary = AcceptanceVerifier.Verify(expectation, HappyPathTrace(100));
        var json = AcceptanceJsonCodec.SerializeSummary(summary);
        var back = AcceptanceJsonCodec.ParseSummary(json); // 往返反序列化

        Assert.True(back.result.passed);
        Assert.Equal(summary.caseId, back.caseId);

        // 关键字段在 JSON 里存在（供 AdminConsole/CI 直接消费）
        Assert.Contains("\"caseId\"", json);
        Assert.Contains("\"passed\"", json);
        Assert.Contains("\"coverage\"", json);
        Assert.Contains("\"traceCounts\"", json);
    }

    [Fact]
    public void DamageApply_maxCount_violation_is_rejected()
    {
        var (expectation, _) = LoadScenario();
        // mustContain 里 DamageApply 10010101 maxCount=1；放 2 条应判失败
        var trace = HappyPathTrace(100).Concat(HappyPathTrace(100).Where(r => r.kind == "DamageApply")).ToArray();
        var summary = AcceptanceVerifier.Verify(expectation, trace);
        Assert.False(summary.result.passed);
        Assert.Contains("DamageApply", summary.coverage.missingTraceNodes);
    }

    // —— 辅助 ——

    private static (MobaAcceptanceExpectation expectation, string path) LoadScenario()
    {
        var path = Path.Combine(ExpectationsDir, ScenarioCase);
        return (AcceptanceJsonCodec.LoadExpectation(path), path);
    }

    /// <summary>构造覆盖 skill_10010101_scenario 全部 mustContain + expectedActions 的合成 trace。</summary>
    private static MobaAcceptanceTraceRecord[] HappyPathTrace(long effectRoot) => new[]
    {
        Rec("SkillCast",       10010101,   effectRoot),
        Rec("EffectExecution",  10010101,  effectRoot),
        Rec("EffectAction",    1241142882, effectRoot),
        Rec("EffectAction",    427896051,  effectRoot),
        Rec("EffectAction",    589451731,  effectRoot), // expectedActions 的 debug_log
        Rec("EffectAction",    2133799056, effectRoot),
        Rec("BuffApply",       10010000,   effectRoot),
        Rec("DamageApply",     10010101,   effectRoot), // count=1 ≤ maxCount=1
    };

    private static MobaAcceptanceTraceRecord Rec(string kind, int configId, long rootId) => new()
    {
        kind = kind,
        configId = configId,
        rootId = rootId,
        nodeId = rootId * 1000 + configId % 1000,
    };

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
        throw new DirectoryNotFoundException("找不到 MOBA 验收期望目录（未定位到仓库根）。");
    }
}
