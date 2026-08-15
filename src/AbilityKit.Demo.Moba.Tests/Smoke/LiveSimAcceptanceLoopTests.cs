using System.Linq;
using AbilityKit.Demo.Moba.Acceptance;
using AbilityKit.Demo.Moba.Testing;
using AbilityKit.Game.Test.UnitTest;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>
/// Seam #4 第一切片：证明「期望 → 真实 dotnet console sim → trace → 验收判定」闭环。
/// 用真实 <see cref="SkillCastScenario"/> 产 trace，喂 <see cref="AcceptanceVerifier"/>。
/// 这条路径无需 Unity、无需手动 capture——live-sim 直接产 trace。
/// </summary>
[Trait("Gate", "MobaConsoleSmoke")]
[Trait("Category", "Smoke")]
public class LiveSimAcceptanceLoopTests
{
    private const string CaseId = "live-sim-skillcast";

    private static readonly ITraceSource Source = new LiveSimTraceSource(
        caseId => caseId == CaseId ? new SkillCastScenario { SkillSlot = 1, Repeats = 1 } : null);

    [Fact]
    public void Live_sim_produces_real_skillcast_and_effect_trace()
    {
        Assert.True(Source.TryGetRecords(CaseId, out var records));
        Assert.NotEmpty(records);
        Assert.Contains(records, r => r.kind == "SkillCast");
        Assert.Contains(records, r => r.kind == "EffectExecution");
    }

    [Fact]
    public void Live_sim_trace_feeds_acceptance_verifier_end_to_end()
    {
        Assert.True(Source.TryGetRecords(CaseId, out var records));

        // 用真实 trace 里的 configId 构造最小期望——证明判定器能匹配真实 sim 产物（configId 来自运行期技能表）。
        var skillCast = records.First(r => r.kind == "SkillCast");
        var effectExec = records.First(r => r.kind == "EffectExecution");
        var expectation = new MobaAcceptanceExpectation
        {
            caseId = CaseId,
            config = new MobaAcceptanceConfigExpectation { skillId = skillCast.configId, effectId = effectExec.configId },
            mustContain = new[]
            {
                new MobaAcceptanceTraceExpectation { kind = "SkillCast", configId = skillCast.configId, minCount = 1 },
                new MobaAcceptanceTraceExpectation { kind = "EffectExecution", configId = effectExec.configId, minCount = 1 },
            },
        };

        var summary = AcceptanceVerifier.Verify(expectation, records);

        Assert.True(summary.result.passed);
        Assert.True(summary.result.skillCastTraceFound);
        Assert.True(summary.result.effectExecutionTraceFound);
        Assert.Equal(0, summary.coverage.missingExpectedTraceNodeCount);
    }
}
