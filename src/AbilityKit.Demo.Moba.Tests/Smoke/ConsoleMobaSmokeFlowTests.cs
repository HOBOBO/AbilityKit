using System.IO;
using System.Linq;
using System.Text.Json;
using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba.Console.View;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Testing;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class ConsoleMobaSmokeFlowTests : ConsoleMobaSmokeTestBase
{
    [Fact]
    [Trait("Gate", "MobaConsoleSmoke")]
    [Trait("Category", "Smoke")]
    [Trait("Category", "MobaConsole")]
    public void Console_entry_full_battle_scenario_runs_through_shared_smoke_environment()
    {
        using var result = RunConsoleScenario(new FullBattleScenario { RandomSeed = 1337 });

        AssertConsoleSmokePassed(result);
        Assert.Equal(BattleTestScenarioLibrary.FullBattleName, result.AutoTestResult.ScriptResult.ScriptName);
        Assert.Equal(48, result.AutoTestResult.ScriptResult.StepCount);
        Assert.True(result.AutoTestResult.ScriptResult.TickCount > 0);
    }

    [Fact]
    [Trait("Gate", "MobaConsoleSmoke")]
    [Trait("Category", "MobaConsole")]
    [Trait("Category", "Presentation")]
    public void Console_view_timeline_alignment_decision_is_testable_without_runtime()
    {
        var skip = ConsoleViewTimelineRuntimeOperation.ResolveAlignment(currentFrame: 8, lastAlignedFrame: 8, tickRate: 30);
        var seek = ConsoleViewTimelineRuntimeOperation.ResolveAlignment(currentFrame: 9, lastAlignedFrame: 8, tickRate: 25);
        var invalidTickRate = ConsoleViewTimelineRuntimeOperation.ResolveAlignment(currentFrame: 10, lastAlignedFrame: 9, tickRate: 0);

        Assert.False(skip.ShouldSeek);
        Assert.Equal(8, skip.Frame);
        Assert.Equal(0f, skip.SecondsPerFrame);
        Assert.True(seek.ShouldSeek);
        Assert.Equal(9, seek.Frame);
        Assert.Equal(0.04f, seek.SecondsPerFrame, 0.0001f);
        Assert.True(invalidTickRate.ShouldSeek);
        Assert.Equal(0f, invalidTickRate.SecondsPerFrame);
    }

    [Fact]
    [Trait("Gate", "MobaConsoleSmoke")]
    [Trait("Category", "Smoke")]
    [Trait("Category", "MobaConsole")]
    [Trait("Category", "Presentation")]
    public void Console_entry_aligns_view_timeline_through_smoke_tick_path()
    {
        using var result = RunConsoleScenario(new FullBattleScenario { RandomSeed = 1337 });

        AssertConsoleSmokePassed(result);
        Assert.True(result.Bootstrapper.ViewTimelineRuntime.LastAlignedFrame > 0);
        Assert.Equal(result.Bootstrapper.Context.LastFrame, result.Bootstrapper.ViewTimelineRuntime.LastAlignedFrame);
    }

    [Fact]
    [Trait("Gate", "MobaConsoleSmoke")]
    [Trait("Category", "Smoke")]
    [Trait("Category", "MobaConsole")]
    public void Console_entry_skill_cast_scenario_validates_effect_trigger_context_trace_entry_path()
    {
        const int skillSlot = 1;
        var artifactDirectory = Path.Combine(Path.GetTempPath(), "abilitykit-moba-console-smoke", Path.GetRandomFileName());
        using var result = RunConsoleScenario(
            new SkillCastScenario { SkillSlot = skillSlot, Repeats = 2 },
            artifactOptions: ConsoleSmokeTraceArtifactOptions.Always(artifactDirectory));

        AssertConsoleSmokePassed(result);
        Assert.Equal(BattleTestScenarioLibrary.SkillCastName, result.AutoTestResult.ScriptResult.ScriptName);
        Assert.True(result.AutoTestResult.ScriptResult.TickCount >= 2);
        AssertRuntimeSkillEffectFlow(result, skillSlot);
        AssertTraceArtifactExportedForWebAnalysis(result);
    }
    private static void AssertTraceArtifactExportedForWebAnalysis(ConsoleSmokeRunResult result)
    {
        var artifact = result.ExportTraceArtifact();
        Assert.NotNull(artifact);
        Assert.True(File.Exists(artifact!.TraceJsonlPath), artifact.TraceJsonlPath);
        Assert.True(File.Exists(artifact.SummaryJsonPath), artifact.SummaryJsonPath);
        Assert.True(File.Exists(artifact.BatchSummaryJsonPath), artifact.BatchSummaryJsonPath);
        Assert.True(artifact.TraceNodeCount > 0);

        var summaryJson = File.ReadAllText(artifact.SummaryJsonPath);
        using var summary = JsonDocument.Parse(summaryJson);
        Assert.Equal(artifact.CaseId, summary.RootElement.GetProperty("caseId").GetString());
        Assert.True(summary.RootElement.GetProperty("result").GetProperty("passed").GetBoolean());
        Assert.True(summary.RootElement.GetProperty("result").GetProperty("skillCastTraceFound").GetBoolean());
        Assert.True(summary.RootElement.GetProperty("result").GetProperty("effectExecutionTraceFound").GetBoolean());
        Assert.Equal(artifact.TraceJsonlPath.Replace('\\', '/'), summary.RootElement.GetProperty("traceJsonlPath").GetString());
        Assert.Equal(artifact.SummaryJsonPath.Replace('\\', '/'), summary.RootElement.GetProperty("summaryJsonPath").GetString());

        Assert.Contains("\"kind\":\"SkillCast\"", File.ReadAllText(artifact.TraceJsonlPath));
        Assert.Contains("\"kind\":\"EffectExecution\"", File.ReadAllText(artifact.TraceJsonlPath));
    }

    private static void AssertRuntimeSkillEffectFlow(ConsoleSmokeRunResult result, int skillSlot)
    {
        var bootstrapper = result.Bootstrapper;
        Assert.True(bootstrapper.RuntimeInputPortReady, "Console smoke world must expose the runtime IMobaBattleInputPort.");
        Assert.True(bootstrapper.HostNetworkReady, "Console smoke input must use the formal Host network session path.");

        for (var i = 0; i < 60; i++)
        {
            bootstrapper.Tick();
        }

        var input = bootstrapper.RuntimeInputDiagnostics;
        Assert.True(input.SubmitCount > 0, "AutoTest skill input must be submitted through the Host network client.");
        Assert.True(input.AcceptedCount > 0, input.LastResult);
        Assert.True(input.AcceptedCommandCount > 0, input.LastResult);

        var services = bootstrapper.RuntimeServices;
        Assert.NotNull(services);
        Assert.True(services!.TryResolve<MobaPlayerActorMapService>(out var playerActors) && playerActors != null, "Runtime player actor map service must be resolved.");
        Assert.True(playerActors!.TryGetActorId(new PlayerId("player_1"), out var actorId), "Configured local player must be bound to a runtime actor.");

        Assert.True(services.TryResolve<MobaSkillLoadoutService>(out var loadout) && loadout != null, "Runtime skill loadout service must be resolved.");
        Assert.True(loadout!.TryGetSkillId(actorId, skillSlot, out var skillId), "Configured skill slot must map to a runtime skill id.");

        Assert.True(services.TryResolve<MobaTraceRegistry>(out var trace) && trace != null, "Runtime trace registry must be resolved.");
        Assert.Contains(trace!.GetNodesByKind((int)MobaTraceKind.SkillCast), node => node.Metadata != null && node.Metadata.ConfigId == skillId);
        Assert.True(trace.GetNodesByKind((int)MobaTraceKind.EffectExecution).Any(), "A configured skill cast must execute at least one formal effect trace.");
    }
}
