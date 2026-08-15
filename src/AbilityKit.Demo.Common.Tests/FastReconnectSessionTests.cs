using System;
using System.Collections.Generic;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Demo.Harness;
using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class FastReconnectSessionTests
{
    [Fact]
    public void NewSession_StartsConnectedAtFrameZero()
    {
        var session = new FastReconnectSession(resumeWindowFrames: 16);

        Assert.Equal(FastReconnectPhase.Connected, session.Phase);
        Assert.Equal(0, session.LastAckedServerFrame);
        Assert.Equal(0, session.PendingGapFrames);
        Assert.Equal(16, session.ResumeWindowFrames);
    }

    [Fact]
    public void NonPositiveResumeWindow_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FastReconnectSession(resumeWindowFrames: 0));
    }

    [Fact]
    public void ObserveServerFrame_AcksFrameAndReportsSnapshotReceived()
    {
        var session = new FastReconnectSession();

        var report = session.ObserveServerFrame(12);

        Assert.Equal(FastReconnectPhase.Connected, report.Phase);
        Assert.Equal(12, session.LastAckedServerFrame);
        Assert.False(report.Reconciliation.DidReconcile);
        var healthEvent = Assert.Single(report.HealthEvents);
        Assert.Equal(SyncHealthEventKind.SnapshotReceived, healthEvent.Kind);
        Assert.Equal(SyncHealthSeverity.Info, healthEvent.Severity);
        Assert.Equal(12, healthEvent.Frame);
    }

    [Fact]
    public void ObserveServerFrame_RejectsBackwardsFrame()
    {
        var session = new FastReconnectSession();
        session.ObserveServerFrame(20);

        Assert.Throws<ArgumentOutOfRangeException>(() => session.ObserveServerFrame(19));
    }

    [Fact]
    public void Disconnect_StarvesInterpolationAndReportsTimeout()
    {
        var session = new FastReconnectSession();
        session.ObserveServerFrame(8);

        var report = session.Disconnect();

        Assert.Equal(FastReconnectPhase.Disconnected, report.Phase);
        Assert.Equal(SyncReconciliationReason.SnapshotTimeout, report.Reconciliation.Reason);
        var healthEvent = Assert.Single(report.HealthEvents);
        Assert.Equal(SyncHealthEventKind.InterpolationStarved, healthEvent.Kind);
        Assert.Equal(SyncHealthSeverity.Warning, healthEvent.Severity);
        Assert.Equal(8, healthEvent.Frame);
    }

    [Fact]
    public void Disconnect_WhenAlreadyDisconnected_Throws()
    {
        var session = new FastReconnectSession();
        session.Disconnect();

        Assert.Throws<InvalidOperationException>(() => session.Disconnect());
    }

    [Fact]
    public void SmallGap_ResumesWithinWindow()
    {
        var session = new FastReconnectSession(resumeWindowFrames: 32);
        session.ObserveServerFrame(100);
        session.Disconnect();

        var report = session.Reconnect(currentServerFrame: 120);

        Assert.Equal(FastReconnectPhase.Resuming, session.Phase);
        Assert.Equal(20, session.PendingGapFrames);
        Assert.Equal(SyncRecoveryState.CatchUp, report.Reconciliation.RecoveryState);
        Assert.False(report.Reconciliation.NeedsFullSnapshot);
        Assert.Equal(20, report.Reconciliation.ReplayTicks);

        var gapEvent = Assert.Single(report.HealthEvents);
        Assert.Equal(SyncHealthEventKind.SnapshotGap, gapEvent.Kind);
        Assert.Equal(20L, gapEvent.Value);
        Assert.Equal(120, gapEvent.Frame);
    }

    [Fact]
    public void LargeGap_RequestsFullSnapshot()
    {
        var session = new FastReconnectSession(resumeWindowFrames: 32);
        session.ObserveServerFrame(100);
        session.Disconnect();

        var report = session.Reconnect(currentServerFrame: 200);

        Assert.Equal(FastReconnectPhase.AwaitingFullSnapshot, session.Phase);
        Assert.Equal(100, session.PendingGapFrames);
        Assert.True(report.Reconciliation.NeedsFullSnapshot);
        Assert.Equal(SyncRecoveryState.AwaitingFullSnapshot, report.Reconciliation.RecoveryState);

        Assert.Equal(2, report.HealthEvents.Count);
        Assert.Equal(SyncHealthEventKind.SnapshotGap, report.HealthEvents[0].Kind);
        Assert.Equal(SyncHealthEventKind.FullSnapshotRequested, report.HealthEvents[1].Kind);
        Assert.Equal(100L, report.HealthEvents[1].Value);
    }

    [Fact]
    public void Reconnect_RequiresDisconnectedState()
    {
        var session = new FastReconnectSession();
        session.ObserveServerFrame(10);

        Assert.Throws<InvalidOperationException>(() => session.Reconnect(20));
    }

    [Fact]
    public void GapAtWindowBoundary_PrefersResume()
    {
        var session = new FastReconnectSession(resumeWindowFrames: 32);
        session.ObserveServerFrame(0);
        session.Disconnect();

        session.Reconnect(currentServerFrame: 32);

        Assert.Equal(FastReconnectPhase.Resuming, session.Phase);
    }

    [Fact]
    public void CompleteRecovery_AfterResume_RecoversWithoutSnapshotApply()
    {
        var session = new FastReconnectSession(resumeWindowFrames: 32);
        session.ObserveServerFrame(100);
        session.Disconnect();
        session.Reconnect(currentServerFrame: 120);

        var report = session.CompleteRecovery();

        Assert.Equal(FastReconnectPhase.Recovered, session.Phase);
        Assert.Equal(120, session.LastAckedServerFrame);
        Assert.Equal(0, session.PendingGapFrames);
        Assert.Equal(SyncRecoveryState.Recovered, report.Reconciliation.RecoveryState);
        Assert.Equal(20, report.Reconciliation.ReplayTicks);

        var recovered = Assert.Single(report.HealthEvents);
        Assert.Equal(SyncHealthEventKind.InterpolationRecovered, recovered.Kind);
        Assert.Equal(20L, recovered.Value);
    }

    [Fact]
    public void CompleteRecovery_AfterFullSnapshot_AppliesSnapshotThenRecovers()
    {
        var session = new FastReconnectSession(resumeWindowFrames: 32);
        session.ObserveServerFrame(100);
        session.Disconnect();
        session.Reconnect(currentServerFrame: 300);

        var report = session.CompleteRecovery();

        Assert.Equal(FastReconnectPhase.Recovered, session.Phase);
        Assert.Equal(300, session.LastAckedServerFrame);
        Assert.Equal(0, report.Reconciliation.ReplayTicks);

        Assert.Equal(2, report.HealthEvents.Count);
        Assert.Equal(SyncHealthEventKind.FullSnapshotApplied, report.HealthEvents[0].Kind);
        Assert.Equal(SyncHealthEventKind.InterpolationRecovered, report.HealthEvents[1].Kind);
    }

    [Fact]
    public void CompleteRecovery_WithoutPendingReconnect_Throws()
    {
        var session = new FastReconnectSession();
        session.ObserveServerFrame(10);

        Assert.Throws<InvalidOperationException>(() => session.CompleteRecovery());
    }

    [Fact]
    public void Session_SupportsConsecutiveReconnects()
    {
        var session = new FastReconnectSession(resumeWindowFrames: 32);
        session.ObserveServerFrame(100);

        session.Disconnect();
        session.Reconnect(currentServerFrame: 110);
        session.CompleteRecovery();

        // Continue acking after recovery, then a second outage.
        session.ObserveServerFrame(150);
        session.Disconnect();
        var second = session.Reconnect(currentServerFrame: 400);

        Assert.True(second.Reconciliation.NeedsFullSnapshot);
        session.CompleteRecovery();
        Assert.Equal(400, session.LastAckedServerFrame);
    }
}

/// <summary>
/// End-to-end proof that the <see cref="NetworkSyncModel.FastReconnect"/> capability runs through the
/// demo harness: profile selection, capability declaration, time-anchor stepping and SyncHealthEvent
/// aggregation all close the loop without the carrier re-implementing recovery logic.
/// </summary>
public sealed class FastReconnectDemoHarnessTests
{
    [Fact]
    public void HarnessRun_AggregatesReconnectHealthEvents()
    {
        // 8 steps: observe, observe, disconnect, reconnect(large gap), complete, observe, observe, observe.
        var carrier = new FastReconnectCarrier(resumeWindowFrames: 32, disconnectAtStep: 2, reconnectGap: 200);
        var scenario = new DemoHarnessScenario(
            "FastReconnect",
            NetworkSyncProfiles.FastReconnect,
            NetworkConditionProfile.PoorWifi,
            carrierName: "FastReconnect",
            stepCount: 8,
            deltaSeconds: 0.05f);

        var result = new DemoHarnessRunner().Run(in scenario, carrier);

        Assert.Equal(DemoHarnessRunStatus.Completed, result.Status);
        Assert.Equal(8, result.Metrics.StepsRun);

        // SnapshotReceived x5 (info) + InterpolationStarved (warn) + SnapshotGap (warn)
        //  + FullSnapshotRequested (info) + FullSnapshotApplied (info) + InterpolationRecovered (info)
        Assert.Equal(10, result.Metrics.HealthEventCount);
        Assert.Equal(2, result.Metrics.HealthWarningCount);
        Assert.Equal(0, result.Metrics.HealthErrorCount);

        // The full-snapshot recovery path was exercised.
        Assert.True(result.Metrics.FullSnapshotRequestCount >= 1);
    }

    [Fact]
    public void HarnessRun_SmallGap_TakesResumePath()
    {
        var carrier = new FastReconnectCarrier(resumeWindowFrames: 32, disconnectAtStep: 1, reconnectGap: 10);
        var scenario = new DemoHarnessScenario(
            "FastReconnect",
            NetworkSyncProfiles.FastReconnect,
            NetworkConditionProfile.Lan,
            carrierName: "FastReconnect",
            stepCount: 5,
            deltaSeconds: 0.05f);

        var result = new DemoHarnessRunner().Run(in scenario, carrier);

        Assert.Equal(DemoHarnessRunStatus.Completed, result.Status);
        // Resume path never needs a full snapshot.
        Assert.Equal(0, result.Metrics.FullSnapshotRequestCount);
        // InterpolationStarved (disconnect) + SnapshotGap (resume reconnect).
        Assert.Equal(2, result.Metrics.HealthWarningCount);
    }

    [Fact]
    public void Carrier_DeclaresUnsupportedForNonReconnectProfile()
    {
        var carrier = new FastReconnectCarrier(resumeWindowFrames: 32, disconnectAtStep: 1, reconnectGap: 10);

        var capability = carrier.Supports(NetworkSyncProfiles.Lockstep, NetworkConditionProfile.Ideal);

        Assert.Equal(SyncDemoCapabilityStatus.Unsupported, capability.Status);
        Assert.False(capability.CanRun);
    }

    /// <summary>
    /// A minimal real integrator: drives a <see cref="FastReconnectSession"/> on a deterministic
    /// disconnect/reconnect schedule and forwards the session's reconciliation + health events into the
    /// harness step telemetry. This is all an integrator must write to demonstrate the capability.
    /// </summary>
    private sealed class FastReconnectCarrier : ISyncDemoCarrier, ISyncDemoCarrierCapabilities
    {
        private readonly FastReconnectSession _session;
        private readonly int _disconnectAtStep;
        private readonly int _reconnectGap;
        private int _serverFrame;

        public FastReconnectCarrier(int resumeWindowFrames, int disconnectAtStep, int reconnectGap)
        {
            _session = new FastReconnectSession(resumeWindowFrames);
            _disconnectAtStep = disconnectAtStep;
            _reconnectGap = reconnectGap;
        }

        public string CarrierName => "FastReconnect";

        public NetworkSyncModel SyncModel => NetworkSyncModel.FastReconnect;

        public SyncDemoCapabilityResult Supports(in NetworkSyncProfile profile, in NetworkConditionProfile networkProfile)
        {
            if ((profile.Recovery & RecoveryPolicy.ReconnectResume) == 0)
            {
                return SyncDemoCapabilityResult.Unsupported(
                    $"FastReconnect carrier requires RecoveryPolicy.ReconnectResume, profile '{profile.CompatibilityModel}' lacks it.");
            }

            return SyncDemoCapabilityResult.Supported;
        }

        public DemoHarnessStepTelemetry Step(in DemoHarnessStepContext context)
        {
            FastReconnectStepReport report;
            if (context.StepIndex == _disconnectAtStep)
            {
                report = _session.Disconnect();
            }
            else if (context.StepIndex == _disconnectAtStep + 1)
            {
                report = _session.Reconnect(_serverFrame + _reconnectGap);
                _serverFrame += _reconnectGap;
            }
            else if (context.StepIndex == _disconnectAtStep + 2 &&
                     (_session.Phase == FastReconnectPhase.Resuming || _session.Phase == FastReconnectPhase.AwaitingFullSnapshot))
            {
                report = _session.CompleteRecovery();
            }
            else
            {
                _serverFrame += 1;
                report = _session.ObserveServerFrame(_serverFrame);
            }

            var events = report.HealthEvents;
            var buffer = new SyncHealthEvent[events.Count];
            for (var i = 0; i < events.Count; i++)
            {
                buffer[i] = events[i];
            }

            return new DemoHarnessStepTelemetry(
                tickResult: new SyncTickResult(1, report.Reconciliation.AuthoritativeFrame, 0u),
                reconciliationReport: report.Reconciliation,
                networkStats: default,
                remoteJitter: 0d,
                acceptedHits: 0,
                rejectedHits: 0,
                buffer);
        }
    }
}
