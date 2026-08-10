using System;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    public readonly struct BattleDiagnosticHealthSnapshot : IEquatable<BattleDiagnosticHealthSnapshot>
    {
        public BattleDiagnosticHealthSnapshot(
            BattleDiagnosticSessionInfo sessionInfo,
            long eventStoreRevision,
            long stateStoreRevision,
            long traceStoreRevision,
            int lastSuccessfulStateFrame,
            long lastEventSequence,
            BattleDiagnosticEventChannel enabledChannels,
            bool isFrozen,
            BattleDiagnosticStoreMetrics eventStoreMetrics,
            long stateSampleFailureCount,
            long eventCollectFailureCount,
            string lastStateSampleError,
            string lastEventCollectError)
        {
            SessionInfo = sessionInfo;
            EventStoreRevision = eventStoreRevision;
            StateStoreRevision = stateStoreRevision;
            TraceStoreRevision = traceStoreRevision;
            LastSuccessfulStateFrame = lastSuccessfulStateFrame;
            LastEventSequence = lastEventSequence;
            EnabledChannels = enabledChannels;
            IsFrozen = isFrozen;
            EventStoreMetrics = eventStoreMetrics;
            StateSampleFailureCount = stateSampleFailureCount;
            EventCollectFailureCount = eventCollectFailureCount;
            LastStateSampleError = NormalizeError(lastStateSampleError);
            LastEventCollectError = NormalizeError(lastEventCollectError);
        }

        public BattleDiagnosticSessionInfo SessionInfo { get; }
        public long EventStoreRevision { get; }
        public long StateStoreRevision { get; }
        public long TraceStoreRevision { get; }
        public int LastSuccessfulStateFrame { get; }
        public long LastEventSequence { get; }
        public BattleDiagnosticEventChannel EnabledChannels { get; }
        public bool IsFrozen { get; }
        public BattleDiagnosticStoreMetrics EventStoreMetrics { get; }
        public long StateSampleFailureCount { get; }
        public long EventCollectFailureCount { get; }
        public string LastStateSampleError { get; }
        public string LastEventCollectError { get; }

        public bool IsValid => SessionInfo.IsValid;
        public bool HasErrors =>
            !string.IsNullOrEmpty(LastStateSampleError) ||
            !string.IsNullOrEmpty(LastEventCollectError);
        public bool HasProducedState =>
            StateStoreRevision > 0 && BattleDiagnosticFrames.IsValid(LastSuccessfulStateFrame);
        public bool HasProducedEvents => EventStoreRevision > 0 && LastEventSequence > 0;

        public bool Equals(BattleDiagnosticHealthSnapshot other)
        {
            return SessionInfo.Equals(other.SessionInfo) &&
                   EventStoreRevision == other.EventStoreRevision &&
                   StateStoreRevision == other.StateStoreRevision &&
                   TraceStoreRevision == other.TraceStoreRevision &&
                   LastSuccessfulStateFrame == other.LastSuccessfulStateFrame &&
                   LastEventSequence == other.LastEventSequence &&
                   EnabledChannels == other.EnabledChannels &&
                   IsFrozen == other.IsFrozen &&
                   EventStoreMetrics.Equals(other.EventStoreMetrics) &&
                   StateSampleFailureCount == other.StateSampleFailureCount &&
                   EventCollectFailureCount == other.EventCollectFailureCount &&
                   string.Equals(LastStateSampleError, other.LastStateSampleError, StringComparison.Ordinal) &&
                   string.Equals(LastEventCollectError, other.LastEventCollectError, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is BattleDiagnosticHealthSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = SessionInfo.GetHashCode();
                hashCode = (hashCode * 397) ^ EventStoreRevision.GetHashCode();
                hashCode = (hashCode * 397) ^ StateStoreRevision.GetHashCode();
                hashCode = (hashCode * 397) ^ TraceStoreRevision.GetHashCode();
                hashCode = (hashCode * 397) ^ LastSuccessfulStateFrame;
                hashCode = (hashCode * 397) ^ LastEventSequence.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)EnabledChannels;
                hashCode = (hashCode * 397) ^ IsFrozen.GetHashCode();
                hashCode = (hashCode * 397) ^ EventStoreMetrics.GetHashCode();
                hashCode = (hashCode * 397) ^ StateSampleFailureCount.GetHashCode();
                hashCode = (hashCode * 397) ^ EventCollectFailureCount.GetHashCode();
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(LastStateSampleError ?? string.Empty);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(LastEventCollectError ?? string.Empty);
                return hashCode;
            }
        }

        private static string NormalizeError(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            const int maxLength = 512;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }

    public interface IBattleDiagnosticHealthReadStore
    {
        BattleDiagnosticHealthSnapshot CaptureHealthSnapshot();
    }
}
