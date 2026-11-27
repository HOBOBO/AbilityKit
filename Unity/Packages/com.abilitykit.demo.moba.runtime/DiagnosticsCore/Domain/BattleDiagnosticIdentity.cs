using System;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    public static class BattleDiagnosticFrames
    {
        public const int Invalid = -1;

        public static bool IsValid(int frame)
        {
            return frame >= 0;
        }
    }

    public enum BattleDiagnosticDataAvailability
    {
        Available = 0,
        NotProduced = 1,
        NotCaptured = 2,
        Evicted = 3,
        Truncated = 4,
        Unsupported = 5,
        Disconnected = 6,
        Error = 7
    }

    public enum BattleDiagnosticSelectionKind
    {
        None = 0,
        Frame = 1,
        Actor = 2,
        Event = 3,
        TraceRoot = 4,
        TraceNode = 5,
        SkillRuntime = 6,
        Attack = 7,
        ConfigAsset = 8,
        Warning = 9,
        Exception = 10,
        ActorAttributeEvidence = 11
    }

    public readonly struct BattleDiagnosticSessionScope : IEquatable<BattleDiagnosticSessionScope>
    {
        public BattleDiagnosticSessionScope(string sessionId, string worldId, long worldEpoch)
        {
            SessionId = sessionId ?? string.Empty;
            WorldId = worldId ?? string.Empty;
            WorldEpoch = worldEpoch;
        }

        public string SessionId { get; }
        public string WorldId { get; }
        public long WorldEpoch { get; }

        public bool IsValid =>
            !string.IsNullOrEmpty(SessionId) &&
            !string.IsNullOrEmpty(WorldId) &&
            WorldEpoch >= 0;

        public bool Equals(BattleDiagnosticSessionScope other)
        {
            return string.Equals(SessionId, other.SessionId, StringComparison.Ordinal) &&
                   string.Equals(WorldId, other.WorldId, StringComparison.Ordinal) &&
                   WorldEpoch == other.WorldEpoch;
        }

        public override bool Equals(object obj)
        {
            return obj is BattleDiagnosticSessionScope other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = StringComparer.Ordinal.GetHashCode(SessionId ?? string.Empty);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(WorldId ?? string.Empty);
                hashCode = (hashCode * 397) ^ WorldEpoch.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString()
        {
            return IsValid ? $"{SessionId}/{WorldId}@{WorldEpoch}" : "<invalid>";
        }

        public static bool operator ==(BattleDiagnosticSessionScope left, BattleDiagnosticSessionScope right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BattleDiagnosticSessionScope left, BattleDiagnosticSessionScope right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct BattleDiagnosticSelection : IEquatable<BattleDiagnosticSelection>
    {
        public BattleDiagnosticSelection(
            BattleDiagnosticSessionScope scope,
            BattleDiagnosticSelectionKind kind,
            long id,
            int frame,
            long relatedId = 0)
        {
            Scope = scope;
            Kind = kind;
            Id = id;
            Frame = frame;
            RelatedId = relatedId;
        }

        public BattleDiagnosticSessionScope Scope { get; }
        public BattleDiagnosticSelectionKind Kind { get; }
        public long Id { get; }
        public int Frame { get; }
        public long RelatedId { get; }

        public bool IsValid =>
            Scope.IsValid &&
            Kind != BattleDiagnosticSelectionKind.None &&
            (Kind == BattleDiagnosticSelectionKind.Frame
                ? BattleDiagnosticFrames.IsValid(Frame)
                : Id != 0);

        public bool BelongsTo(BattleDiagnosticSessionScope scope)
        {
            return IsValid && Scope == scope;
        }

        public BattleDiagnosticSelection AtFrame(int frame)
        {
            return new BattleDiagnosticSelection(Scope, Kind, Id, frame, RelatedId);
        }

        public bool Equals(BattleDiagnosticSelection other)
        {
            return Scope.Equals(other.Scope) &&
                   Kind == other.Kind &&
                   Id == other.Id &&
                   Frame == other.Frame &&
                   RelatedId == other.RelatedId;
        }

        public override bool Equals(object obj)
        {
            return obj is BattleDiagnosticSelection other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Scope.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Kind;
                hashCode = (hashCode * 397) ^ Id.GetHashCode();
                hashCode = (hashCode * 397) ^ Frame;
                hashCode = (hashCode * 397) ^ RelatedId.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString()
        {
            return IsValid ? $"{Kind}:{Id}@{Frame} [{Scope}]" : "<none>";
        }

        public static bool operator ==(BattleDiagnosticSelection left, BattleDiagnosticSelection right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BattleDiagnosticSelection left, BattleDiagnosticSelection right)
        {
            return !left.Equals(right);
        }
    }
}
