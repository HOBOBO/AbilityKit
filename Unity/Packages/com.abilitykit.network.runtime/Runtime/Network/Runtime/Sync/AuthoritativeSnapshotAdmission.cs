#nullable enable

namespace AbilityKit.Network.Runtime.Sync
{
    public enum AuthoritativeSnapshotAdmissionStatus
    {
        Accepted = 0,
        WrongWorld = 1,
        BaselineRequired = 2,
        StaleOrDuplicate = 3,
        FrameGapTooLarge = 4,
        UnsupportedSchemaVersion = 5
    }

    public readonly struct AuthoritativeSnapshotAdmissionResult
    {
        public AuthoritativeSnapshotAdmissionResult(
            AuthoritativeSnapshotAdmissionStatus status,
            int lastAcceptedFrame,
            bool shouldRequestFullResync)
        {
            Status = status;
            LastAcceptedFrame = lastAcceptedFrame;
            ShouldRequestFullResync = shouldRequestFullResync;
        }

        public AuthoritativeSnapshotAdmissionStatus Status { get; }

        public int LastAcceptedFrame { get; }

        public bool ShouldRequestFullResync { get; }

        public bool Accepted => Status == AuthoritativeSnapshotAdmissionStatus.Accepted;
    }

    /// <summary>
    /// Validates an authoritative snapshot stream before import or interpolation.
    /// A full snapshot establishes a baseline; deltas may only advance that baseline.
    /// </summary>
    public sealed class AuthoritativeSnapshotAdmission
    {
        public const int DefaultMaxDeltaFrameGap = 120;

        private readonly int _maxDeltaFrameGap;
        private readonly int _minSchemaVersion;
        private readonly int _maxSchemaVersion;
        private ulong _activeWorldId;
        private bool _hasBaseline;
        private int _lastAcceptedFrame;

        public AuthoritativeSnapshotAdmission(
            int maxDeltaFrameGap = DefaultMaxDeltaFrameGap,
            int minSchemaVersion = 0,
            int maxSchemaVersion = int.MaxValue)
        {
            _maxDeltaFrameGap = maxDeltaFrameGap > 0
                ? maxDeltaFrameGap
                : DefaultMaxDeltaFrameGap;
            _minSchemaVersion = minSchemaVersion;
            _maxSchemaVersion = maxSchemaVersion >= minSchemaVersion
                ? maxSchemaVersion
                : minSchemaVersion;
        }

        public bool HasBaseline => _hasBaseline;

        public int LastAcceptedFrame => _lastAcceptedFrame;

        public void Reset(ulong activeWorldId)
        {
            _activeWorldId = activeWorldId;
            _hasBaseline = false;
            _lastAcceptedFrame = 0;
        }

        public void RequireFullBaseline()
        {
            _hasBaseline = false;
        }

        public AuthoritativeSnapshotAdmissionResult Admit(
            ulong worldId,
            int frame,
            bool isFullSnapshot,
            int schemaVersion = 0)
        {
            if (_activeWorldId == 0 || worldId != _activeWorldId)
            {
                return Reject(AuthoritativeSnapshotAdmissionStatus.WrongWorld, false);
            }

            if (schemaVersion < _minSchemaVersion || schemaVersion > _maxSchemaVersion)
            {
                _hasBaseline = false;
                return Reject(AuthoritativeSnapshotAdmissionStatus.UnsupportedSchemaVersion, true);
            }

            if (frame < 0)
            {
                return Reject(AuthoritativeSnapshotAdmissionStatus.StaleOrDuplicate, false);
            }

            if (isFullSnapshot)
            {
                if (_hasBaseline && frame <= _lastAcceptedFrame)
                {
                    return Reject(AuthoritativeSnapshotAdmissionStatus.StaleOrDuplicate, false);
                }

                _hasBaseline = true;
                _lastAcceptedFrame = frame;
                return Accept();
            }

            if (!_hasBaseline)
            {
                return Reject(AuthoritativeSnapshotAdmissionStatus.BaselineRequired, true);
            }

            if (frame <= _lastAcceptedFrame)
            {
                return Reject(AuthoritativeSnapshotAdmissionStatus.StaleOrDuplicate, false);
            }

            if ((long)frame - _lastAcceptedFrame > _maxDeltaFrameGap)
            {
                _hasBaseline = false;
                return Reject(AuthoritativeSnapshotAdmissionStatus.FrameGapTooLarge, true);
            }

            _lastAcceptedFrame = frame;
            return Accept();
        }

        private AuthoritativeSnapshotAdmissionResult Accept()
        {
            return new AuthoritativeSnapshotAdmissionResult(
                AuthoritativeSnapshotAdmissionStatus.Accepted,
                _lastAcceptedFrame,
                false);
        }

        private AuthoritativeSnapshotAdmissionResult Reject(
            AuthoritativeSnapshotAdmissionStatus status,
            bool shouldRequestFullResync)
        {
            return new AuthoritativeSnapshotAdmissionResult(
                status,
                _lastAcceptedFrame,
                shouldRequestFullResync);
        }
    }
}
