#nullable enable

using System.Collections.Generic;

namespace AbilityKit.Network.Runtime.Sync
{
    /// <summary>
    /// A read-only snapshot of network synchronization health at a point in time.
    /// Aggregates the key metrics from <see cref="SyncClock"/>, <see cref="SyncRecoveryState"/>,
    /// <see cref="InterpolationDiagnostics"/> and <see cref="NetworkConditioningStats"/> into one struct
    /// that any monitoring/debugging UI can query without demo-specific knowledge.
    /// </summary>
    public readonly struct NetworkDiagnosticsSnapshot
    {
        /// <summary>Estimated server round-trip time (ms), or -1 if unknown.</summary>
        public readonly double EstimatedRttMs;

        /// <summary>Clock offset between local and server (ms), or 0 if unknown.</summary>
        public readonly double ClockOffsetMs;

        /// <summary>Current logical frame the client is rendering.</summary>
        public readonly int CurrentFrame;

        /// <summary>Last authoritative frame received from server, or -1.</summary>
        public readonly int LastAuthoritativeFrame;

        /// <summary>Frame gap between client render and last authority (positive = client ahead).</summary>
        public readonly int FrameGap;

        /// <summary>Total resync requests since connection.</summary>
        public readonly int ResyncCount;

        /// <summary>Total snapshots received.</summary>
        public readonly int SnapshotsReceived;

        /// <summary>Total inputs submitted.</summary>
        public readonly int InputsSubmitted;

        /// <summary>Total inputs rejected by server.</summary>
        public readonly int InputsRejected;

        /// <summary>Current fast-reconnect phase (<see cref="FastReconnectPhase"/>), or Normal.</summary>
        public readonly FastReconnectPhase ReconnectPhase;

        /// <summary>Recent sync health events (capped, newest-first).</summary>
        public readonly IReadOnlyList<SyncHealthEvent> RecentHealthEvents;

        /// <summary>True if the connection is currently healthy (connected, no active recovery).</summary>
        public bool IsHealthy =>
            ResyncCount == 0
            && ReconnectPhase == FastReconnectPhase.Connected
            && FrameGap is >= -10 and <= 30;

        public NetworkDiagnosticsSnapshot(
            double estimatedRttMs,
            double clockOffsetMs,
            int currentFrame,
            int lastAuthoritativeFrame,
            int frameGap,
            int resyncCount,
            int snapshotsReceived,
            int inputsSubmitted,
            int inputsRejected,
            FastReconnectPhase reconnectPhase,
            IReadOnlyList<SyncHealthEvent>? recentHealthEvents)
        {
            EstimatedRttMs = estimatedRttMs;
            ClockOffsetMs = clockOffsetMs;
            CurrentFrame = currentFrame;
            LastAuthoritativeFrame = lastAuthoritativeFrame;
            FrameGap = frameGap;
            ResyncCount = resyncCount;
            SnapshotsReceived = snapshotsReceived;
            InputsSubmitted = inputsSubmitted;
            InputsRejected = inputsRejected;
            ReconnectPhase = reconnectPhase;
            RecentHealthEvents = recentHealthEvents ?? System.Array.Empty<SyncHealthEvent>();
        }

        public static NetworkDiagnosticsSnapshot Empty => new(-1, 0, 0, -1, 0, 0, 0, 0, 0, FastReconnectPhase.Connected, null);
    }

    /// <summary>
    /// Exposes a unified network diagnostics snapshot for monitoring/debugging UIs.
    /// Implement in your sync controller or session; consume from any UI without demo-specific types.
    /// </summary>
    public interface INetworkDiagnostics
    {
        /// <summary>Current network diagnostics. Safe to call from any thread (values may be slightly stale).</summary>
        NetworkDiagnosticsSnapshot GetDiagnostics();
    }
}
