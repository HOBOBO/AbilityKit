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
        TriggerAnalysis = 2,
        SkillFailure = 3,
        BuffLifecycle = 4,
        TriggerAnalysisAggregate = 5,
        DamageCalculation = 6,
        TargetSearch = 7,
        InputCommand = 8,
        SkillExecution = 9
    }

    public enum BattleDiagnosticSkillExecutionStage
    {
        PreCastStarted = 1,
        PreCastCompleted = 2,
        CastStarted = 3,
        CastCompleted = 4,
        CastFailed = 5,
        CastInterrupted = 6,
        EconomyReserved = 7,
        ResourceConsumed = 8,
        EconomyCommitted = 9,
        EconomyRefunded = 10,
        EconomyRejected = 11,
        RuntimeWaitingChildren = 12,
        RuntimeFinalized = 13,
        RuntimeForceTerminated = 14,
        RuntimeCleared = 15
    }

    public readonly struct BattleDiagnosticSkillExecutionPayload
    {
        public const int CurrentSchemaVersion = 1;

        public BattleDiagnosticSkillExecutionPayload(
            long commandId,
            BattleDiagnosticSkillExecutionStage stage,
            int skillSlot,
            int skillLevel,
            int castSequence,
            int endReason = 0,
            int resourceType = 0,
            long resourceAmountRaw = 0L,
            long resourceBeforeRaw = 0L,
            long resourceAfterRaw = 0L,
            int chargeCost = 0,
            int cooldownMs = 0,
            int sharedCooldownMs = 0,
            int globalCooldownMs = 0,
            int pendingChildren = 0,
            bool forced = false,
            string detail = "")
        {
            CommandId = commandId;
            Stage = stage;
            SkillSlot = skillSlot;
            SkillLevel = skillLevel;
            CastSequence = castSequence;
            EndReason = endReason;
            ResourceType = resourceType;
            ResourceAmountRaw = resourceAmountRaw;
            ResourceBeforeRaw = resourceBeforeRaw;
            ResourceAfterRaw = resourceAfterRaw;
            ChargeCost = chargeCost;
            CooldownMs = cooldownMs;
            SharedCooldownMs = sharedCooldownMs;
            GlobalCooldownMs = globalCooldownMs;
            PendingChildren = pendingChildren;
            Forced = forced;
            Detail = detail ?? string.Empty;
        }

        public long CommandId { get; }
        public BattleDiagnosticSkillExecutionStage Stage { get; }
        public int SkillSlot { get; }
        public int SkillLevel { get; }
        public int CastSequence { get; }
        public int EndReason { get; }
        public int ResourceType { get; }
        public long ResourceAmountRaw { get; }
        public long ResourceBeforeRaw { get; }
        public long ResourceAfterRaw { get; }
        public int ChargeCost { get; }
        public int CooldownMs { get; }
        public int SharedCooldownMs { get; }
        public int GlobalCooldownMs { get; }
        public int PendingChildren { get; }
        public bool Forced { get; }
        public string Detail { get; }
    }

    public readonly struct BattleDiagnosticInputCommandPayload
    {
        public const int CurrentSchemaVersion = 1;

        public BattleDiagnosticInputCommandPayload(long commandId, int inputFrame, string playerId,
            int opCode, bool succeeded, int failureCode, string message, int skillSlot = 0,
            int skillPhase = 0, int targetActorId = 0)
        {
            CommandId = commandId;
            InputFrame = inputFrame;
            PlayerId = playerId ?? string.Empty;
            OpCode = opCode;
            Succeeded = succeeded;
            FailureCode = failureCode;
            Message = message ?? string.Empty;
            SkillSlot = skillSlot;
            SkillPhase = skillPhase;
            TargetActorId = targetActorId;
        }

        public long CommandId { get; }
        public int InputFrame { get; }
        public string PlayerId { get; }
        public int OpCode { get; }
        public bool Succeeded { get; }
        public int FailureCode { get; }
        public string Message { get; }
        public int SkillSlot { get; }
        public int SkillPhase { get; }
        public int TargetActorId { get; }
    }

    public readonly struct BattleDiagnosticTargetSearchPayload
    {
        public const int CurrentSchemaVersion = 1;

        public BattleDiagnosticTargetSearchPayload(long commandId, int explicitTargetActorId,
            int candidateCount, int eligibleCount, int selectedCount,
            string selectedActorIds, string decisionDetails)
        {
            CommandId = commandId;
            ExplicitTargetActorId = explicitTargetActorId;
            CandidateCount = candidateCount;
            EligibleCount = eligibleCount;
            SelectedCount = selectedCount;
            SelectedActorIds = selectedActorIds ?? string.Empty;
            DecisionDetails = decisionDetails ?? string.Empty;
        }

        public long CommandId { get; }
        public int ExplicitTargetActorId { get; }
        public int CandidateCount { get; }
        public int EligibleCount { get; }
        public int SelectedCount { get; }
        public string SelectedActorIds { get; }
        public string DecisionDetails { get; }
    }

    public enum BattleDiagnosticDamageStage
    {
        TargetMissing = 1,
        ShieldCommitRejected = 2,
        HealthCommitRejected = 3,
        Completed = 4,
        InvalidRequest = 5,
        TransactionRejected = 6,
        ExecutionFailed = 7,
        PostCommitNotificationFailed = 8
    }

    public readonly struct BattleDiagnosticDamageCalculationPayload
    {
        public const int CurrentSchemaVersion = 1;

        public BattleDiagnosticDamageCalculationPayload(BattleDiagnosticDamageStage stage,
            long baseDamageRaw, long rawDamageRaw, long mitigatedDamageRaw,
            long shieldAbsorbRaw, long plannedHpDamageRaw, long appliedHpDamageRaw)
        {
            Stage = stage;
            BaseDamageRaw = baseDamageRaw;
            RawDamageRaw = rawDamageRaw;
            MitigatedDamageRaw = mitigatedDamageRaw;
            ShieldAbsorbRaw = shieldAbsorbRaw;
            PlannedHpDamageRaw = plannedHpDamageRaw;
            AppliedHpDamageRaw = appliedHpDamageRaw;
        }

        public BattleDiagnosticDamageStage Stage { get; }
        public bool HasCalculation => Stage == BattleDiagnosticDamageStage.Completed ||
            Stage == BattleDiagnosticDamageStage.ShieldCommitRejected || Stage == BattleDiagnosticDamageStage.HealthCommitRejected;
        public long BaseDamageRaw { get; }
        public long RawDamageRaw { get; }
        public long MitigatedDamageRaw { get; }
        public long ShieldAbsorbRaw { get; }
        public long PlannedHpDamageRaw { get; }
        public long AppliedHpDamageRaw { get; }

        // Diagnostic values are signed Q32.32, matching the simulation's Fixed64 raw representation.
        public static double ToDisplayValue(long raw) => raw / 4294967296d;
    }

    public enum BattleDiagnosticBuffLifecycleStage
    {
        Applied = 1,
        Refreshed = 2,
        StackChanged = 3,
        Interval = 4,
        Removed = 5
    }

    public readonly struct BattleDiagnosticBuffLifecyclePayload : IEquatable<BattleDiagnosticBuffLifecyclePayload>
    {
        public const int CurrentSchemaVersion = 1;

        public BattleDiagnosticBuffLifecyclePayload(
            BattleDiagnosticBuffLifecycleStage stage,
            int stackCount,
            int previousStackCount,
            int durationMilliseconds,
            int remainingMilliseconds,
            int intervalRemainingMilliseconds,
            int maxStacks,
            int modifierBindingCount,
            int modifierSourceId,
            int removeReason)
        {
            Stage = stage;
            StackCount = stackCount;
            PreviousStackCount = previousStackCount;
            DurationMilliseconds = durationMilliseconds;
            RemainingMilliseconds = remainingMilliseconds;
            IntervalRemainingMilliseconds = intervalRemainingMilliseconds;
            MaxStacks = maxStacks;
            ModifierBindingCount = modifierBindingCount;
            ModifierSourceId = modifierSourceId;
            RemoveReason = removeReason;
        }

        public BattleDiagnosticBuffLifecycleStage Stage { get; }
        public int StackCount { get; }
        public int PreviousStackCount { get; }
        public int DurationMilliseconds { get; }
        public int RemainingMilliseconds { get; }
        public int IntervalRemainingMilliseconds { get; }
        public int MaxStacks { get; }
        public int ModifierBindingCount { get; }
        public int ModifierSourceId { get; }
        public int RemoveReason { get; }

        public bool Equals(BattleDiagnosticBuffLifecyclePayload other)
        {
            return Stage == other.Stage && StackCount == other.StackCount && PreviousStackCount == other.PreviousStackCount &&
                   DurationMilliseconds == other.DurationMilliseconds && RemainingMilliseconds == other.RemainingMilliseconds &&
                   IntervalRemainingMilliseconds == other.IntervalRemainingMilliseconds && MaxStacks == other.MaxStacks &&
                   ModifierBindingCount == other.ModifierBindingCount && ModifierSourceId == other.ModifierSourceId &&
                   RemoveReason == other.RemoveReason;
        }

        public override bool Equals(object obj) => obj is BattleDiagnosticBuffLifecyclePayload other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)Stage;
                hashCode = (hashCode * 397) ^ StackCount;
                hashCode = (hashCode * 397) ^ PreviousStackCount;
                hashCode = (hashCode * 397) ^ DurationMilliseconds;
                hashCode = (hashCode * 397) ^ RemainingMilliseconds;
                hashCode = (hashCode * 397) ^ IntervalRemainingMilliseconds;
                hashCode = (hashCode * 397) ^ MaxStacks;
                hashCode = (hashCode * 397) ^ ModifierBindingCount;
                hashCode = (hashCode * 397) ^ ModifierSourceId;
                return (hashCode * 397) ^ RemoveReason;
            }
        }
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

    public readonly struct BattleDiagnosticTriggerAnalysisAggregatePayload :
        IEquatable<BattleDiagnosticTriggerAnalysisAggregatePayload>
    {
        public const int CurrentSchemaVersion = 1;

        public BattleDiagnosticTriggerAnalysisAggregatePayload(
            int triggerId,
            int contextKind,
            int originKind,
            BattleDiagnosticTriggerAnalysisStage stage,
            BattleDiagnosticTriggerAnalysisResult result,
            int detailCode,
            int occurrenceCount,
            int firstFrame,
            int lastFrame,
            long firstContextId,
            long lastContextId,
            long firstRootContextId,
            long lastRootContextId,
            string failureKey = "",
            string sampleReason = "")
        {
            if (triggerId <= 0) throw new ArgumentOutOfRangeException(nameof(triggerId));
            if (occurrenceCount <= 0) throw new ArgumentOutOfRangeException(nameof(occurrenceCount));
            if (!BattleDiagnosticFrames.IsValid(firstFrame)) throw new ArgumentOutOfRangeException(nameof(firstFrame));
            if (lastFrame < firstFrame) throw new ArgumentOutOfRangeException(nameof(lastFrame));

            TriggerId = triggerId;
            ContextKind = contextKind;
            OriginKind = originKind;
            Stage = stage;
            Result = result;
            DetailCode = detailCode;
            OccurrenceCount = occurrenceCount;
            FirstFrame = firstFrame;
            LastFrame = lastFrame;
            FirstContextId = firstContextId;
            LastContextId = lastContextId;
            FirstRootContextId = firstRootContextId;
            LastRootContextId = lastRootContextId;
            FailureKey = failureKey ?? string.Empty;
            SampleReason = sampleReason ?? string.Empty;
        }

        public int TriggerId { get; }
        public int ContextKind { get; }
        public int OriginKind { get; }
        public BattleDiagnosticTriggerAnalysisStage Stage { get; }
        public BattleDiagnosticTriggerAnalysisResult Result { get; }
        public int DetailCode { get; }
        public int OccurrenceCount { get; }
        public int FirstFrame { get; }
        public int LastFrame { get; }
        public int FrameSpan => LastFrame - FirstFrame;
        public long FirstContextId { get; }
        public long LastContextId { get; }
        public long FirstRootContextId { get; }
        public long LastRootContextId { get; }
        public string FailureKey { get; }
        public string SampleReason { get; }

        public bool Equals(BattleDiagnosticTriggerAnalysisAggregatePayload other)
        {
            return TriggerId == other.TriggerId &&
                   ContextKind == other.ContextKind &&
                   OriginKind == other.OriginKind &&
                   Stage == other.Stage &&
                   Result == other.Result &&
                   DetailCode == other.DetailCode &&
                   OccurrenceCount == other.OccurrenceCount &&
                   FirstFrame == other.FirstFrame &&
                   LastFrame == other.LastFrame &&
                   FirstContextId == other.FirstContextId &&
                   LastContextId == other.LastContextId &&
                   FirstRootContextId == other.FirstRootContextId &&
                   LastRootContextId == other.LastRootContextId &&
                   string.Equals(FailureKey, other.FailureKey, StringComparison.Ordinal) &&
                   string.Equals(SampleReason, other.SampleReason, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is BattleDiagnosticTriggerAnalysisAggregatePayload other && Equals(other);
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
                hashCode = (hashCode * 397) ^ OccurrenceCount;
                hashCode = (hashCode * 397) ^ FirstFrame;
                hashCode = (hashCode * 397) ^ LastFrame;
                hashCode = (hashCode * 397) ^ FirstContextId.GetHashCode();
                hashCode = (hashCode * 397) ^ LastContextId.GetHashCode();
                hashCode = (hashCode * 397) ^ FirstRootContextId.GetHashCode();
                hashCode = (hashCode * 397) ^ LastRootContextId.GetHashCode();
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(FailureKey ?? string.Empty);
                return (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(SampleReason ?? string.Empty);
            }
        }
    }

    public readonly struct BattleDiagnosticSkillFailurePayload :
        IEquatable<BattleDiagnosticSkillFailurePayload>
    {
        public const int CurrentSchemaVersion = 1;

        public BattleDiagnosticSkillFailurePayload(
            int slot,
            string source,
            string stage,
            string code,
            string message,
            long commandId = 0L)
        {
            Slot = slot;
            Source = source ?? string.Empty;
            Stage = stage ?? string.Empty;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            CommandId = commandId;
        }

        public int Slot { get; }
        public string Source { get; }
        public string Stage { get; }
        public string Code { get; }
        public string Message { get; }
        public long CommandId { get; }

        public bool Equals(BattleDiagnosticSkillFailurePayload other)
        {
            return Slot == other.Slot && CommandId == other.CommandId &&
                   string.Equals(Source, other.Source, StringComparison.Ordinal) &&
                   string.Equals(Stage, other.Stage, StringComparison.Ordinal) &&
                   string.Equals(Code, other.Code, StringComparison.Ordinal) &&
                   string.Equals(Message, other.Message, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is BattleDiagnosticSkillFailurePayload other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Slot * 397) ^ CommandId.GetHashCode();
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Source ?? string.Empty);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Stage ?? string.Empty);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Code ?? string.Empty);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Message ?? string.Empty);
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
        private readonly long _int64Value;
        private readonly long _int64Value2;
        private readonly long _int64Value3;
        private readonly long _int64Value4;
        private readonly long _int64Value5;
        private readonly long _int64Value6;
        private readonly string _stringValue;
        private readonly string _stringValue2;
        private readonly string _stringValue3;
        private readonly string _stringValue4;

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
            long int64Value = 0L,
            long int64Value2 = 0L,
            long int64Value3 = 0L,
            long int64Value4 = 0L,
            long int64Value5 = 0L,
            long int64Value6 = 0L,
            string stringValue = "",
            string stringValue2 = "",
            string stringValue3 = "",
            string stringValue4 = "")
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
            _int64Value = int64Value;
            _int64Value2 = int64Value2;
            _int64Value3 = int64Value3;
            _int64Value4 = int64Value4;
            _int64Value5 = int64Value5;
            _int64Value6 = int64Value6;
            _stringValue = stringValue ?? string.Empty;
            _stringValue2 = stringValue2 ?? string.Empty;
            _stringValue3 = stringValue3 ?? string.Empty;
            _stringValue4 = stringValue4 ?? string.Empty;
        }

        public static BattleDiagnosticEventPayload None => default;

        public BattleDiagnosticPayloadKind Kind { get; }
        public int SchemaVersion { get; }
        public bool HasValue => Kind != BattleDiagnosticPayloadKind.None;

        public static BattleDiagnosticEventPayload FromSkillExecution(
            in BattleDiagnosticSkillExecutionPayload payload)
        {
            return new BattleDiagnosticEventPayload(BattleDiagnosticPayloadKind.SkillExecution,
                BattleDiagnosticSkillExecutionPayload.CurrentSchemaVersion,
                (int)payload.Stage, payload.Forced ? 1U : 0U,
                payload.SkillSlot, payload.SkillLevel, payload.CastSequence, payload.EndReason,
                payload.ResourceType, payload.ChargeCost, payload.CooldownMs,
                payload.SharedCooldownMs, payload.GlobalCooldownMs,
                int64Value: payload.CommandId,
                int64Value2: payload.ResourceAmountRaw,
                int64Value3: payload.ResourceBeforeRaw,
                int64Value4: payload.ResourceAfterRaw,
                int64Value5: payload.PendingChildren,
                stringValue: payload.Detail);
        }

        public bool TryGetSkillExecution(out BattleDiagnosticSkillExecutionPayload payload)
        {
            if (Kind != BattleDiagnosticPayloadKind.SkillExecution ||
                SchemaVersion != BattleDiagnosticSkillExecutionPayload.CurrentSchemaVersion)
            {
                payload = default;
                return false;
            }

            payload = new BattleDiagnosticSkillExecutionPayload(
                _int64Value,
                (BattleDiagnosticSkillExecutionStage)_int32Value,
                _int32Value2,
                _int32Value3,
                _int32Value4,
                _int32Value5,
                _int32Value6,
                _int64Value2,
                _int64Value3,
                _int64Value4,
                _int32Value7,
                _int32Value8,
                _int32Value9,
                _int32Value10,
                (int)_int64Value5,
                _uint32Value != 0U,
                _stringValue);
            return true;
        }

        public static BattleDiagnosticEventPayload FromInputCommand(in BattleDiagnosticInputCommandPayload payload)
        {
            return new BattleDiagnosticEventPayload(BattleDiagnosticPayloadKind.InputCommand,
                BattleDiagnosticInputCommandPayload.CurrentSchemaVersion,
                payload.InputFrame, 0U, payload.OpCode, payload.Succeeded ? 1 : 0,
                payload.FailureCode, payload.SkillSlot, payload.SkillPhase, payload.TargetActorId,
                int64Value: payload.CommandId, stringValue: payload.PlayerId,
                stringValue2: payload.Message);
        }

        public bool TryGetInputCommand(out BattleDiagnosticInputCommandPayload payload)
        {
            if (Kind != BattleDiagnosticPayloadKind.InputCommand ||
                SchemaVersion != BattleDiagnosticInputCommandPayload.CurrentSchemaVersion)
            {
                payload = default;
                return false;
            }
            payload = new BattleDiagnosticInputCommandPayload(_int64Value, _int32Value,
                _stringValue, _int32Value2, _int32Value3 != 0, _int32Value4,
                _stringValue2, _int32Value5, _int32Value6, _int32Value7);
            return true;
        }

        public static BattleDiagnosticEventPayload FromTargetSearch(in BattleDiagnosticTargetSearchPayload payload)
        {
            return new BattleDiagnosticEventPayload(BattleDiagnosticPayloadKind.TargetSearch,
                BattleDiagnosticTargetSearchPayload.CurrentSchemaVersion,
                payload.ExplicitTargetActorId, 0U, payload.CandidateCount,
                payload.EligibleCount, payload.SelectedCount,
                int64Value: payload.CommandId, stringValue: payload.SelectedActorIds,
                stringValue2: payload.DecisionDetails);
        }

        public bool TryGetTargetSearch(out BattleDiagnosticTargetSearchPayload payload)
        {
            if (Kind != BattleDiagnosticPayloadKind.TargetSearch ||
                SchemaVersion != BattleDiagnosticTargetSearchPayload.CurrentSchemaVersion)
            {
                payload = default;
                return false;
            }
            payload = new BattleDiagnosticTargetSearchPayload(_int64Value, _int32Value,
                _int32Value2, _int32Value3, _int32Value4, _stringValue, _stringValue2);
            return true;
        }

        public static BattleDiagnosticEventPayload FromDamageCalculation(in BattleDiagnosticDamageCalculationPayload payload)
        {
            return new BattleDiagnosticEventPayload(BattleDiagnosticPayloadKind.DamageCalculation,
                BattleDiagnosticDamageCalculationPayload.CurrentSchemaVersion, (int)payload.Stage, 0U,
                int64Value: payload.BaseDamageRaw, int64Value2: payload.RawDamageRaw,
                int64Value3: payload.MitigatedDamageRaw, int64Value4: payload.ShieldAbsorbRaw,
                int64Value5: payload.PlannedHpDamageRaw, int64Value6: payload.AppliedHpDamageRaw);
        }

        public bool TryGetDamageCalculation(out BattleDiagnosticDamageCalculationPayload payload)
        {
            if (Kind != BattleDiagnosticPayloadKind.DamageCalculation ||
                SchemaVersion != BattleDiagnosticDamageCalculationPayload.CurrentSchemaVersion)
            {
                payload = default;
                return false;
            }

            payload = new BattleDiagnosticDamageCalculationPayload((BattleDiagnosticDamageStage)_int32Value,
                _int64Value, _int64Value2, _int64Value3, _int64Value4, _int64Value5, _int64Value6);
            return true;
        }

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
                stringValue: payload.FailureKey,
                stringValue2: payload.Reason);
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

        public static BattleDiagnosticEventPayload FromTriggerAnalysisAggregate(
            in BattleDiagnosticTriggerAnalysisAggregatePayload payload)
        {
            return new BattleDiagnosticEventPayload(
                BattleDiagnosticPayloadKind.TriggerAnalysisAggregate,
                BattleDiagnosticTriggerAnalysisAggregatePayload.CurrentSchemaVersion,
                payload.TriggerId,
                0U,
                payload.ContextKind,
                payload.OriginKind,
                (int)payload.Stage,
                (int)payload.Result,
                payload.DetailCode,
                payload.OccurrenceCount,
                payload.FirstFrame,
                payload.LastFrame,
                int64Value: payload.FirstContextId,
                int64Value2: payload.LastContextId,
                int64Value3: payload.FirstRootContextId,
                int64Value4: payload.LastRootContextId,
                stringValue: payload.FailureKey,
                stringValue2: payload.SampleReason);
        }

        public bool TryGetTriggerAnalysisAggregate(
            out BattleDiagnosticTriggerAnalysisAggregatePayload payload)
        {
            if (Kind != BattleDiagnosticPayloadKind.TriggerAnalysisAggregate ||
                SchemaVersion != BattleDiagnosticTriggerAnalysisAggregatePayload.CurrentSchemaVersion)
            {
                payload = default;
                return false;
            }

            payload = new BattleDiagnosticTriggerAnalysisAggregatePayload(
                _int32Value,
                _int32Value2,
                _int32Value3,
                (BattleDiagnosticTriggerAnalysisStage)_int32Value4,
                (BattleDiagnosticTriggerAnalysisResult)_int32Value5,
                _int32Value6,
                _int32Value7,
                _int32Value8,
                _int32Value9,
                _int64Value,
                _int64Value2,
                _int64Value3,
                _int64Value4,
                _stringValue,
                _stringValue2);
            return true;
        }

        public static BattleDiagnosticEventPayload FromBuffLifecycle(
            in BattleDiagnosticBuffLifecyclePayload payload)
        {
            return new BattleDiagnosticEventPayload(
                BattleDiagnosticPayloadKind.BuffLifecycle,
                BattleDiagnosticBuffLifecyclePayload.CurrentSchemaVersion,
                (int)payload.Stage,
                0U,
                payload.StackCount,
                payload.PreviousStackCount,
                payload.DurationMilliseconds,
                payload.RemainingMilliseconds,
                payload.IntervalRemainingMilliseconds,
                payload.MaxStacks,
                payload.ModifierBindingCount,
                payload.ModifierSourceId,
                payload.RemoveReason);
        }

        public bool TryGetBuffLifecycle(out BattleDiagnosticBuffLifecyclePayload payload)
        {
            if (Kind != BattleDiagnosticPayloadKind.BuffLifecycle || SchemaVersion != BattleDiagnosticBuffLifecyclePayload.CurrentSchemaVersion)
            {
                payload = default;
                return false;
            }

            payload = new BattleDiagnosticBuffLifecyclePayload(
                (BattleDiagnosticBuffLifecycleStage)_int32Value,
                _int32Value2,
                _int32Value3,
                _int32Value4,
                _int32Value5,
                _int32Value6,
                _int32Value7,
                _int32Value8,
                _int32Value9,
                _int32Value10);
            return true;
        }

        public static BattleDiagnosticEventPayload FromSkillFailure(
            in BattleDiagnosticSkillFailurePayload payload)
        {
            return new BattleDiagnosticEventPayload(
                BattleDiagnosticPayloadKind.SkillFailure,
                BattleDiagnosticSkillFailurePayload.CurrentSchemaVersion,
                payload.Slot,
                0U,
                int64Value: payload.CommandId,
                stringValue: payload.Source,
                stringValue2: payload.Stage,
                stringValue3: payload.Code,
                stringValue4: payload.Message);
        }

        public bool TryGetSkillFailure(out BattleDiagnosticSkillFailurePayload payload)
        {
            if (Kind != BattleDiagnosticPayloadKind.SkillFailure ||
                SchemaVersion != BattleDiagnosticSkillFailurePayload.CurrentSchemaVersion)
            {
                payload = default;
                return false;
            }

            payload = new BattleDiagnosticSkillFailurePayload(
                _int32Value,
                _stringValue,
                _stringValue2,
                _stringValue3,
                _stringValue4,
                _int64Value);
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
                   _int64Value == other._int64Value &&
                   _int64Value2 == other._int64Value2 &&
                   _int64Value3 == other._int64Value3 &&
                   _int64Value4 == other._int64Value4 &&
                   _int64Value5 == other._int64Value5 &&
                   _int64Value6 == other._int64Value6 &&
                   string.Equals(_stringValue, other._stringValue, StringComparison.Ordinal) &&
                   string.Equals(_stringValue2, other._stringValue2, StringComparison.Ordinal) &&
                   string.Equals(_stringValue3, other._stringValue3, StringComparison.Ordinal) &&
                   string.Equals(_stringValue4, other._stringValue4, StringComparison.Ordinal);
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
                hashCode = (hashCode * 397) ^ _int64Value.GetHashCode();
                hashCode = (hashCode * 397) ^ _int64Value2.GetHashCode();
                hashCode = (hashCode * 397) ^ _int64Value3.GetHashCode();
                hashCode = (hashCode * 397) ^ _int64Value4.GetHashCode();
                hashCode = (hashCode * 397) ^ _int64Value5.GetHashCode();
                hashCode = (hashCode * 397) ^ _int64Value6.GetHashCode();
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(_stringValue ?? string.Empty);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(_stringValue2 ?? string.Empty);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(_stringValue3 ?? string.Empty);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(_stringValue4 ?? string.Empty);
                return hashCode;
            }
        }
    }
}
