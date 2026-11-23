#nullable enable

using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Protocol.Moba.StateSync;

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

        internal bool IsExpectedWorld(ulong worldId) => _admission.IsExpectedWorld(worldId);

        public void Reset(ulong activeWorldId)
        {
            _admission.Reset(activeWorldId);
        }

        public void RequireFullBaseline()
        {
            _admission.RequireFullBaseline();
        }

        /// <summary>
        /// 固定基线已在重建层验证；世界应用前只判定准入，不移动游标。
        /// </summary>
        internal MobaSnapshotAdmissionResult CheckFixed(in GatewayStateSyncSnapshot snapshot) => Map(
            _admission.Check(snapshot.WorldId, snapshot.Frame, snapshot.IsFullSnapshot, ResolveAdmissionSchema(in snapshot),
                explicitBaselineAndOrderValidated: true));

        /// <summary>
        /// 在世界应用成功后，提交通过检查的固定基线快照。
        /// </summary>
        internal MobaSnapshotAdmissionResult CommitFixed(in GatewayStateSyncSnapshot snapshot) => Admit(
            snapshot.WorldId, snapshot.Frame, snapshot.IsFullSnapshot, ResolveAdmissionSchema(in snapshot),
            explicitBaselineAndOrderValidated: true);

        internal static int ResolveAdmissionSchema(in GatewayStateSyncSnapshot snapshot) =>
            snapshot.PresentationCueBaseline is { Length: > 0 } &&
            snapshot.PayloadOpCode == MobaCharacterHfsmWireCodec.CompressedOpCode &&
            snapshot.SchemaVersion == MobaCharacterHfsmWireCodec.CompressedSchemaVersion
                ? MobaStateSyncCuePayloadCodec.CarrierSchemaVersion
                : snapshot.SchemaVersion;

        public MobaSnapshotAdmissionResult Admit(
            ulong worldId,
            int frame,
            bool isFullSnapshot,
            int schemaVersion = 0,
            bool explicitBaselineAndOrderValidated = false)
        {
            return Map(_admission.Admit(worldId, frame, isFullSnapshot, schemaVersion, explicitBaselineAndOrderValidated));
        }

        private static MobaSnapshotAdmissionResult Map(AuthoritativeSnapshotAdmissionResult result)
        {
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
