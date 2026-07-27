#nullable enable

using AbilityKit.Network.Runtime.Sync;

namespace AbilityKit.Game.Battle.Agent
{
    public enum MobaSnapshotAdmissionStatus
    {
        Accepted = 0,
        WrongWorld = 1,
        BaselineRequired = 2,
        StaleOrDuplicate = 3,
        FrameGapTooLarge = 4,
        UnsupportedSchemaVersion = 5
    }

    public readonly struct MobaSnapshotAdmissionResult
    {
        public readonly MobaSnapshotAdmissionStatus Status;
        public readonly int LastAcceptedFrame;
        public readonly bool ShouldRequestFullResync;

        public MobaSnapshotAdmissionResult(
            MobaSnapshotAdmissionStatus status,
            int lastAcceptedFrame,
            bool shouldRequestFullResync)
        {
            Status = status;
            LastAcceptedFrame = lastAcceptedFrame;
            ShouldRequestFullResync = shouldRequestFullResync;
        }

        public bool Accepted => Status == MobaSnapshotAdmissionStatus.Accepted;
    }

    /// <summary>
    /// Validates the authoritative snapshot stream before state import or interpolation.
    /// A full snapshot establishes a new baseline; deltas may only advance that baseline.
    /// </summary>
    public sealed class MobaSnapshotAdmission
    {
        public const int DefaultMaxDeltaFrameGap = AuthoritativeSnapshotAdmission.DefaultMaxDeltaFrameGap;

        private readonly AuthoritativeSnapshotAdmission _admission;

        public MobaSnapshotAdmission(int maxDeltaFrameGap = DefaultMaxDeltaFrameGap)
        {
            _admission = new AuthoritativeSnapshotAdmission(
                maxDeltaFrameGap,
                minSchemaVersion: 0,
                maxSchemaVersion: GatewayStateSyncSnapshot.CurrentSchemaVersion);
        }

        public bool HasBaseline => _admission.HasBaseline;
        public int LastAcceptedFrame => _admission.LastAcceptedFrame;

        public void Reset(ulong activeWorldId)
        {
            _admission.Reset(activeWorldId);
        }

        public void RequireFullBaseline()
        {
            _admission.RequireFullBaseline();
        }

        public MobaSnapshotAdmissionResult Admit(
            ulong worldId,
            int frame,
            bool isFullSnapshot,
            int schemaVersion = 0)
        {
            var result = _admission.Admit(worldId, frame, isFullSnapshot, schemaVersion);
            return new MobaSnapshotAdmissionResult(
                MapStatus(result.Status),
                result.LastAcceptedFrame,
                result.ShouldRequestFullResync);
        }

        private static MobaSnapshotAdmissionStatus MapStatus(AuthoritativeSnapshotAdmissionStatus status)
        {
            return status switch
            {
                AuthoritativeSnapshotAdmissionStatus.Accepted => MobaSnapshotAdmissionStatus.Accepted,
                AuthoritativeSnapshotAdmissionStatus.WrongWorld => MobaSnapshotAdmissionStatus.WrongWorld,
                AuthoritativeSnapshotAdmissionStatus.BaselineRequired => MobaSnapshotAdmissionStatus.BaselineRequired,
                AuthoritativeSnapshotAdmissionStatus.StaleOrDuplicate => MobaSnapshotAdmissionStatus.StaleOrDuplicate,
                AuthoritativeSnapshotAdmissionStatus.FrameGapTooLarge => MobaSnapshotAdmissionStatus.FrameGapTooLarge,
                AuthoritativeSnapshotAdmissionStatus.UnsupportedSchemaVersion => MobaSnapshotAdmissionStatus.UnsupportedSchemaVersion,
                _ => MobaSnapshotAdmissionStatus.StaleOrDuplicate
            };
        }
    }
}
