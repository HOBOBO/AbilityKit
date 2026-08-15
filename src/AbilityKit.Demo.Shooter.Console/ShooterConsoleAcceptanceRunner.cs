using AbilityKit.Demo.Host.Console;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Network.Runtime;
using AbilityKit.Demo.Harness;

namespace AbilityKit.Demo.Shooter.Console;

internal sealed class ShooterConsoleAcceptanceRunner
{
    private const float DeltaSeconds = 1f / 30f;

    private readonly IConsoleOutput _output;

    public ShooterConsoleAcceptanceRunner(IConsoleOutput output)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    public int Run(ShooterConsoleOptions options)
    {
        try
        {
            return options.Mode switch
            {
                ShooterConsoleMode.Spec => RunSpec(options),
                ShooterConsoleMode.Acceptance => RunAcceptance(options),
                ShooterConsoleMode.Matrix => RunMatrix(options),
                _ => Fail($"Mode '{options.Mode}' is not an acceptance mode.")
            };
        }
        catch (Exception ex)
        {
            _output.Write(ConsoleOutputChannel.Error, $"ERROR exception={Escape(ex.GetType().Name)} message=\"{Escape(ex.Message)}\"");
            _output.Write(ConsoleOutputChannel.Error, $"RESULT status=fail mode={options.Mode.ToString().ToLowerInvariant()}");
            return 1;
        }
    }

    private int RunSpec(ShooterConsoleOptions options)
    {
        if (!TryResolveSpec(options.SpecId, out var spec))
        {
            return Fail($"Unknown spec '{options.SpecId}'.");
        }

        var result = new ShooterAcceptanceSpecRunner().Run(spec);
        _output.Write(ConsoleOutputChannel.Battle,
            $"RESULT status=pass mode=spec spec={result.SpecId} frame={result.Frame} hash=0x{result.StateHash:X8} players={result.Snapshot.Players.Length} bullets={result.Snapshot.Bullets.Length} events={result.Events.Count} packedWorld={result.PackedSnapshot.WorldId}");
        return 0;
    }

    private int RunAcceptance(ShooterConsoleOptions options)
    {
        if (!TryResolveSync(options.SyncId, out var sync))
        {
            return Fail($"Unknown sync '{options.SyncId}'.");
        }

        if (!TryResolveNetwork(options.NetworkId, out var network))
        {
            return Fail($"Unknown network '{options.NetworkId}'.");
        }

        using var session = ShooterAcceptanceLab.Create(sync, network, enableAuthoritativeWorld: options.Authoritative);
        var result = session.Run(options.Frames, DeltaSeconds, options.Seed);
        var comparison = session.CompareWorlds();
        WriteCase(result, network.Id, comparison.MaxDistance, options.Authoritative);
        _output.Write(ConsoleOutputChannel.Sync,
            $"RESULT status={(result.Completed ? "pass" : "fail")} mode=acceptance sync={ShooterConsoleOptions.ToSyncId(sync.Model)} network={network.Id} frames={options.Frames} seed={options.Seed} authoritative={options.Authoritative.ToString().ToLowerInvariant()} maxDivergence={comparison.MaxDistance:0.0000}");
        return result.Completed ? 0 : 1;
    }

    private int RunMatrix(ShooterConsoleOptions options)
    {
        var batch = ShooterAcceptanceLab.RunCatalogMatrix(options.Frames, DeltaSeconds, options.Seed);
        for (var i = 0; i < batch.Results.Count; i++)
        {
            var result = batch.Results[i];
            WriteCase(result, networkId: "catalog", maxDivergence: 0d, authoritative: false);
        }

        foreach (var row in batch.Summary.Rows)
        {
            _output.Write(ConsoleOutputChannel.Sync,
                $"SUMMARY carrier={Escape(row.CarrierName)} sync={ShooterConsoleOptions.ToSyncId(row.SyncModel)} status={row.Status.ToString().ToLowerInvariant()} count={row.Count}");
        }

        _output.Write(ConsoleOutputChannel.Sync,
            $"RESULT status={(batch.AllCompleted ? "pass" : "fail")} mode=matrix scenarios={batch.ScenarioCount} completed={batch.CompletedCount} degraded={batch.DegradedCount} unsupported={batch.UnsupportedCount} failed={batch.FailedCount} frames={options.Frames} seed={options.Seed}");
        return batch.AllCompleted ? 0 : 1;
    }

    private void WriteCase(DemoHarnessRunResult result, string networkId, double maxDivergence, bool authoritative)
    {
        var metrics = result.Metrics;
        _output.Write(ConsoleOutputChannel.Sync,
            $"CASE name=\"{Escape(result.Scenario.Name)}\" carrier=\"{Escape(result.Scenario.CarrierName)}\" sync={ShooterConsoleOptions.ToSyncId(result.Scenario.SyncModel)} network={networkId} status={result.Status.ToString().ToLowerInvariant()} completed={result.Completed.ToString().ToLowerInvariant()} steps={metrics.StepsRun} lastFrame={metrics.LastFrame} reconciliations={metrics.ReconciliationCount} fullSnapshots={metrics.FullSnapshotRequestCount} replayMax={metrics.MaxReplayTicks} healthWarnings={metrics.HealthWarningCount} healthErrors={metrics.HealthErrorCount} authoritative={authoritative.ToString().ToLowerInvariant()} maxDivergence={maxDivergence:0.0000} reason=\"{Escape(result.Reason)}\"");
    }

    private static bool TryResolveSpec(string id, out ShooterAcceptanceSpec spec)
    {
        if (string.Equals(id, "basic-combat", StringComparison.OrdinalIgnoreCase))
        {
            spec = ShooterAcceptanceSpecs.BasicCombat;
            return true;
        }

        spec = ShooterAcceptanceSpecs.BasicCombat;
        return false;
    }

    private static bool TryResolveSync(string id, out ShooterAcceptanceSyncOption option)
    {
        var normalized = Normalize(id);
        foreach (var sync in ShooterAcceptanceCatalog.SyncModes)
        {
            if (Normalize(ShooterConsoleOptions.ToSyncId(sync.Model)) == normalized || Normalize(sync.Model.ToString()) == normalized)
            {
                option = sync;
                return sync.Implemented;
            }
        }

        option = default;
        return false;
    }

    private static bool TryResolveNetwork(string id, out ShooterAcceptanceNetworkOption option)
    {
        var normalized = Normalize(id);
        foreach (var network in ShooterAcceptanceCatalog.NetworkEnvironments)
        {
            if (Normalize(network.Id) == normalized)
            {
                option = network;
                return true;
            }
        }

        option = default;
        return false;
    }

    private int Fail(string message)
    {
        _output.Write(ConsoleOutputChannel.Error, $"ERROR message=\"{Escape(message)}\"");
        _output.Write(ConsoleOutputChannel.Error, "RESULT status=fail");
        return 1;
    }

    private static string Normalize(string value)
    {
        return value.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
    }
}
