using System.Diagnostics;
using System.Text.Json;
using Xunit;

public sealed class ShooterPureStateSampleBlockAbScriptTests
{
    [Fact]
    public void RunnerPlanPairsEveryCaseWithSingleAndSampleBlockTemplates()
    {
        var script = Path.Combine(GetRepositoryRoot(), "tools", "run_shooter_pure_state_sample_block_ab.ps1");
        var result = RunPowerShell(
            "-File", script,
            "-EnemyBudgets", "1000",
            "-NetworkEnvironments", "limitedbw",
            "-Repetitions", "2",
            "-PlanOnly");

        Assert.Equal(0, result.ExitCode);
        using var plan = JsonDocument.Parse(result.StandardOutput);
        var root = plan.RootElement;
        var cases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(2, root.GetProperty("caseCount").GetInt32());
        Assert.All(cases, item =>
        {
            Assert.Equal("mass-battle-lod-aoi", item.GetProperty("baselineTemplateId").GetString());
            Assert.Equal("mass-battle-lod-aoi-sample-block", item.GetProperty("candidateTemplateId").GetString());
            Assert.Equal(5, item.GetProperty("syncModel").GetInt32());
        });
    }

    [Fact]
    public void AbRunnerCollectsBothVariantsBeforeApplyingItsOptionalComparisonGate()
    {
        var repositoryRoot = GetRepositoryRoot();
        var abRunner = File.ReadAllText(Path.Combine(repositoryRoot, "tools", "run_shooter_pure_state_sample_block_ab.ps1"));
        var headlessRunner = File.ReadAllText(Path.Combine(repositoryRoot, "tools", "run_shooter_unity_headless_multiplayer.ps1"));
        var headlessCommand = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Unity", "Packages", "com.abilitykit.demo.shooter.view.runtime", "Editor",
            "ShooterMultiplayerHeadlessClientCommand.cs"));

        Assert.Contains("SkipPerformanceValidation = $true", abRunner, StringComparison.Ordinal);
        Assert.Contains("if ($null -eq $Values -or $Values.Count -eq 0)", abRunner, StringComparison.Ordinal);
        Assert.Contains("medianResyncNeededCountDelta", abRunner, StringComparison.Ordinal);
        Assert.Contains("medianFullSnapshotAppliedCountDelta", abRunner, StringComparison.Ordinal);
        Assert.Contains("medianAutomaticFullStateSyncCoalescedRequestCountDelta", abRunner, StringComparison.Ordinal);
        Assert.Contains("medianP99QueueWaitDeltaMs", abRunner, StringComparison.Ordinal);
        Assert.Contains("medianP99ApplyDeltaMs", abRunner, StringComparison.Ordinal);
        Assert.Contains("medianPeakQueueDepthDelta", abRunner, StringComparison.Ordinal);
        Assert.Contains("medianCoalescedSnapshotCountDelta", abRunner, StringComparison.Ordinal);
        Assert.Contains("medianBudgetLimitedDrainRatioDelta", abRunner, StringComparison.Ordinal);
        Assert.Contains("[switch]$SkipPerformanceValidation", headlessRunner, StringComparison.Ordinal);
        Assert.Contains("if (-not $SkipPerformanceValidation)", headlessRunner, StringComparison.Ordinal);
        Assert.Contains("[int]$CompileWarmupTimeoutSeconds = 600", headlessRunner, StringComparison.Ordinal);
        Assert.Contains("[int]$ClientStartupTimeoutSeconds = 600", headlessRunner, StringComparison.Ordinal);
        Assert.Contains("$startupTimer = [System.Diagnostics.Stopwatch]::StartNew()", headlessRunner, StringComparison.Ordinal);
        Assert.Contains("Both Shooter clients published startup state", headlessRunner, StringComparison.Ordinal);
        Assert.Contains("[string]$SyncTemplateId = 'mass-battle-lod-aoi-sample-block'", headlessRunner, StringComparison.Ordinal);
        Assert.Contains("[string]$NetworkEnvironmentId = 'ideal'", headlessRunner, StringComparison.Ordinal);
        Assert.Contains("AddSeconds(30)", headlessRunner, StringComparison.Ordinal);
        Assert.Contains("[System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete", headlessRunner, StringComparison.Ordinal);
        Assert.Contains("[System.Text.Encoding]::Unicode", headlessRunner, StringComparison.Ordinal);
        Assert.Contains("[System.Text.Encoding]::BigEndianUnicode", headlessRunner, StringComparison.Ordinal);
        Assert.Contains("$readOffset--", headlessRunner, StringComparison.Ordinal);
        Assert.Contains("[string]$ServerLogPath", headlessRunner, StringComparison.Ordinal);
        Assert.Contains("[BattleLogicHost] ServerPerformance", headlessRunner, StringComparison.Ordinal);
        Assert.Contains("server-performance.log", headlessRunner, StringComparison.Ordinal);
        Assert.Contains("battlePushBudgetLimitedDrainCount", headlessCommand, StringComparison.Ordinal);
        Assert.Contains("battlePushCoalescedSnapshotCount", headlessCommand, StringComparison.Ordinal);
        Assert.Matches("SetProfileField\\(\\s*profile,\\s*\"networkEnvironmentId\"", headlessCommand);
        Assert.Contains("movementBackwardEvents", headlessCommand, StringComparison.Ordinal);
        Assert.Contains("authorityAdvanced", headlessCommand, StringComparison.Ordinal);
    }

    [Fact]
    public void ComparatorCalculatesPairedMetricsAndPassesExplicitGate()
    {
        var directory = Directory.CreateTempSubdirectory("shooter-sample-block-ab-");
        try
        {
            var baselineOwner = WriteResult(directory.FullName, "baseline-owner.json", "mass-battle-lod-aoi", 1000d, 0L, 0L, 0.20d, 0.10d);
            var baselineMember = WriteResult(directory.FullName, "baseline-member.json", "mass-battle-lod-aoi", 1000d, 0L, 0L, 0.20d, 0.10d);
            var candidateOwner = WriteResult(directory.FullName, "candidate-owner.json", "mass-battle-lod-aoi-sample-block", 1500d, 10L, 300L, 0.10d, 0.05d);
            var candidateMember = WriteResult(directory.FullName, "candidate-member.json", "mass-battle-lod-aoi-sample-block", 1500d, 10L, 300L, 0.10d, 0.05d);
            var output = Path.Combine(directory.FullName, "comparison.json");
            var script = Path.Combine(GetRepositoryRoot(), "tools", "compare_shooter_pure_state_sample_block_ab.ps1");

            var result = RunPowerShell(
                "-File", script,
                "-BaselineOwnerResultPath", baselineOwner,
                "-BaselineMemberResultPath", baselineMember,
                "-CandidateOwnerResultPath", candidateOwner,
                "-CandidateMemberResultPath", candidateMember,
                "-OutputPath", output,
                "-EnableGate");

            Assert.Equal(0, result.ExitCode);
            using var comparison = JsonDocument.Parse(File.ReadAllText(output));
            var root = comparison.RootElement;
            Assert.True(root.GetProperty("contractPassed").GetBoolean());
            Assert.True(root.GetProperty("gatePassed").GetBoolean());
            Assert.Equal(1.5d, root.GetProperty("delta").GetProperty("averagePayloadAmplification").GetDouble(), 3);
            Assert.Equal(-0.1d, root.GetProperty("delta").GetProperty("starvationRatio").GetDouble(), 3);
            Assert.Equal(20L, root.GetProperty("candidate").GetProperty("receivedSampleBlockCount").GetInt64());
            Assert.Equal(600L, root.GetProperty("candidate").GetProperty("historicalTransformCount").GetInt64());
            Assert.Equal(30, root.GetProperty("candidate").GetProperty("maxTransformSamplesPerBlock").GetInt32());
            Assert.Equal(4L, root.GetProperty("candidate").GetProperty("resyncNeededCount").GetInt64());
            Assert.Equal(10L, root.GetProperty("candidate").GetProperty("automaticFullStateSyncCoalescedRequestCount").GetInt64());
            Assert.Equal(8L, root.GetProperty("delta").GetProperty("automaticFullStateSyncCoalescedRequestCount").GetInt64());
            Assert.Equal(4, root.GetProperty("candidate").GetProperty("peakQueueDepth").GetInt32());
            Assert.Equal(4L, root.GetProperty("candidate").GetProperty("coalescedSnapshotCount").GetInt64());
            Assert.Equal(2, root.GetProperty("delta").GetProperty("peakQueueDepth").GetInt32());
            Assert.Equal(0.05d, root.GetProperty("delta").GetProperty("budgetLimitedDrainRatio").GetDouble(), 3);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void ComparatorFailsClosedWhenCandidateArtifactUsesWrongTemplate()
    {
        var directory = Directory.CreateTempSubdirectory("shooter-sample-block-ab-invalid-");
        try
        {
            var baselineOwner = WriteResult(directory.FullName, "baseline-owner.json", "mass-battle-lod-aoi", 1000d, 0L, 0L, 0.2d, 0.1d);
            var baselineMember = WriteResult(directory.FullName, "baseline-member.json", "mass-battle-lod-aoi", 1000d, 0L, 0L, 0.2d, 0.1d);
            var candidateOwner = WriteResult(directory.FullName, "candidate-owner.json", "mass-battle-lod-aoi", 1500d, 10L, 500L, 0.1d, 0.05d);
            var candidateMember = WriteResult(directory.FullName, "candidate-member.json", "mass-battle-lod-aoi", 1500d, 10L, 500L, 0.1d, 0.05d);
            var output = Path.Combine(directory.FullName, "comparison.json");
            var script = Path.Combine(GetRepositoryRoot(), "tools", "compare_shooter_pure_state_sample_block_ab.ps1");

            var result = RunPowerShell(
                "-File", script,
                "-BaselineOwnerResultPath", baselineOwner,
                "-BaselineMemberResultPath", baselineMember,
                "-CandidateOwnerResultPath", candidateOwner,
                "-CandidateMemberResultPath", candidateMember,
                "-OutputPath", output);

            Assert.NotEqual(0, result.ExitCode);
            using var comparison = JsonDocument.Parse(File.ReadAllText(output));
            Assert.False(comparison.RootElement.GetProperty("contractPassed").GetBoolean());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void ComparatorFailsClosedWhenCandidateExceedsTransformBlockBudget()
    {
        var directory = Directory.CreateTempSubdirectory("shooter-sample-block-ab-budget-");
        try
        {
            var baselineOwner = WriteResult(directory.FullName, "baseline-owner.json", "mass-battle-lod-aoi", 1000d, 0L, 0L, 0.2d, 0.1d);
            var baselineMember = WriteResult(directory.FullName, "baseline-member.json", "mass-battle-lod-aoi", 1000d, 0L, 0L, 0.2d, 0.1d);
            var candidateOwner = WriteResult(directory.FullName, "candidate-owner.json", "mass-battle-lod-aoi-sample-block", 1500d, 10L, 330L, 0.1d, 0.05d, maxTransformsPerBlock: 33);
            var candidateMember = WriteResult(directory.FullName, "candidate-member.json", "mass-battle-lod-aoi-sample-block", 1500d, 10L, 330L, 0.1d, 0.05d, maxTransformsPerBlock: 33);
            var output = Path.Combine(directory.FullName, "comparison.json");
            var script = Path.Combine(GetRepositoryRoot(), "tools", "compare_shooter_pure_state_sample_block_ab.ps1");

            var result = RunPowerShell(
                "-File", script,
                "-BaselineOwnerResultPath", baselineOwner,
                "-BaselineMemberResultPath", baselineMember,
                "-CandidateOwnerResultPath", candidateOwner,
                "-CandidateMemberResultPath", candidateMember,
                "-OutputPath", output);

            Assert.NotEqual(0, result.ExitCode);
            using var comparison = JsonDocument.Parse(File.ReadAllText(output));
            var root = comparison.RootElement;
            Assert.False(root.GetProperty("contractPassed").GetBoolean());
            Assert.Contains(
                root.GetProperty("assertions").EnumerateArray(),
                item => item.GetProperty("name").GetString() == "candidate-respects-transform-block-budget"
                        && !item.GetProperty("passed").GetBoolean());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void ComparatorGateRejectsQueueAndApplyLatencyRegression()
    {
        var directory = Directory.CreateTempSubdirectory("shooter-sample-block-ab-latency-");
        try
        {
            var baselineOwner = WriteResult(directory.FullName, "baseline-owner.json", "mass-battle-lod-aoi", 1000d, 0L, 0L, 0.2d, 0.1d);
            var baselineMember = WriteResult(directory.FullName, "baseline-member.json", "mass-battle-lod-aoi", 1000d, 0L, 0L, 0.2d, 0.1d);
            var candidateOwner = WriteResult(directory.FullName, "candidate-owner.json", "mass-battle-lod-aoi-sample-block", 1500d, 10L, 300L, 0.1d, 0.05d, queueWaitMs: 100d, applyMs: 100d);
            var candidateMember = WriteResult(directory.FullName, "candidate-member.json", "mass-battle-lod-aoi-sample-block", 1500d, 10L, 300L, 0.1d, 0.05d, queueWaitMs: 100d, applyMs: 100d);
            var output = Path.Combine(directory.FullName, "comparison.json");
            var script = Path.Combine(GetRepositoryRoot(), "tools", "compare_shooter_pure_state_sample_block_ab.ps1");

            var result = RunPowerShell(
                "-File", script,
                "-BaselineOwnerResultPath", baselineOwner,
                "-BaselineMemberResultPath", baselineMember,
                "-CandidateOwnerResultPath", candidateOwner,
                "-CandidateMemberResultPath", candidateMember,
                "-OutputPath", output,
                "-EnableGate");

            Assert.NotEqual(0, result.ExitCode);
            using var comparison = JsonDocument.Parse(File.ReadAllText(output));
            var assertions = comparison.RootElement.GetProperty("assertions").EnumerateArray().ToArray();
            Assert.Contains(assertions, item => item.GetProperty("name").GetString() == "queue-wait-regression-budget" && !item.GetProperty("passed").GetBoolean());
            Assert.Contains(assertions, item => item.GetProperty("name").GetString() == "apply-regression-budget" && !item.GetProperty("passed").GetBoolean());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static string WriteResult(
        string directory,
        string fileName,
        string templateId,
        double averagePayloadBytes,
        long sampleBlockCount,
        long historicalTransformCount,
        double starvationRatio,
        double heldRatio,
        int maxTransformsPerBlock = 30,
        double queueWaitMs = 5d,
        double applyMs = 2d)
    {
        var path = Path.Combine(directory, fileName);
        var result = new
        {
            success = true,
            state = new
            {
                syncTemplateId = templateId,
                syncModel = 5,
                networkEnvironmentId = "limitedbw",
                enemyBudget = 1000,
                viewBackend = "GameObject",
                battlePushAveragePayloadBytes = averagePayloadBytes,
                battlePushMaxPayloadBytes = averagePayloadBytes * 2d,
                pureStatePlaybackStarvationRatio = starvationRatio,
                pureStatePlaybackHeldRatio = heldRatio,
                p99SnapshotArrivalGapMs = templateId.EndsWith("sample-block", StringComparison.Ordinal) ? 90d : 80d,
                p95SnapshotSourceAgeMs = 100d,
                p99BattlePushQueueWaitMs = queueWaitMs,
                p99BattlePushApplyMs = applyMs,
                maxBackwardMovement = 0.1d,
                maxUnexplainedBackwardMovement = 0.05d,
                averageGcBytesPerFrame = 1024d,
                battlePushPeakQueueDepth = sampleBlockCount > 0L ? 4 : 2,
                battlePushEnqueuedCount = 100L,
                battlePushProcessedCount = 98L,
                battlePushCoalescedSnapshotCount = sampleBlockCount > 0L ? 2L : 0L,
                battlePushDrainCount = 200L,
                battlePushBudgetLimitedDrainCount = sampleBlockCount > 0L ? 20L : 10L,
                pureStatePlaybackReceivedSampleBlockCount = sampleBlockCount,
                pureStatePlaybackReceivedFrameSampleCount = sampleBlockCount * 2L,
                pureStatePlaybackRejectedFrameSampleCount = 0L,
                pureStatePlaybackStaleFrameSampleCount = 0L,
                pureStatePlaybackInvalidFrameSampleCount = 0L,
                pureStatePlaybackReceivedTransformSampleCount = historicalTransformCount,
                pureStatePlaybackMaxTransformSampleCountPerBlock = sampleBlockCount > 0L ? maxTransformsPerBlock : 0,
                pureStatePlaybackReceivedAuthoritativeTransformCount = 400L,
                snapshotResyncNeededCount = sampleBlockCount > 0L ? 2L : 0L,
                automaticFullStateSyncCoalescedRequestCount = sampleBlockCount > 0L ? 5L : 1L,
                pureStateFullAppliedCount = sampleBlockCount > 0L ? 3L : 1L,
            },
        };
        File.WriteAllText(path, JsonSerializer.Serialize(result));
        return path;
    }

    private static ProcessResult RunPowerShell(params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
        process.StartInfo.ArgumentList.Add("Bypass");
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(30_000), "PowerShell A/B contract test timed out.");
        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "tools")) &&
                File.Exists(Path.Combine(directory.FullName, "global.json")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Server", "Orleans")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the AbilityKit repository root.");
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
