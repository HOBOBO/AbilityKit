using System;
using System.IO;
using AbilityKit.Demo.Moba.Acceptance;
using AbilityKit.Scenario;
using AbilityKit.Demo.Moba.Testing;
using AbilityKit.Game.Test.UnitTest;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>
/// Seam #4 第一切片：live-sim trace 源。跑真实 console sim（纯 dotnet）→ 读其导出的
/// <c>&lt;caseId&gt;_trace.jsonl</c> → 返回 <see cref="MobaAcceptanceTraceRecord"/>[]，供 <see cref="AcceptanceVerifier"/> 判定。
/// 证明「期望 → 真实 dotnet sim → trace → 判定」闭环，无需 Unity、无需手动捕获。
/// 复用经验证的 console smoke 管线（<see cref="ConsoleMobaSmokeTestBase.RunConsoleScenario"/> + trace 导出）。
/// </summary>
public sealed class LiveSimTraceSource : ITraceSource
{
    private readonly Func<string, IBattleTestScenario?> _scenarioResolver;

    public LiveSimTraceSource(Func<string, IBattleTestScenario?> scenarioResolver)
        => _scenarioResolver = scenarioResolver ?? throw new ArgumentNullException(nameof(scenarioResolver));

    public bool TryGetRecords(string caseId, out MobaAcceptanceTraceRecord[] records)
    {
        records = null!;
        var scenario = _scenarioResolver(caseId);
        if (scenario is null) return false;

        // 复用经验证的 console smoke：跑真实逻辑世界 + 强制 Always 导出 trace jsonl。
        var artifactDir = Path.Combine(Path.GetTempPath(), "abk-livesim-" + Guid.NewGuid().ToString("N"));
        using var run = ConsoleMobaSmokeTestBase.RunConsoleScenario(
            scenario,
            artifactOptions: ConsoleSmokeTraceArtifactOptions.Always(artifactDir));
        ConsoleMobaSmokeTestBase.AssertConsoleSmokePassed(run);

        var artifact = run.ExportTraceArtifact();
        if (artifact is null) return false;
        records = AcceptanceJsonCodec.LoadTraceRecords(artifact.TraceJsonlPath);
        return records.Length > 0;
    }
}
