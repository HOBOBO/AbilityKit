#nullable enable

using AbilityKit.Demo.Shooter.View.Hosting;
using AbilityKit.Network.Runtime.Sync;

namespace AbilityKit.Demo.Shooter.View.PlayMode
{
    internal static class ShooterRemotePresentationFrameBuilder
    {
        public static ShooterHostPresentationFrame Build(
            ShooterClientNetworkLaunchResult launch,
            ShooterPlayModeSessionOptions options,
            int controlledPlayerId,
            SyncTimeAnchor remoteTimeAnchor,
            SyncTimeAnchor localTimeAnchor,
            ShooterRemoteLatencyCompensationDiagnostics remoteLatencyCompensationDiagnostics)
        {
            var session = launch.Session;
            var presentation = session.Presentation;
            var lastPushResult = launch.GatewayConnection.LastPushResult;

            // 姿态连续性探针：吃最终合成批，量化远端拉扯与己方回拉（无头排查用）。
            ShooterRemotePoseContinuityProbe.Configure(controlledPlayerId);
            ShooterRemotePoseContinuityProbe.Observe(presentation.RenderBatch);
            ShooterRemotePoseContinuityProbe.TryDriveSustainedInput(session, controlledPlayerId);

            return new ShooterHostPresentationFrame(
                presentation.RenderBatch,
                ShooterSnapshotViewBatch.Empty,
                false,
                controlledPlayerId,
                options.WorldScale,
                null,
                lastPushResult,
                remoteTimeAnchor,
                localTimeAnchor,
                null,
                null,
                remoteLatencyCompensationDiagnostics,
                ShooterCrossLayerDiagnostics.From(
                    session.FrameworkSnapshotPipelineDiagnostics,
                    lastPushResult,
                    remoteLatencyCompensationDiagnostics,
                    presentation.NeedsPureStateFullBaselineResync,
                    presentation.LastPureStateAppliedFrame,
                    presentation.LastPureStateResyncFrame),
                presentation.LastPureStateSyncDiagnostics,
                presentation.NeedsPureStateFullBaselineResync,
                presentation.LastPureStateResyncReason,
                presentation.LastPureStateAppliedFrame,
                presentation.LastPureStateAppliedStateHash,
                presentation.LastPureStateResyncFrame,
                presentation.LastPureStateResyncStateHash);
        }
    }
}
