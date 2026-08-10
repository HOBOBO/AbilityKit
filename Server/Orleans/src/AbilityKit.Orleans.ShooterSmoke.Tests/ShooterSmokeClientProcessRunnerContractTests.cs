using AbilityKit.Demo.Shooter.View;
using Xunit;

public sealed class ShooterSmokeClientProcessRunnerContractTests
{
    private static readonly string Source = File.ReadAllText(GetSourcePath());
    private static readonly string FrameSyncSource = File.ReadAllText(GetUnitySourcePath(
        "Runtime", "Client", "Synchronization", "ShooterClientFrameSyncController.cs"));
    private static readonly string GatewayConnectionSource = File.ReadAllText(GetUnitySourcePath(
        "Runtime", "Client", "Gateway", "ShooterRoomGatewayConnection.cs"));
    private static readonly string BattleHandleSource = File.ReadAllText(GetUnitySourcePath(
        "Runtime", "Client", "ShooterClientBattleHandle.cs"));
    private static readonly string BattleDataPlaneSource = File.ReadAllText(GetUnitySourcePath(
        "Runtime", "Client", "ShooterBattleDataPlane.cs"));

    [Fact]
    public void ClientProcessUsesFormalAccountLoginProtocol()
    {
        Assert.Contains("var accountId = $\"shooter-smoke-{options.ClientId}-{Guid.NewGuid():N}\";", Source, StringComparison.Ordinal);
        Assert.Contains("ShooterSmokeScenarioBase.LoginAccountAsync(connection, accountId, kickExisting: true)", Source, StringComparison.Ordinal);
        Assert.DoesNotContain("ShooterSmokeScenarioBase.LoginGuestAsync(connection)", Source, StringComparison.Ordinal);
    }

    [Fact]
    public void FinalResultReusesSingleRuntimeStateSample()
    {
        Assert.Contains("var finalRuntimeFrame = runtime.CurrentFrame;", Source, StringComparison.Ordinal);
        Assert.Contains("var finalViewFrame = presentation.ViewModel.Frame;", Source, StringComparison.Ordinal);
        Assert.Contains("var finalStateHash = runtime.ComputeStateHash();", Source, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(Source, "            finalRuntimeFrame,"));
        Assert.Equal(2, CountOccurrences(Source, "            finalViewFrame,"));
        Assert.Equal(1, CountOccurrences(Source, "            finalStateHash,"));
        Assert.Contains("var latestComparableSnapshotFrame = 0;", Source, StringComparison.Ordinal);
        Assert.Contains("var latestComparableAuthoritativeHash = 0u;", Source, StringComparison.Ordinal);
        Assert.Contains("var latestComparableClientHash = 0u;", Source, StringComparison.Ordinal);
        Assert.Contains("launcher.GatewayConnection.CurrentSession?.FrameSync.LastImportedSnapshotEvidence", Source, StringComparison.Ordinal);
        Assert.Contains("importedEvidence.Frame == pushResult.PackedFrame", Source, StringComparison.Ordinal);
        Assert.Contains("importedEvidence.AuthoritativeStateHash == pushResult.PackedStateHash", Source, StringComparison.Ordinal);
        Assert.Contains("comparableClientHash = importedEvidence.ImportedStateHash;", Source, StringComparison.Ordinal);
        Assert.Contains("else if (runtime.CurrentFrame == pushResult.PackedFrame)", Source, StringComparison.Ordinal);
        Assert.Contains("comparableClientHash = runtime.ComputeStateHash();", Source, StringComparison.Ordinal);
        Assert.Contains("SHOOTER_MP_HASH_SAMPLE status=", Source, StringComparison.Ordinal);
        Assert.Contains("latestComparableAuthoritativeHash = pushResult.PackedStateHash;", Source, StringComparison.Ordinal);
        Assert.Contains("latestComparableClientHash = comparableClientHash;", Source, StringComparison.Ordinal);
        Assert.Contains("public ShooterClientSession? CurrentSession => _session;", GatewayConnectionSource, StringComparison.Ordinal);
        Assert.Contains("LastImportedSnapshotEvidence = new ShooterClientImportedSnapshotEvidence(", FrameSyncSource, StringComparison.Ordinal);
        Assert.True(
            FrameSyncSource.IndexOf("LastImportedSnapshotEvidence = new ShooterClientImportedSnapshotEvidence(", StringComparison.Ordinal)
            < FrameSyncSource.IndexOf("_predictionReconciliation.ReconcileAfterAuthoritativeSnapshot(", StringComparison.Ordinal));
        Assert.Contains("var hasComparableReconciliation =", Source, StringComparison.Ordinal);
        Assert.Contains("reconciliation.ImportedStateHash != 0u;", Source, StringComparison.Ordinal);
        Assert.Contains("var hasComparableAppliedSnapshot =", Source, StringComparison.Ordinal);
        Assert.Contains("latestComparableClientHash != 0u;", Source, StringComparison.Ordinal);
        Assert.Contains("TryCapturePureStateComparableHash(", Source, StringComparison.Ordinal);
        Assert.Contains("presentation.LastPureStateAppliedFrame", Source, StringComparison.Ordinal);
        Assert.Contains("presentation.LastPureStateAppliedStateHash", Source, StringComparison.Ordinal);
        Assert.Contains("sampleSource = \"pure-state\";", Source, StringComparison.Ordinal);
        Assert.Contains("latestComparableSnapshotFrame = pushResult.PackedFrame;", Source, StringComparison.Ordinal);
        Assert.Contains("latestComparableAuthoritativeHash = pushResult.PackedStateHash;", Source, StringComparison.Ordinal);
        Assert.Contains("latestComparableClientHash = comparableClientHash;", Source, StringComparison.Ordinal);
        Assert.Contains("? reconciliation.AuthoritativeFrame", Source, StringComparison.Ordinal);
        Assert.Contains("? reconciliation.AuthoritativeStateHash", Source, StringComparison.Ordinal);
        Assert.Contains("? reconciliation.ImportedStateHash", Source, StringComparison.Ordinal);
        Assert.Contains(": hasComparableAppliedSnapshot ? latestComparableSnapshotFrame : 0;", Source, StringComparison.Ordinal);
        Assert.Contains(": hasComparableAppliedSnapshot ? latestComparableAuthoritativeHash : 0u;", Source, StringComparison.Ordinal);
        Assert.Contains(": hasComparableAppliedSnapshot ? latestComparableClientHash : 0u;", Source, StringComparison.Ordinal);
        Assert.DoesNotContain("hasComparablePureStateSnapshot", Source, StringComparison.Ordinal);
    }

    [Fact]
    public void FullStateRecoveryUsesSingleFlightAndBoundedAutomaticTimeout()
    {
        Assert.Contains("private readonly object _fullStateSyncGate = new object();", BattleHandleSource, StringComparison.Ordinal);
        Assert.Contains("private Task<ShooterGatewayFullStateSyncRequestResult>? _fullStateSyncInFlight;", BattleHandleSource, StringComparison.Ordinal);
        Assert.Contains("if (_fullStateSyncInFlight != null)", BattleHandleSource, StringComparison.Ordinal);
        Assert.Contains("return _fullStateSyncInFlight;", BattleHandleSource, StringComparison.Ordinal);
        Assert.Contains("_fullStateSyncInFlight = completion.Task;", BattleHandleSource, StringComparison.Ordinal);
        Assert.Contains("if (ReferenceEquals(_fullStateSyncInFlight, completion.Task))", BattleHandleSource, StringComparison.Ordinal);
        Assert.Contains("_fullStateSyncInFlight = null;", BattleHandleSource, StringComparison.Ordinal);
        Assert.Contains("AutomaticFullStateSyncTimeout = TimeSpan.FromSeconds(10);", BattleDataPlaneSource, StringComparison.Ordinal);
        Assert.Contains("RequestFullSnapshotResyncIfNeededAsync(AutomaticFullStateSyncTimeout)", BattleDataPlaneSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PureStateComparableHashRequiresSuccessfulSameFrameNonZeroEvidence()
    {
        var push = CreatePureStatePush(ShooterSnapshotApplyResult.AppliedActorSnapshot, frame: 113, stateHash: 0x155AE66Fu);

        var captured = ShooterSmokeClientProcessRunner.TryCapturePureStateComparableHash(
            in push,
            appliedFrame: 113,
            appliedStateHash: 0x155AE66Fu,
            out var clientHash);

        Assert.True(captured);
        Assert.Equal(0x155AE66Fu, clientHash);
    }

    [Fact]
    public void PureStateComparableHashRejectsNonComparablePushes()
    {
        var ignored = CreatePureStatePush(ShooterSnapshotApplyResult.IgnoredStaleSnapshot, frame: 91, stateHash: 0x11111111u);
        var wrongFrame = CreatePureStatePush(ShooterSnapshotApplyResult.AppliedActorSnapshot, frame: 91, stateHash: 0x11111111u);
        var missingAuthoritativeHash = CreatePureStatePush(ShooterSnapshotApplyResult.AppliedActorSnapshot, frame: 113, stateHash: 0u);
        var valid = CreatePureStatePush(ShooterSnapshotApplyResult.AppliedActorSnapshot, frame: 113, stateHash: 0x155AE66Fu);

        Assert.False(ShooterSmokeClientProcessRunner.TryCapturePureStateComparableHash(in ignored, 113, 0x155AE66Fu, out var ignoredHash));
        Assert.False(ShooterSmokeClientProcessRunner.TryCapturePureStateComparableHash(in wrongFrame, 113, 0x155AE66Fu, out var wrongFrameHash));
        Assert.False(ShooterSmokeClientProcessRunner.TryCapturePureStateComparableHash(in missingAuthoritativeHash, 113, 0x155AE66Fu, out var missingAuthoritativeHashResult));
        Assert.False(ShooterSmokeClientProcessRunner.TryCapturePureStateComparableHash(in valid, 113, 0u, out var missingClientHash));
        Assert.Equal(0u, ignoredHash);
        Assert.Equal(0u, wrongFrameHash);
        Assert.Equal(0u, missingAuthoritativeHashResult);
        Assert.Equal(0u, missingClientHash);
    }

    private static ShooterSnapshotPushSmokeResult CreatePureStatePush(
        ShooterSnapshotApplyResult applyResult,
        int frame,
        uint stateHash)
    {
        return new ShooterSnapshotPushSmokeResult(
            ApplyResult: applyResult,
            WireWorldId: 42UL,
            WireFrame: frame,
            WireServerTicks: frame,
            PayloadOpCode: 5207,
            PackedWorldId: 42UL,
            PackedFrame: frame,
            PackedServerTick: frame,
            PackedStateHash: stateHash,
            PackedEntityCount: 2,
            MatchState: 0,
            MatchFinal: false,
            MatchVictory: false,
            MatchCompletedFrame: 0,
            DefeatedEnemies: 0,
            VictoryTargetDefeats: 0,
            TimeLimitFrames: 0,
            RemainingTimeFrames: 0);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static string GetSourcePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "AbilityKit.Orleans.sln");
            if (File.Exists(solutionPath))
            {
                return Path.Combine(
                    directory.FullName,
                    "src",
                    "AbilityKit.Orleans.ShooterSmoke",
                    "Runner",
                    "ShooterSmokeClientProcessRunner.cs");
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Orleans workspace from the test output directory.");
    }

    private static string GetUnitySourcePath(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var packageRoot = Path.Combine(
                directory.FullName,
                "Unity",
                "Packages",
                "com.abilitykit.demo.shooter.view.runtime");
            if (Directory.Exists(packageRoot))
            {
                return Path.Combine(new[] { packageRoot }.Concat(segments).ToArray());
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Shooter view runtime package from the test output directory.");
    }
}
