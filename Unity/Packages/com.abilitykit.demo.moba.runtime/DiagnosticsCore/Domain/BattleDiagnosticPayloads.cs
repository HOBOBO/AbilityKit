using System;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    /// <summary>
    /// 诊断事件结构化载荷的稳定判别值。已有数值不得复用或改变语义。
    /// </summary>
    public enum BattleDiagnosticPayloadKind
    {
        None = 0,
        SyncSnapshotReceived = 1,
        TriggerAnalysis = 2
    }

    public enum BattleDiagnosticTriggerAnalysisStage
    {
        Unknown = 0,
        Budget = 1,
        Conditions = 2,
        Plan = 3,
        Execution = 4
    }

    public enum BattleDiagnosticTriggerAnalysisResult
    {
        Unknown = 0,
        Passed = 1,
        Failed = 2,
        Blocked = 3,
        Skipped = 4
    }

    /// <summary>
    /// 收到权威状态哈希快照时记录的第一版结构化载荷。
    /// </summary>
    public readonly struct BattleDiagnosticSyncSnapshotReceivedPayload :
        IEquatable<BattleDiagnosticSyncSnapshotReceivedPayload>
    {
        public const int CurrentSchemaVersion = 1;

        public BattleDiagnosticSyncSnapshotReceivedPayload(
            int authoritativeFrame,
            uint stateHash)
        {
            AuthoritativeFrame = authoritativeFrame;
            StateHash = stateHash;
        }

        public int AuthoritativeFrame { get; }
        public uint StateHash { get; }

        public bool Equals(BattleDiagnosticSyncSnapshotReceivedPayload other)
        {
            return AuthoritativeFrame == other.AuthoritativeFrame &&
                   StateHash == other.StateHash;
        }

        public override bool Equals(object obj)
        {
            return obj is BattleDiagnosticSyncSnapshotReceivedPayload other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (AuthoritativeFrame * 397) ^ (int)StateHash;
            }
        }
    }

    public readonly struct BattleDiagnosticTriggerAnalysisPayload :
        IEquatable<BattleDiagnosticTriggerAnalysisPayload>
    {
        public const int CurrentSchemaVersion = 1;

        public BattleDiagnosticTriggerAnalysisPayload(
            int triggerId,
            int contextKind,
            int originKind,
            BattleDiagnosticTriggerAnalysisStage stage,
            BattleDiagnosticTriggerAnalysisResult result,
            int detailCode = 0,
            int currentDepth = 0,
            int currentFrameCount = 0,
            int currentRootCount = 0,
            int currentSameTriggerCount = 0,
            string failureKey = "",
            string reason = "")
        {
            if (triggerId <= 0) throw new ArgumentOutOfRangeException(nameof(triggerId));

            TriggerId = triggerId;
            ContextKind = contextKind;
            OriginKind = originKind;
            Stage = stage;
            Result = result;
            DetailCode = detailCode;
            CurrentDepth = currentDepth;
            CurrentFrameCount = currentFrameCount;
            CurrentRootCount = currentRootCount;
            CurrentSameTriggerCount = currentSameTriggerCount;
            FailureKey = failureKey ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public int TriggerId { get; }
        public int ContextKind { get; }
        public int OriginKind { get; }
        public BattleDiagnosticTriggerAnalysisStage Stage { get; }
        public BattleDiagnosticTriggerAnalysisResult Result { get; }
        public int DetailCode { get; }
        public int CurrentDepth { get; }
        public int CurrentFrameCount { get; }
        public int CurrentRootCount { get; }
        public int CurrentSameTriggerCount { get; }
        public string FailureKey { get; }
        public string Reason { get; }

        public bool Equals(BattleDiagnosticTriggerAnalysisPayload other)
        {
            return TriggerId == other.TriggerId &&
                   ContextKind == other.ContextKind &&
                   OriginKind == other.OriginKind &&
                   Stage == other.Stage &&
                   Result == other.Result &&
                   DetailCode == other.DetailCode &&
                   CurrentDepth == other.CurrentDepth &&
                   CurrentFrameCount == other.CurrentFrameCount &&
                   CurrentRootCount == other.CurrentRootCount &&
                   CurrentSameTriggerCount == other.CurrentSameTriggerCount &&
                   string.Equals(FailureKey, other.FailureKey, StringComparison.Ordinal) &&
                   string.Equals(Reason, other.Reason, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is BattleDiagnosticTriggerAnalysisPayload other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = TriggerId;
                hashCode = (hashCode * 397) ^ ContextKind;
                hashCode = (hashCode * 397) ^ OriginKind;
                hashCode = (hashCode * 397) ^ (int)Stage;
                hashCode = (hashCode * 397) ^ (int)Result;
                hashCode = (hashCode * 397) ^ DetailCode;
                hashCode = (hashCode * 397) ^ CurrentDepth;
                hashCode = (hashCode * 397) ^ CurrentFrameCount;
                hashCode = (hashCode * 397) ^ CurrentRootCount;
                hashCode = (hashCode * 397) ^ CurrentSameTriggerCount;
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(FailureKey ?? string.Empty);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Reason ?? string.Empty);
                return hashCode;
            }
        }
    }

    /// <summary>
    /// 平台无关、版本化诊断载荷判别联合。
    /// 未迁移事件使用 <see cref="None"/>，消费者必须按 Kind 通过专用 TryGet 读取。
    /// </summary>
    public readonly struct BattleDiagnosticEventPayload :
        IEquatable<BattleDiagnosticEventPayload>
    {
        private readonly int _int32Value;
        private readonly uint _uint32Value;
        private readonly int _int32Value2;
        private readonly int _int32Value3;
        private readonly int _int32Value4;
        private readonly int _int32Value5;
        private readonly int _int32Value6;
        private readonly int _int32Value7;
        private readonly int _int32Value8;
        private readonly int _int32Value9;
        private readonly int _int32Value10;
        private readonly string _stringValue;
        private readonly string _stringValue2;

        private BattleDiagnosticEventPayload(
            BattleDiagnosticPayloadKind kind,
            int schemaVersion,
            int int32Value,
            uint uint32Value,
            int int32Value2 = 0,
            int int32Value3 = 0,
            int int32Value4 = 0,
            int int32Value5 = 0,
            int int32Value6 = 0,
            int int32Value7 = 0,
            int int32Value8 = 0,
            int int32Value9 = 0,
            int int32Value10 = 0,
            string stringValue = "",
            string stringValue2 = "")
        {
            if (kind == BattleDiagnosticPayloadKind.None)
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (schemaVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            }

            Kind = kind;
            SchemaVersion = schemaVersion;
            _int32Value = int32Value;
            _uint32Value = uint32Value;
            _int32Value2 = int32Value2;
            _int32Value3 = int32Value3;
            _int32Value4 = int32Value4;
            _int32Value5 = int32Value5;
            _int32Value6 = int32Value6;
            _int32Value7 = int32Value7;
            _int32Value8 = int32Value8;
            _int32Value9 = int32Value9;
            _int32Value10 = int32Value10;
            _stringValue = stringValue ?? string.Empty;
            _stringValue2 = stringValue2 ?? string.Empty;
        }

        public static BattleDiagnosticEventPayload None => default;

        public BattleDiagnosticPayloadKind Kind { get; }
        public int SchemaVersion { get; }
        public bool HasValue => Kind != BattleDiagnosticPayloadKind.None;

        public static BattleDiagnosticEventPayload FromSyncSnapshotReceived(
            in BattleDiagnosticSyncSnapshotReceivedPayload payload)
        {
            return new BattleDiagnosticEventPayload(
                BattleDiagnosticPayloadKind.SyncSnapshotReceived,
                BattleDiagnosticSyncSnapshotReceivedPayload.CurrentSchemaVersion,
                payload.AuthoritativeFrame,
                payload.StateHash);
        }

        public bool TryGetSyncSnapshotReceived(
            out BattleDiagnosticSyncSnapshotReceivedPayload payload)
        {
            if (Kind != BattleDiagnosticPayloadKind.SyncSnapshotReceived ||
                SchemaVersion != BattleDiagnosticSyncSnapshotReceivedPayload.CurrentSchemaVersion)
            {
                payload = default;
                return false;
            }

            payload = new BattleDiagnosticSyncSnapshotReceivedPayload(
                _int32Value,
                _uint32Value);
            return true;
        }

        public static BattleDiagnosticEventPayload FromTriggerAnalysis(
            in BattleDiagnosticTriggerAnalysisPayload payload)
        {
            return new BattleDiagnosticEventPayload(
                BattleDiagnosticPayloadKind.TriggerAnalysis,
                BattleDiagnosticTriggerAnalysisPayload.CurrentSchemaVersion,
                payload.TriggerId,
                0U,
                payload.ContextKind,
                payload.OriginKind,
                (int)payload.Stage,
                (int)payload.Result,
                payload.DetailCode,
                payload.CurrentDepth,
                payload.CurrentFrameCount,
                payload.CurrentRootCount,
                payload.CurrentSameTriggerCount,
                payload.FailureKey,
                payload.Reason);
        }

        public bool TryGetTriggerAnalysis(
            out BattleDiagnosticTriggerAnalysisPayload payload)
        {
            if (Kind != BattleDiagnosticPayloadKind.TriggerAnalysis ||
                SchemaVersion != BattleDiagnosticTriggerAnalysisPayload.CurrentSchemaVersion)
            {
                payload = default;
                return false;
            }

            payload = new BattleDiagnosticTriggerAnalysisPayload(
                _int32Value,
                _int32Value2,
                _int32Value3,
                (BattleDiagnosticTriggerAnalysisStage)_int32Value4,
                (BattleDiagnosticTriggerAnalysisResult)_int32Value5,
                _int32Value6,
                _int32Value7,
                _int32Value8,
                _int32Value9,
                _int32Value10,
                _stringValue,
                _stringValue2);
            return true;
        }

        public bool Equals(BattleDiagnosticEventPayload other)
        {
            return Kind == other.Kind &&
                   SchemaVersion == other.SchemaVersion &&
                   _int32Value == other._int32Value &&
                   _uint32Value == other._uint32Value &&
                   _int32Value2 == other._int32Value2 &&
                   _int32Value3 == other._int32Value3 &&
                   _int32Value4 == other._int32Value4 &&
                   _int32Value5 == other._int32Value5 &&
                   _int32Value6 == other._int32Value6 &&
                   _int32Value7 == other._int32Value7 &&
                   _int32Value8 == other._int32Value8 &&
                   _int32Value9 == other._int32Value9 &&
                   _int32Value10 == other._int32Value10 &&
                   string.Equals(_stringValue, other._stringValue, StringComparison.Ordinal) &&
                   string.Equals(_stringValue2, other._stringValue2, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is BattleDiagnosticEventPayload other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)Kind;
                hashCode = (hashCode * 397) ^ SchemaVersion;
                hashCode = (hashCode * 397) ^ _int32Value;
                hashCode = (hashCode * 397) ^ (int)_uint32Value;
                hashCode = (hashCode * 397) ^ _int32Value2;
                hashCode = (hashCode * 397) ^ _int32Value3;
                hashCode = (hashCode * 397) ^ _int32Value4;
                hashCode = (hashCode * 397) ^ _int32Value5;
                hashCode = (hashCode * 397) ^ _int32Value6;
                hashCode = (hashCode * 397) ^ _int32Value7;
                hashCode = (hashCode * 397) ^ _int32Value8;
                hashCode = (hashCode * 397) ^ _int32Value9;
                hashCode = (hashCode * 397) ^ _int32Value10;
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(_stringValue ?? string.Empty);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(_stringValue2 ?? string.Empty);
                return hashCode;
            }
        }
    }
}
