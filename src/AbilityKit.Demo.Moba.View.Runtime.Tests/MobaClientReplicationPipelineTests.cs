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
        Assert.Equal(1, strategy.TickCount);
    }

    private sealed class RecordingStrategy : IClientSyncStrategy<PlayerInputCommand, MobaRemoteSnapshotSample>
    {
        public int TickCount { get; private set; }
        public NetworkSyncModel SyncModel => NetworkSyncModel.AuthoritativeInterpolation;
        public bool IsStarted => TickCount > 0;
        public int CurrentFrame => 0;

        public SyncTickResult Tick(float deltaSeconds)
        {
            TickCount++;
            return new SyncTickResult(0, 0, 0u);
        }

        public void SubmitInput(in PlayerInputCommand input) { }
        public void ObserveRemote(in MobaRemoteSnapshotSample sample) { }
        public SyncReconciliationReport GetReconciliationReport() => SyncReconciliationReport.None;
    }
}
