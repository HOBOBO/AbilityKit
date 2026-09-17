using System;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    public enum BattleDiagnosticActionDamageOutcome
    {
        Unknown, Applied, FullyAbsorbed, NoHpDamage, Rejected, ExecutionFailed, PostCommitNotificationFailed
    }

    public readonly struct BattleDiagnosticActionDamageResult : IEquatable<BattleDiagnosticActionDamageResult>
    {
        public BattleDiagnosticActionDamageResult(long sequence, int frame, long sourceActorId, long targetActorId,
            long originContextId, in BattleDiagnosticDamageCalculationPayload calculation, string detail)
        {
            Sequence = sequence; Frame = frame; SourceActorId = sourceActorId; TargetActorId = targetActorId;
            OriginContextId = originContextId; Calculation = calculation;
            Detail = detail == null ? string.Empty : detail.Length <= 256 ? detail : detail.Substring(0, 256);
        }
        public long Sequence { get; }
        public int Frame { get; }
        public long SourceActorId { get; }
        public long TargetActorId { get; }
        public long OriginContextId { get; }
        public BattleDiagnosticDamageCalculationPayload Calculation { get; }
        public string Detail { get; }
        public bool Equals(BattleDiagnosticActionDamageResult other) => Sequence == other.Sequence && Frame == other.Frame &&
            SourceActorId == other.SourceActorId && TargetActorId == other.TargetActorId && OriginContextId == other.OriginContextId &&
            Calculation.Stage == other.Calculation.Stage && Calculation.BaseDamageRaw == other.Calculation.BaseDamageRaw &&
            Calculation.RawDamageRaw == other.Calculation.RawDamageRaw && Calculation.MitigatedDamageRaw == other.Calculation.MitigatedDamageRaw &&
            Calculation.ShieldAbsorbRaw == other.Calculation.ShieldAbsorbRaw && Calculation.PlannedHpDamageRaw == other.Calculation.PlannedHpDamageRaw &&
            Calculation.AppliedHpDamageRaw == other.Calculation.AppliedHpDamageRaw &&
            string.Equals(Detail ?? string.Empty, other.Detail ?? string.Empty, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is BattleDiagnosticActionDamageResult other && Equals(other);
        public override int GetHashCode() => Sequence.GetHashCode() ^ Frame ^ (int)Calculation.Stage;
        public BattleDiagnosticActionDamageOutcome Outcome
        {
            get
            {
                switch (Calculation.Stage)
                {
                    case BattleDiagnosticDamageStage.Completed:
                        if (Calculation.AppliedHpDamageRaw > 0) return BattleDiagnosticActionDamageOutcome.Applied;
                        if (Calculation.PlannedHpDamageRaw <= 0 && Calculation.MitigatedDamageRaw > 0 &&
                            Calculation.ShieldAbsorbRaw >= Calculation.MitigatedDamageRaw)
                            return BattleDiagnosticActionDamageOutcome.FullyAbsorbed;
                        return Calculation.PlannedHpDamageRaw <= 0 ? BattleDiagnosticActionDamageOutcome.NoHpDamage :
                            BattleDiagnosticActionDamageOutcome.Unknown;
                    case BattleDiagnosticDamageStage.TargetMissing:
                    case BattleDiagnosticDamageStage.ShieldCommitRejected:
                    case BattleDiagnosticDamageStage.HealthCommitRejected:
                    case BattleDiagnosticDamageStage.InvalidRequest:
                    case BattleDiagnosticDamageStage.TransactionRejected:
                        return BattleDiagnosticActionDamageOutcome.Rejected;
                    case BattleDiagnosticDamageStage.ExecutionFailed:
                        return BattleDiagnosticActionDamageOutcome.ExecutionFailed;
                    case BattleDiagnosticDamageStage.PostCommitNotificationFailed:
                        return BattleDiagnosticActionDamageOutcome.PostCommitNotificationFailed;
                    default: return BattleDiagnosticActionDamageOutcome.Unknown;
                }
            }
        }
    }
}
