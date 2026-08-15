using System.IO;
using AbilityKit.Demo.Moba.Acceptance;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>
/// Seam #4 收敛件：真实期望文件（skill_10010101_scenario）的 live 判定。
/// 期望 → 真实 console sim（actors 装配 + setup + timeline + 锚点平移）→ 真实 trace → AcceptanceVerifier。
///
/// 【已验证（plumbing 级）】SkillCast/EffectExecution/全部 expectedActions/BuffApply + dash 全程 8 单位穿过 target。
/// 【已知缺口（hit-chain）】dash 的 motion-hit（hit_trigger 10010111 → DamageApply + 击退）未触发：
/// 几何已确认穿过 target、双方均有 Collider 组件、brain 已抑——下一步排查
/// MobaMotionInitSystem 的 MobaMotionCollisionWorldAdapter 查询面（service world vs actor 注册表）
/// 与 dash sweep 的 layer 过滤。排查点详见设计文档 §14.1。
/// </summary>
[Trait("Gate", "MobaConsoleSmoke")]
[Trait("Category", "Smoke")]
public class LiveSimAcceptanceScenarioRunnerTests
{
    [Fact]
    public void Real_dash_hit_expectation_runs_live_through_full_scenario_pipeline()
    {
        var expectationPath = Path.Combine(
            ResolveExpectationsDir(), "skill_10010101_scenario.expected.json");
        var expectation = AcceptanceJsonCodec.LoadExpectation(expectationPath);

        var summary = LiveSimAcceptanceScenarioRunner.Run(expectation);

        // 完整 live verdict：期望 → 真实 console sim → 真实 trace → 判定全链路（含 dash motion-hit → DamageApply + 击退）。
        Assert.True(summary.result.passed,
            "live verdict failed. missingTraceNodes=" + summary.coverage.missingTraceNodes
            + " | missingActions=" + summary.coverage.missingActions);
        Assert.True(summary.result.skillCastTraceFound);
        Assert.True(summary.result.effectExecutionTraceFound);
        Assert.True(summary.result.allExpectedActionsExecuted);
        Assert.True(summary.result.traceNodeCount > 0);
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
