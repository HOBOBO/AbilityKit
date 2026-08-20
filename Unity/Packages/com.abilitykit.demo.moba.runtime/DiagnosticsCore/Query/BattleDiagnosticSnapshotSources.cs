namespace AbilityKit.Demo.Moba.Diagnostics
{
    public interface IBattleDiagnosticEventSnapshotSource
    {
        BattleDiagnosticSessionScope Scope { get; }
        BattleDiagnosticEventTrackSnapshot CaptureEventSnapshot();
    }

    public interface IBattleDiagnosticStateSnapshotSource
    {
        BattleDiagnosticSessionScope Scope { get; }
        BattleDiagnosticStateTrackSnapshot CaptureStateSnapshot();
    }

    public interface IBattleDiagnosticAttributeSnapshotSource
    {
        BattleDiagnosticSessionScope Scope { get; }
        BattleDiagnosticAttributeTrackSnapshot CaptureAttributeSnapshot();
    }

    public interface IBattleDiagnosticBuffSnapshotSource
    {
        BattleDiagnosticSessionScope Scope { get; }
        BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorBuff> CaptureBuffSnapshot();
    }

    public interface IBattleDiagnosticTagSnapshotSource
    {
        BattleDiagnosticSessionScope Scope { get; }
        BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorTag> CaptureTagSnapshot();
    }

    public interface IBattleDiagnosticEffectSnapshotSource
    {
        BattleDiagnosticSessionScope Scope { get; }
        BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorEffect> CaptureEffectSnapshot();
    }

    public interface IBattleDiagnosticTraceSnapshotSource
    {
        BattleDiagnosticSessionScope Scope { get; }
        BattleDiagnosticTraceTrackSnapshot CaptureTraceSnapshot();
    }

    public interface IBattleDiagnosticMetricSnapshotSource
    {
        BattleDiagnosticSessionScope Scope { get; }
        BattleDiagnosticMetricTrackSnapshot CaptureMetricSnapshot();
    }
}
