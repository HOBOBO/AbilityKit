using System;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    [Flags]
    public enum BattleDiagnosticCapabilities : long
    {
        None = 0,
        WorldState = 1L << 0,
        ActorState = 1L << 1,
        Events = 1L << 2,
        Trace = 1L << 3,
        SkillRuntime = 1L << 4,
        FreezeCapture = 1L << 5,
        Clear = 1L << 6,
        PinTrace = 1L << 7,
        Export = 1L << 8,
        SelfMetrics = 1L << 9,
        ActorAttributes = 1L << 10,
        ActorBuffs = 1L << 11,
        ActorTags = 1L << 12,
        ActorEffects = 1L << 13,
        RuntimeObjects = 1L << 14,
        FrameMetrics = 1L << 15,
        AllLocal = WorldState |
                   ActorState |
                   Events |
                   Trace |
                   SkillRuntime |
                   ActorAttributes |
                   ActorBuffs |
                   ActorTags |
                   ActorEffects |
                   RuntimeObjects |
                   FrameMetrics |
                   FreezeCapture |
                   Clear |
                   PinTrace |
                   Export |
                   SelfMetrics
    }

    public enum BattleDiagnosticConnectionState
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2,
        Faulted = 3
    }

    public enum BattleDiagnosticCaptureState
    {
        Off = 0,
        Capturing = 1,
        Frozen = 2
    }

    public readonly struct BattleDiagnosticRuntimeHandle : IEquatable<BattleDiagnosticRuntimeHandle>
    {
        public BattleDiagnosticRuntimeHandle(long runtimeId, int generation)
        {
            RuntimeId = runtimeId;
            Generation = generation;
        }

        public long RuntimeId { get; }
        public int Generation { get; }
        public bool IsValid => RuntimeId != 0 && Generation >= 0;

        public bool Equals(BattleDiagnosticRuntimeHandle other)
        {
            return RuntimeId == other.RuntimeId && Generation == other.Generation;
        }

        public override bool Equals(object obj)
        {
            return obj is BattleDiagnosticRuntimeHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (RuntimeId.GetHashCode() * 397) ^ Generation;
            }
        }

        public override string ToString()
        {
            return IsValid ? $"{RuntimeId}:{Generation}" : "<invalid>";
        }

        public static bool operator ==(BattleDiagnosticRuntimeHandle left, BattleDiagnosticRuntimeHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BattleDiagnosticRuntimeHandle left, BattleDiagnosticRuntimeHandle right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct BattleDiagnosticSessionInfo : IEquatable<BattleDiagnosticSessionInfo>
    {
        public BattleDiagnosticSessionInfo(
            BattleDiagnosticSessionScope scope,
            string displayName,
            string buildId,
            int schemaVersion,
            long monotonicTimestampFrequency,
            BattleDiagnosticCapabilities capabilities,
            BattleDiagnosticConnectionState connectionState,
            BattleDiagnosticCaptureState captureState)
        {
            if (schemaVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            }

            if (monotonicTimestampFrequency <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(monotonicTimestampFrequency));
            }

            Scope = scope;
            DisplayName = displayName ?? string.Empty;
            BuildId = buildId ?? string.Empty;
            SchemaVersion = schemaVersion;
            MonotonicTimestampFrequency = monotonicTimestampFrequency;
            Capabilities = capabilities;
            ConnectionState = connectionState;
            CaptureState = captureState;
        }

        public BattleDiagnosticSessionScope Scope { get; }
        public string DisplayName { get; }
        public string BuildId { get; }
        public int SchemaVersion { get; }
        public long MonotonicTimestampFrequency { get; }
        public BattleDiagnosticCapabilities Capabilities { get; }
        public BattleDiagnosticConnectionState ConnectionState { get; }
        public BattleDiagnosticCaptureState CaptureState { get; }
        public bool IsValid => Scope.IsValid && SchemaVersion >= 1 && MonotonicTimestampFrequency > 0;

        public bool Supports(BattleDiagnosticCapabilities capability)
        {
            return (Capabilities & capability) == capability;
        }

        public bool Equals(BattleDiagnosticSessionInfo other)
        {
            return Scope.Equals(other.Scope) &&
                   string.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal) &&
                   string.Equals(BuildId, other.BuildId, StringComparison.Ordinal) &&
                   SchemaVersion == other.SchemaVersion &&
                   MonotonicTimestampFrequency == other.MonotonicTimestampFrequency &&
                   Capabilities == other.Capabilities &&
                   ConnectionState == other.ConnectionState &&
                   CaptureState == other.CaptureState;
        }

        public override bool Equals(object obj)
        {
            return obj is BattleDiagnosticSessionInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Scope.GetHashCode();
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(DisplayName ?? string.Empty);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(BuildId ?? string.Empty);
                hashCode = (hashCode * 397) ^ SchemaVersion;
                hashCode = (hashCode * 397) ^ MonotonicTimestampFrequency.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Capabilities;
                hashCode = (hashCode * 397) ^ (int)ConnectionState;
                hashCode = (hashCode * 397) ^ (int)CaptureState;
                return hashCode;
            }
        }
    }
}
