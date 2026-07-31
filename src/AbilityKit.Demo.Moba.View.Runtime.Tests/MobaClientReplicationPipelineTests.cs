using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class MobaClientReplicationPipelineTests
{
    [Fact]
    public void Pipeline_RecordsInputAcknowledgementAndObservedSnapshot()
    {
        var strategy = new RecordingStrategy();
        var pipeline = new MobaClientReplicationPipeline(strategy);
        var input = new PlayerInputCommand(new FrameIndex(12), new PlayerId("p1"), 7, Array.Empty<byte>());
        var sample = new MobaRemoteSnapshotSample(9, 18, Array.Empty<GatewayStateSyncActorSnapshot>());

        pipeline.SubmitInput(in input);
        pipeline.AcknowledgeInput(10);
        pipeline.ObserveRemote(in sample);
        pipeline.Tick(1f / 30f);

        var diagnostics = pipeline.GetDiagnostics();
        Assert.Equal(NetworkSyncModel.AuthoritativeInterpolation, diagnostics.SyncModel);
        Assert.Equal(12, diagnostics.LastSubmittedFrame);
        Assert.Equal(10, diagnostics.LastAcknowledgedFrame);
        Assert.Equal(18, diagnostics.LastObservedFrame);
        Assert.Equal(1, diagnostics.SubmittedInputCount);
        Assert.Equal(1, diagnostics.ObservedSnapshotCount);
        Assert.Equal(2, diagnostics.UnacknowledgedInputFrames);
        Assert.Equal(1, strategy.TickCount);
    }

    [Fact]
    public void ResetDiagnostics_ClearsPipelineStateWithoutResettingStrategy()
    {
        var strategy = new RecordingStrategy();
        var pipeline = new MobaClientReplicationPipeline(strategy);
        var input = new PlayerInputCommand(new FrameIndex(12), new PlayerId("p1"), 7, Array.Empty<byte>());
        var sample = new MobaRemoteSnapshotSample(9, 18, Array.Empty<GatewayStateSyncActorSnapshot>());

        pipeline.SubmitInput(in input);
        pipeline.AcknowledgeInput(10);
        pipeline.ObserveRemote(in sample);
        pipeline.Tick(1f / 30f);
        pipeline.ResetDiagnostics();

        var diagnostics = pipeline.GetDiagnostics();
        Assert.Equal(0, diagnostics.LastSubmittedFrame);
        Assert.Equal(0, diagnostics.LastAcknowledgedFrame);
        Assert.Equal(0, diagnostics.LastObservedFrame);
        Assert.Equal(0, diagnostics.SubmittedInputCount);
        Assert.Equal(0, diagnostics.ObservedSnapshotCount);
        Assert.Equal(0, diagnostics.UnacknowledgedInputFrames);
        Assert.Equal(0, diagnostics.Health.EventCount);
        Assert.Equal(1, strategy.TickCount);
    }

    [Fact]
    public void Pipeline_PublishesFrameworkHealthEventsForNetworkAndRecoveryEdges()
    {
        var strategy = new RecordingStrategy();
        var pipeline = new MobaClientReplicationPipeline(strategy);
        var input = new PlayerInputCommand(new FrameIndex(10), new PlayerId("p1"), 7, Array.Empty<byte>());

        pipeline.SubmitInput(in input);
        pipeline.AcknowledgeInput(8);
        pipeline.ObserveRemote(new MobaRemoteSnapshotSample(1, 20, Array.Empty<GatewayStateSyncActorSnapshot>()));
        pipeline.ObserveRemote(new MobaRemoteSnapshotSample(1, 23, Array.Empty<GatewayStateSyncActorSnapshot>()));
        pipeline.ObserveRemote(new MobaRemoteSnapshotSample(1, 22, Array.Empty<GatewayStateSyncActorSnapshot>()));

        strategy.EnqueueReport(new SyncReconciliationReport(
            SyncReconciliationReason.AuthoritativeHashMismatch,
            SyncRecoveryState.CatchUp,
            needsFullSnapshot: false,
            clientFrame: 24,
            authoritativeFrame: 20,
            clientStateHash: 1u,
            authoritativeStateHash: 2u,
            replayTicks: 2));
        strategy.EnqueueReport(new SyncReconciliationReport(
            SyncReconciliationReason.None,
            SyncRecoveryState.Recovered,
            needsFullSnapshot: false,
            clientFrame: 24,
            authoritativeFrame: 20,
            clientStateHash: 1u,
            authoritativeStateHash: 2u,
            replayTicks: 4));
        pipeline.Tick(1f / 30f);
        pipeline.Tick(1f / 30f);

        var diagnostics = pipeline.GetDiagnostics();
        Assert.Equal(1, GetHealthCount(diagnostics.Health, SyncHealthEventKind.InputAccepted));
        Assert.Equal(2, GetHealthCount(diagnostics.Health, SyncHealthEventKind.SnapshotReceived));
        Assert.Equal(1, GetHealthCount(diagnostics.Health, SyncHealthEventKind.SnapshotGap));
        Assert.Equal(1, GetHealthCount(diagnostics.Health, SyncHealthEventKind.SnapshotStale));
        Assert.Equal(1, GetHealthCount(diagnostics.Health, SyncHealthEventKind.RollbackStarted));
        Assert.Equal(1, GetHealthCount(diagnostics.Health, SyncHealthEventKind.ReplayCompleted));
        Assert.Equal(23, diagnostics.LastObservedFrame);
    }

    private static long GetHealthCount(SyncHealthReport report, SyncHealthEventKind kind)
    {
        return report.Kinds.Single(summary => summary.Kind == kind).Count;
    }

    private sealed class RecordingStrategy : IClientSyncStrategy<PlayerInputCommand, MobaRemoteSnapshotSample>
    {
        private readonly Queue<SyncReconciliationReport> _reports = new();

        public int TickCount { get; private set; }
        public NetworkSyncModel SyncModel => NetworkSyncModel.AuthoritativeInterpolation;
        public bool IsStarted => TickCount > 0;
        public int CurrentFrame => 0;

        public void EnqueueReport(SyncReconciliationReport report) => _reports.Enqueue(report);

        public SyncTickResult Tick(float deltaSeconds)
        {
            TickCount++;
            return new SyncTickResult(0, 0, 0u);
        }

        public void SubmitInput(in PlayerInputCommand input) { }
        public void ObserveRemote(in MobaRemoteSnapshotSample sample) { }
        public SyncReconciliationReport GetReconciliationReport() =>
            _reports.Count > 0 ? _reports.Dequeue() : SyncReconciliationReport.None;
    }
}
