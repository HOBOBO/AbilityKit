using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.AutoTest;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Testing;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>
/// Base fixture for Console-driven MOBA smoke tests.
///
/// The shared scenario assets live in AbilityKit.Demo.Moba.Core, while this test base deliberately
/// uses AbilityKit.Demo.Moba.Console as the host adapter. This keeps executable demo code free from
/// test assertions, but still validates the real console bootstrap/input/tick path.
/// </summary>
public abstract class ConsoleMobaSmokeTestBase
{
    public static ConsoleSmokeRunResult RunConsoleScenario(
        IBattleTestScenario scenario,
        Action<ConsoleBattleBootstrapper>? configure = null,
        AutoTestConfig? config = null,
        ConsoleSmokeTraceArtifactOptions? artifactOptions = null)
    {
        if (scenario == null) throw new ArgumentNullException(nameof(scenario));

        ConsoleBattleBootstrapper? bootstrapper = null;
        AutoTestResult? autoResult = null;

        try
        {
            bootstrapper = CreateBootstrapper();
            configure?.Invoke(bootstrapper);

            bootstrapper.Initialize();
            bootstrapper.Start();
            TickUntilPrepared(bootstrapper);
            bootstrapper.SetupBattle();

            using var runner = new AutoTestRunner(
                bootstrapper,
                config ?? new AutoTestConfig
                {
                    TickIntervalMs = 0,
                    TimeoutTicks = 500
                });

            runner.OnTestCompleted += result => autoResult = result;
            runner.RunScenario(scenario);
            runner.WaitForCompletion();

            Assert.NotNull(autoResult);
            return new ConsoleSmokeRunResult(bootstrapper, autoResult!, artifactOptions ?? ConsoleSmokeTraceArtifactOptions.FromEnvironment());
        }
        catch
        {
            bootstrapper?.Dispose();
            throw;
        }
    }

    public static void AssertConsoleSmokePassed(ConsoleSmokeRunResult result)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));

        var autoResult = result.AutoTestResult;
        Assert.False(autoResult.HasUnexpectedError, autoResult.ErrorMessage);
        Assert.NotNull(autoResult.ScriptResult);
        Assert.True(autoResult.ScriptResult.Completed, autoResult.ScriptResult.ErrorMessage);
        Assert.NotNull(autoResult.InitTest);
        Assert.True(autoResult.InitTest.Passed, autoResult.InitTest.FailReason);
        Assert.NotNull(autoResult.PhaseTest);
        Assert.True(autoResult.PhaseTest.Passed, autoResult.PhaseTest.FailReason);

        var bootstrapper = result.Bootstrapper;
        Assert.True(bootstrapper.IsRunning);
        Assert.Equal("InMatch", bootstrapper.Flow.CurrentPhase);
        Assert.True(bootstrapper.Context.IsInitialized);
        Assert.NotNull(bootstrapper.Context.EcsWorld);
        Assert.True(bootstrapper.Context.LastFrame > 0);
    }

    protected static ConsoleBattleBootstrapper CreateBootstrapper()
    {
        return new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
    }

    private static void TickUntilPrepared(ConsoleBattleBootstrapper bootstrapper)
    {
        for (var i = 0; i < 8 && bootstrapper.Context.EcsWorld == null; i++)
        {
            bootstrapper.Tick();
        }
    }
}

public sealed class ConsoleSmokeRunResult : IDisposable
{
    public ConsoleSmokeRunResult(ConsoleBattleBootstrapper bootstrapper, AutoTestResult autoTestResult, ConsoleSmokeTraceArtifactOptions artifactOptions)
    {
        Bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
        AutoTestResult = autoTestResult ?? throw new ArgumentNullException(nameof(autoTestResult));
        ArtifactOptions = artifactOptions ?? throw new ArgumentNullException(nameof(artifactOptions));
    }

    public ConsoleBattleBootstrapper Bootstrapper { get; }

    public AutoTestResult AutoTestResult { get; }

    public ConsoleSmokeTraceArtifactOptions ArtifactOptions { get; }

    public ConsoleSmokeTraceArtifact? ExportedArtifact { get; private set; }

    public ConsoleSmokeTraceArtifact? ExportTraceArtifact()
    {
        ExportedArtifact ??= ConsoleSmokeTraceArtifactExporter.Export(Bootstrapper, AutoTestResult, ArtifactOptions);
        return ExportedArtifact;
    }

    public void Dispose()
    {
        ExportTraceArtifact();
        Bootstrapper.Stop();
        Bootstrapper.Dispose();
    }
}
