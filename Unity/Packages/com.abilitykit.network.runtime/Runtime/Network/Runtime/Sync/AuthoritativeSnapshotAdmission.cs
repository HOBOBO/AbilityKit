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

        /// <summary>
        /// 判断快照是否属于当前绑定的权威世界，不推进帧游标。
        /// </summary>
        public bool IsExpectedWorld(ulong worldId) => _activeWorldId != 0 && worldId == _activeWorldId;

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

        /// <summary>
        /// 先检查世界、版本及恢复状态。仅在调用方已验证精确基线和发布顺序时，允许同帧发布和跨帧跳包。
        /// </summary>
        public AuthoritativeSnapshotAdmissionResult Admit(
            ulong worldId,
            int frame,
            bool isFullSnapshot,
            int schemaVersion = 0,
            bool explicitBaselineAndOrderValidated = false)
        {
            var result = Check(worldId, frame, isFullSnapshot, schemaVersion, explicitBaselineAndOrderValidated);
            if (!result.Accepted)
            {
                if (result.Status is AuthoritativeSnapshotAdmissionStatus.UnsupportedSchemaVersion or
                    AuthoritativeSnapshotAdmissionStatus.FrameGapTooLarge)
                    RequireFullBaseline();
                return result;
            }

            _hasBaseline = true;
            _lastAcceptedFrame = frame;
            return Accept();
        }

        /// <summary>
        /// 只检查候选快照，结果中的帧仍为已提交游标；不会推进游标或使基线失效。
        /// 调用方成功应用后，使用同一组参数调用 Admit 提交。
        /// </summary>
        public AuthoritativeSnapshotAdmissionResult Check(
            ulong worldId,
            int frame,
            bool isFullSnapshot,
            int schemaVersion = 0,
            bool explicitBaselineAndOrderValidated = false)
        {
            if (!IsExpectedWorld(worldId))
            {
                return Reject(AuthoritativeSnapshotAdmissionStatus.WrongWorld, false);
            }

            if (schemaVersion < _minSchemaVersion || schemaVersion > _maxSchemaVersion)
            {
                return Reject(AuthoritativeSnapshotAdmissionStatus.UnsupportedSchemaVersion, true);
            }

            if (frame < 0 || (explicitBaselineAndOrderValidated && frame < _lastAcceptedFrame))
            {
                return Reject(AuthoritativeSnapshotAdmissionStatus.StaleOrDuplicate, false);
            }

            if (isFullSnapshot)
            {
                if (_hasBaseline && !explicitBaselineAndOrderValidated && frame <= _lastAcceptedFrame)
                {
                    return Reject(AuthoritativeSnapshotAdmissionStatus.StaleOrDuplicate, false);
                }

                return Accept();
            }

            if (!_hasBaseline)
            {
                return Reject(AuthoritativeSnapshotAdmissionStatus.BaselineRequired, true);
            }

            if (!explicitBaselineAndOrderValidated && frame <= _lastAcceptedFrame)
            {
                return Reject(AuthoritativeSnapshotAdmissionStatus.StaleOrDuplicate, false);
            }

            if (!explicitBaselineAndOrderValidated && (long)frame - _lastAcceptedFrame > _maxDeltaFrameGap)
            {
                return Reject(AuthoritativeSnapshotAdmissionStatus.FrameGapTooLarge, true);
            }

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
