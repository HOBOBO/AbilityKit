using System;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    public readonly struct BattleDiagnosticEffectExecutionFacts : IEquatable<BattleDiagnosticEffectExecutionFacts>
    {
        private readonly bool _initialized;
        private readonly BattleDiagnosticDataAvailability _availability;
        public BattleDiagnosticDataAvailability Availability => _initialized ? _availability : BattleDiagnosticDataAvailability.NotCaptured;
        public bool IsCaptured => Availability == BattleDiagnosticDataAvailability.Available;
        public long SnapshotId { get; }
        public long Generation { get; }
        public int Frame { get; }
        public string TypeId { get; }
        public int SchemaVersion { get; }
        public int EffectConfigId { get; }
        public int TriggerId { get; }
        public string PayloadTypeName { get; }
        public bool HasRuntimeContext { get; }
        public long RuntimeContextId { get; }
        public long RuntimeContextVersion { get; }
        public bool HasStageSnapshot { get; }
        public int StackCount { get; }
        public float ElapsedSeconds { get; }
        public float RemainingSeconds { get; }
        public float DurationSeconds { get; }

        public BattleDiagnosticEffectExecutionFacts(BattleDiagnosticDataAvailability availability,
            long snapshotId = 0, long generation = 0, int frame = 0, string typeId = "", int schemaVersion = 0,
            int effectConfigId = 0, int triggerId = 0, string payloadTypeName = "", bool hasRuntimeContext = false,
            long runtimeContextId = 0, long runtimeContextVersion = 0, bool hasStageSnapshot = false,
            int stackCount = 0, float elapsedSeconds = 0, float remainingSeconds = 0, float durationSeconds = 0)
        {
            _initialized = true;
            _availability = availability;
            SnapshotId = snapshotId;
            Generation = generation;
            Frame = frame;
            TypeId = typeId ?? string.Empty;
            SchemaVersion = schemaVersion;
            EffectConfigId = effectConfigId;
            TriggerId = triggerId;
            PayloadTypeName = payloadTypeName ?? string.Empty;
            HasRuntimeContext = hasRuntimeContext;
            RuntimeContextId = runtimeContextId;
            RuntimeContextVersion = runtimeContextVersion;
            HasStageSnapshot = hasStageSnapshot;
            StackCount = stackCount;
            ElapsedSeconds = elapsedSeconds;
            RemainingSeconds = remainingSeconds;
            DurationSeconds = durationSeconds;
        }

        public static BattleDiagnosticEffectExecutionFacts Missing(BattleDiagnosticDataAvailability availability) =>
            new BattleDiagnosticEffectExecutionFacts(availability);

        public bool Equals(BattleDiagnosticEffectExecutionFacts other) => Availability == other.Availability &&
            SnapshotId == other.SnapshotId && Generation == other.Generation && Frame == other.Frame &&
            string.Equals(TypeId ?? string.Empty, other.TypeId ?? string.Empty, StringComparison.Ordinal) &&
            SchemaVersion == other.SchemaVersion && EffectConfigId == other.EffectConfigId && TriggerId == other.TriggerId &&
            string.Equals(PayloadTypeName ?? string.Empty, other.PayloadTypeName ?? string.Empty, StringComparison.Ordinal) &&
            HasRuntimeContext == other.HasRuntimeContext && RuntimeContextId == other.RuntimeContextId &&
            RuntimeContextVersion == other.RuntimeContextVersion && HasStageSnapshot == other.HasStageSnapshot &&
            StackCount == other.StackCount && ElapsedSeconds.Equals(other.ElapsedSeconds) &&
            RemainingSeconds.Equals(other.RemainingSeconds) && DurationSeconds.Equals(other.DurationSeconds);

        public override bool Equals(object obj) => obj is BattleDiagnosticEffectExecutionFacts other && Equals(other);
        public override int GetHashCode() => SnapshotId.GetHashCode() ^ Generation.GetHashCode() ^ (int)Availability;
    }
}
