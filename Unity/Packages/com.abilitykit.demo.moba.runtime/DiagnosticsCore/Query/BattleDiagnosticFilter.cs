using System;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    [Flags]
    public enum BattleDiagnosticEventChannel
    {
        None = 0,
        Skill = 1 << 0,
        Effect = 1 << 1,
        Buff = 1 << 2,
        TemporaryEntity = 1 << 3,
        DamageAndHeal = 1 << 4,
        Sync = 1 << 5,
        WarningAndException = 1 << 6,
        Trigger = 1 << 7,
        All = Skill |
              Effect |
              Buff |
              TemporaryEntity |
              DamageAndHeal |
              Sync |
              WarningAndException |
              Trigger
    }

    public enum BattleDiagnosticActorRelation
    {
        Any = 0,
        Source = 1,
        Target = 2,
        Either = 3
    }

    public readonly struct BattleDiagnosticFrameFilter : IEquatable<BattleDiagnosticFrameFilter>
    {
        public BattleDiagnosticFrameFilter(int firstFrame, int lastFrame)
        {
            FirstFrame = firstFrame;
            LastFrame = lastFrame;
        }

        public int FirstFrame { get; }
        public int LastFrame { get; }

        public bool IsBounded =>
            BattleDiagnosticFrames.IsValid(FirstFrame) &&
            LastFrame >= FirstFrame;

        public bool Contains(int frame)
        {
            return !IsBounded || frame >= FirstFrame && frame <= LastFrame;
        }

        public bool Equals(BattleDiagnosticFrameFilter other)
        {
            return FirstFrame == other.FirstFrame && LastFrame == other.LastFrame;
        }

        public override bool Equals(object obj)
        {
            return obj is BattleDiagnosticFrameFilter other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (FirstFrame * 397) ^ LastFrame;
            }
        }
    }

    public readonly struct BattleDiagnosticFilter : IEquatable<BattleDiagnosticFilter>
    {
        public BattleDiagnosticFilter(
            BattleDiagnosticFrameFilter frames,
            BattleDiagnosticEventChannel channels,
            long actorId = 0,
            BattleDiagnosticActorRelation actorRelation = BattleDiagnosticActorRelation.Any,
            int configId = 0,
            long rootContextId = 0,
            long contextId = 0,
            long skillRuntimeId = 0,
            long attackId = 0,
            bool failuresOnly = false,
            bool unfinishedOnly = false,
            string searchText = "",
            BattleDiagnosticTriggerAnalysisStage triggerStage = BattleDiagnosticTriggerAnalysisStage.Unknown,
            BattleDiagnosticTriggerAnalysisResult triggerResult = BattleDiagnosticTriggerAnalysisResult.Unknown,
            int triggerContextKind = 0,
            int triggerOriginKind = 0)
        {
            Frames = frames;
            Channels = channels;
            ActorId = actorId;
            ActorRelation = actorRelation;
            ConfigId = configId;
            RootContextId = rootContextId;
            ContextId = contextId;
            SkillRuntimeId = skillRuntimeId;
            AttackId = attackId;
            FailuresOnly = failuresOnly;
            UnfinishedOnly = unfinishedOnly;
            SearchText = NormalizeSearchText(searchText);
            TriggerStage = triggerStage;
            TriggerResult = triggerResult;
            TriggerContextKind = triggerContextKind;
            TriggerOriginKind = triggerOriginKind;
        }

        public BattleDiagnosticFrameFilter Frames { get; }
        public BattleDiagnosticEventChannel Channels { get; }
        public long ActorId { get; }
        public BattleDiagnosticActorRelation ActorRelation { get; }
        public int ConfigId { get; }
        public long RootContextId { get; }
        public long ContextId { get; }
        public long SkillRuntimeId { get; }
        public long AttackId { get; }
        public bool FailuresOnly { get; }
        public bool UnfinishedOnly { get; }
        public string SearchText { get; }
        public BattleDiagnosticTriggerAnalysisStage TriggerStage { get; }
        public BattleDiagnosticTriggerAnalysisResult TriggerResult { get; }
        public int TriggerContextKind { get; }
        public int TriggerOriginKind { get; }

        public bool HasActorFilter => ActorId != 0;
        public bool HasCorrelationFilter =>
            RootContextId != 0 ||
            ContextId != 0 ||
            SkillRuntimeId != 0 ||
            AttackId != 0;

        public bool HasTextSearch => !string.IsNullOrEmpty(SearchText);
        public bool HasTriggerAnalysisFilter =>
            TriggerStage != BattleDiagnosticTriggerAnalysisStage.Unknown ||
            TriggerResult != BattleDiagnosticTriggerAnalysisResult.Unknown ||
            TriggerContextKind != 0 ||
            TriggerOriginKind != 0;

        public int ActiveFilterCount
        {
            get
            {
                var count = 0;
                if (Frames.IsBounded) count++;
                if (Channels != BattleDiagnosticEventChannel.All) count++;
                if (HasActorFilter) count++;
                if (ConfigId != 0) count++;
                if (RootContextId != 0) count++;
                if (ContextId != 0) count++;
                if (SkillRuntimeId != 0) count++;
                if (AttackId != 0) count++;
                if (FailuresOnly) count++;
                if (UnfinishedOnly) count++;
                if (HasTextSearch) count++;
                if (TriggerStage != BattleDiagnosticTriggerAnalysisStage.Unknown) count++;
                if (TriggerResult != BattleDiagnosticTriggerAnalysisResult.Unknown) count++;
                if (TriggerContextKind != 0) count++;
                if (TriggerOriginKind != 0) count++;
                return count;
            }
        }

        public static BattleDiagnosticFilter Default => new BattleDiagnosticFilter(
            new BattleDiagnosticFrameFilter(
                BattleDiagnosticFrames.Invalid,
                BattleDiagnosticFrames.Invalid),
            BattleDiagnosticEventChannel.All);

        public BattleDiagnosticFilter WithActor(long actorId, BattleDiagnosticActorRelation relation)
        {
            return new BattleDiagnosticFilter(
                Frames,
                Channels,
                actorId,
                relation,
                ConfigId,
                RootContextId,
                ContextId,
                SkillRuntimeId,
                AttackId,
                FailuresOnly,
                UnfinishedOnly,
                SearchText,
                TriggerStage,
                TriggerResult,
                TriggerContextKind,
                TriggerOriginKind);
        }

        public BattleDiagnosticFilter WithFrames(BattleDiagnosticFrameFilter frames)
        {
            return new BattleDiagnosticFilter(
                frames,
                Channels,
                ActorId,
                ActorRelation,
                ConfigId,
                RootContextId,
                ContextId,
                SkillRuntimeId,
                AttackId,
                FailuresOnly,
                UnfinishedOnly,
                SearchText,
                TriggerStage,
                TriggerResult,
                TriggerContextKind,
                TriggerOriginKind);
        }

        public BattleDiagnosticFilter WithSearchText(string searchText)
        {
            return new BattleDiagnosticFilter(
                Frames,
                Channels,
                ActorId,
                ActorRelation,
                ConfigId,
                RootContextId,
                ContextId,
                SkillRuntimeId,
                AttackId,
                FailuresOnly,
                UnfinishedOnly,
                searchText,
                TriggerStage,
                TriggerResult,
                TriggerContextKind,
                TriggerOriginKind);
        }

        public BattleDiagnosticFilter WithTriggerAnalysis(
            BattleDiagnosticTriggerAnalysisStage stage,
            BattleDiagnosticTriggerAnalysisResult result,
            int contextKind = 0,
            int originKind = 0)
        {
            return new BattleDiagnosticFilter(
                Frames,
                Channels,
                ActorId,
                ActorRelation,
                ConfigId,
                RootContextId,
                ContextId,
                SkillRuntimeId,
                AttackId,
                FailuresOnly,
                UnfinishedOnly,
                SearchText,
                stage,
                result,
                contextKind,
                originKind);
        }

        public bool Equals(BattleDiagnosticFilter other)
        {
            return Frames.Equals(other.Frames) &&
                   Channels == other.Channels &&
                   ActorId == other.ActorId &&
                   ActorRelation == other.ActorRelation &&
                   ConfigId == other.ConfigId &&
                   RootContextId == other.RootContextId &&
                   ContextId == other.ContextId &&
                   SkillRuntimeId == other.SkillRuntimeId &&
                   AttackId == other.AttackId &&
                   FailuresOnly == other.FailuresOnly &&
                   UnfinishedOnly == other.UnfinishedOnly &&
                   TriggerStage == other.TriggerStage &&
                   TriggerResult == other.TriggerResult &&
                   TriggerContextKind == other.TriggerContextKind &&
                   TriggerOriginKind == other.TriggerOriginKind &&
                   string.Equals(SearchText, other.SearchText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is BattleDiagnosticFilter other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Frames.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Channels;
                hashCode = (hashCode * 397) ^ ActorId.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)ActorRelation;
                hashCode = (hashCode * 397) ^ ConfigId;
                hashCode = (hashCode * 397) ^ RootContextId.GetHashCode();
                hashCode = (hashCode * 397) ^ ContextId.GetHashCode();
                hashCode = (hashCode * 397) ^ SkillRuntimeId.GetHashCode();
                hashCode = (hashCode * 397) ^ AttackId.GetHashCode();
                hashCode = (hashCode * 397) ^ FailuresOnly.GetHashCode();
                hashCode = (hashCode * 397) ^ UnfinishedOnly.GetHashCode();
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(SearchText ?? string.Empty);
                hashCode = (hashCode * 397) ^ (int)TriggerStage;
                hashCode = (hashCode * 397) ^ (int)TriggerResult;
                hashCode = (hashCode * 397) ^ TriggerContextKind;
                hashCode = (hashCode * 397) ^ TriggerOriginKind;
                return hashCode;
            }
        }

        private static string NormalizeSearchText(string searchText)
        {
            return string.IsNullOrWhiteSpace(searchText) ? string.Empty : searchText.Trim();
        }
    }
}
