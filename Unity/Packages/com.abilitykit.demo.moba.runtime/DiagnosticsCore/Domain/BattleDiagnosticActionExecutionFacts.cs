using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    public enum BattleDiagnosticActionOutcome { EntryOnly, Completed, Failed, Aborted }

    public readonly struct BattleDiagnosticActionActorValues
    {
        public BattleDiagnosticActionActorValues(long actorId, long bindingId, bool hasActor,
            bool hasHp = false, float hp = 0, bool hasMana = false, float mana = 0)
        {
            ActorId = actorId; BindingId = bindingId; HasActor = hasActor;
            HasHp = hasHp; Hp = hp; HasMana = hasMana; Mana = mana;
        }
        public long ActorId { get; }
        public long BindingId { get; }
        public bool HasActor { get; }
        public bool HasHp { get; }
        public float Hp { get; }
        public bool HasMana { get; }
        public float Mana { get; }
        public bool IsSameBinding(in BattleDiagnosticActionActorValues other) =>
            HasActor && other.HasActor && ActorId == other.ActorId && BindingId == other.BindingId;
    }

    public readonly struct BattleDiagnosticActionHealthCommit
    {
        public BattleDiagnosticActionHealthCommit(int kind, long sourceActorId, long targetActorId,
            int valueType, int reasonKind, int reasonParam, float requestedValue, float appliedValue,
            float oldHp, float targetHp, float targetMaxHp, long originContextId)
        {
            Kind = kind; SourceActorId = sourceActorId; TargetActorId = targetActorId;
            ValueType = valueType; ReasonKind = reasonKind; ReasonParam = reasonParam;
            RequestedValue = requestedValue; AppliedValue = appliedValue;
            OldHp = oldHp; TargetHp = targetHp; TargetMaxHp = targetMaxHp; OriginContextId = originContextId;
        }
        public int Kind { get; }
        public long SourceActorId { get; }
        public long TargetActorId { get; }
        public int ValueType { get; }
        public int ReasonKind { get; }
        public int ReasonParam { get; }
        public float RequestedValue { get; }
        public float AppliedValue { get; }
        public float OldHp { get; }
        public float TargetHp { get; }
        public float TargetMaxHp { get; }
        public long OriginContextId { get; }
    }

    public readonly struct BattleDiagnosticActionExecutionFacts : IEquatable<BattleDiagnosticActionExecutionFacts>
    {
        private readonly bool _initialized;
        private readonly BattleDiagnosticDataAvailability _availability;
        private readonly IReadOnlyList<BattleDiagnosticActionHealthCommit> _commits;
        private readonly IReadOnlyList<BattleDiagnosticActionDamageResult> _damageResults;
        private readonly BattleDiagnosticDataAvailability _damageAvailability;
        public BattleDiagnosticActionExecutionFacts(BattleDiagnosticDataAvailability availability,
            long snapshotId = 0, long generation = 0, int frame = 0, string typeId = "", int schemaVersion = 0,
            int actionIndex = -1, long actionId = 0, BattleDiagnosticActionOutcome outcome = default,
            bool hasAfter = false, int endFrame = -1, bool commitsComplete = false, bool commitsTruncated = false,
            BattleDiagnosticActionActorValues sourceBefore = default, BattleDiagnosticActionActorValues targetBefore = default,
            BattleDiagnosticActionActorValues sourceAfter = default, BattleDiagnosticActionActorValues targetAfter = default,
            IEnumerable<BattleDiagnosticActionHealthCommit> commits = null,
            BattleDiagnosticDataAvailability damageAvailability = BattleDiagnosticDataAvailability.NotCaptured,
            bool damageCoverageContinuous = false, bool damageResultsTruncated = false,
            IEnumerable<BattleDiagnosticActionDamageResult> damageResults = null)
        {
            _initialized = true; _availability = availability;
            SnapshotId = snapshotId; Generation = generation; Frame = frame; TypeId = typeId ?? string.Empty;
            SchemaVersion = schemaVersion; ActionIndex = actionIndex; ActionId = actionId; Outcome = outcome;
            HasAfter = hasAfter; EndFrame = endFrame; CommitsComplete = commitsComplete; CommitsTruncated = commitsTruncated;
            SourceBefore = sourceBefore; TargetBefore = targetBefore; SourceAfter = sourceAfter; TargetAfter = targetAfter;
            _commits = commits == null ? Array.Empty<BattleDiagnosticActionHealthCommit>() :
                new List<BattleDiagnosticActionHealthCommit>(commits).AsReadOnly();
            _damageAvailability = damageAvailability; DamageCoverageContinuous = damageCoverageContinuous;
            DamageResultsTruncated = damageResultsTruncated;
            _damageResults = damageResults == null ? Array.Empty<BattleDiagnosticActionDamageResult>() :
                new List<BattleDiagnosticActionDamageResult>(damageResults).AsReadOnly();
        }
        public BattleDiagnosticDataAvailability Availability => _initialized ? _availability : BattleDiagnosticDataAvailability.NotCaptured;
        public bool IsCaptured => Availability == BattleDiagnosticDataAvailability.Available;
        public long SnapshotId { get; }
        public long Generation { get; }
        public int Frame { get; }
        public string TypeId { get; }
        public int SchemaVersion { get; }
        public int ActionIndex { get; }
        public long ActionId { get; }
        public BattleDiagnosticActionOutcome Outcome { get; }
        public bool HasAfter { get; }
        public int EndFrame { get; }
        public bool CommitsComplete { get; }
        public bool CommitsTruncated { get; }
        public BattleDiagnosticActionActorValues SourceBefore { get; }
        public BattleDiagnosticActionActorValues TargetBefore { get; }
        public BattleDiagnosticActionActorValues SourceAfter { get; }
        public BattleDiagnosticActionActorValues TargetAfter { get; }
        public IReadOnlyList<BattleDiagnosticActionHealthCommit> Commits => _commits ?? Array.Empty<BattleDiagnosticActionHealthCommit>();
        public BattleDiagnosticDataAvailability DamageAvailability => _initialized ? _damageAvailability : BattleDiagnosticDataAvailability.NotCaptured;
        public bool DamageCoverageContinuous { get; }
        public bool DamageResultsTruncated { get; }
        public IReadOnlyList<BattleDiagnosticActionDamageResult> DamageResults => _damageResults ?? Array.Empty<BattleDiagnosticActionDamageResult>();
        public bool Equals(BattleDiagnosticActionExecutionFacts other)
        {
            if (Availability != other.Availability || SnapshotId != other.SnapshotId || Generation != other.Generation ||
                Frame != other.Frame || (TypeId ?? string.Empty) != (other.TypeId ?? string.Empty) || SchemaVersion != other.SchemaVersion ||
                ActionIndex != other.ActionIndex || ActionId != other.ActionId || Outcome != other.Outcome ||
                HasAfter != other.HasAfter || EndFrame != other.EndFrame || CommitsComplete != other.CommitsComplete ||
                CommitsTruncated != other.CommitsTruncated || !SourceBefore.Equals(other.SourceBefore) ||
                !TargetBefore.Equals(other.TargetBefore) || !SourceAfter.Equals(other.SourceAfter) ||
                !TargetAfter.Equals(other.TargetAfter) || Commits.Count != other.Commits.Count || DamageAvailability != other.DamageAvailability ||
                DamageCoverageContinuous != other.DamageCoverageContinuous || DamageResultsTruncated != other.DamageResultsTruncated ||
                DamageResults.Count != other.DamageResults.Count) return false;
            for (var i = 0; i < Commits.Count; i++) if (!Commits[i].Equals(other.Commits[i])) return false;
            for (var i = 0; i < DamageResults.Count; i++) if (!DamageResults[i].Equals(other.DamageResults[i])) return false;
            return true;
        }
        public override bool Equals(object obj) => obj is BattleDiagnosticActionExecutionFacts other && Equals(other);
        public override int GetHashCode() => SnapshotId.GetHashCode() ^ Generation.GetHashCode() ^ (int)Availability;
    }
}
