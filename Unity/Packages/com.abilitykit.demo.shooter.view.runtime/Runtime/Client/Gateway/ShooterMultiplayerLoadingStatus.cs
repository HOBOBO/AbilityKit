#nullable enable

using System;
using AbilityKit.Ability.Host.Extensions.Session;

namespace AbilityKit.Demo.Shooter.View
{
    public enum ShooterMultiplayerLoadingState
    {
        Idle = 0,
        Loading = 1,
        WaitingForPlayers = 2,
        Started = 3,
        Failed = 4
    }

    public readonly struct ShooterMultiplayerLoadingView
    {
        public readonly string RoomId;
        public readonly long LaunchGeneration;
        public readonly ShooterMultiplayerLoadingState State;
        public readonly int LocalProgress;
        public readonly string Stage;
        public readonly RoomGatewaySnapshot? Snapshot;

        public ShooterMultiplayerLoadingView(
            string roomId,
            long launchGeneration,
            ShooterMultiplayerLoadingState state,
            int localProgress,
            string stage,
            RoomGatewaySnapshot? snapshot)
        {
            RoomId = roomId ?? string.Empty;
            LaunchGeneration = Math.Max(0L, launchGeneration);
            State = state;
            LocalProgress = Math.Max(0, Math.Min(100, localProgress));
            Stage = stage ?? string.Empty;
            Snapshot = snapshot;
        }
    }

    /// <summary>
    /// Read-only UI projection of the shared Room loading workflow.
    /// It does not own transport or room state; authoritative member progress comes from Room snapshots.
    /// </summary>
    public static class ShooterMultiplayerLoadingStatus
    {
        private static readonly object Gate = new object();
        private static ShooterMultiplayerLoadingView _current;

        public static ShooterMultiplayerLoadingView Current
        {
            get { lock (Gate) return _current; }
        }

        internal static void Reset()
        {
            lock (Gate) _current = default;
        }

        internal static void Begin(RoomGatewaySnapshot snapshot, string stage)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            lock (Gate)
            {
                _current = new ShooterMultiplayerLoadingView(
                    snapshot.RoomId,
                    snapshot.LaunchGeneration,
                    ShooterMultiplayerLoadingState.Loading,
                    0,
                    stage,
                    snapshot);
            }
        }

        internal static void Update(int progress, string stage, RoomGatewaySnapshot? snapshot)
        {
            lock (Gate)
            {
                if (!CanApply(snapshot)) return;
                var roomId = snapshot?.RoomId ?? _current.RoomId;
                var generation = snapshot?.LaunchGeneration ?? _current.LaunchGeneration;
                var state = progress >= 100
                    ? ShooterMultiplayerLoadingState.WaitingForPlayers
                    : (_current.State == ShooterMultiplayerLoadingState.Idle
                        ? ShooterMultiplayerLoadingState.Loading
                        : _current.State);
                _current = new ShooterMultiplayerLoadingView(
                    roomId,
                    generation,
                    state,
                    progress,
                    stage,
                    snapshot ?? _current.Snapshot);
            }
        }

        internal static void UpdateSnapshot(RoomGatewaySnapshot snapshot)
        {
            if (snapshot == null) return;
            lock (Gate)
            {
                if (!CanApply(snapshot)) return;
                _current = new ShooterMultiplayerLoadingView(
                    snapshot.RoomId,
                    snapshot.LaunchGeneration,
                    _current.State,
                    _current.LocalProgress,
                    _current.Stage,
                    snapshot);
            }
        }

        internal static void MarkStarted(RoomGatewaySnapshot snapshot)
        {
            if (snapshot == null) return;
            lock (Gate)
            {
                if (!CanApply(snapshot)) return;
                _current = new ShooterMultiplayerLoadingView(
                    snapshot.RoomId,
                    snapshot.LaunchGeneration,
                    ShooterMultiplayerLoadingState.Started,
                    100,
                    "Battle started",
                    snapshot);
            }
        }

        internal static void Fail(string roomId, long launchGeneration, string message)
        {
            lock (Gate)
            {
                if (!string.IsNullOrWhiteSpace(_current.RoomId) &&
                    (!string.Equals(_current.RoomId, roomId, StringComparison.Ordinal) ||
                     (_current.LaunchGeneration > 0L && _current.LaunchGeneration != launchGeneration)))
                {
                    return;
                }

                _current = new ShooterMultiplayerLoadingView(
                    roomId,
                    launchGeneration,
                    ShooterMultiplayerLoadingState.Failed,
                    _current.LocalProgress,
                    message,
                    _current.Snapshot);
            }
        }

        private static bool CanApply(RoomGatewaySnapshot? snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(_current.RoomId)) return true;
            if (!string.Equals(_current.RoomId, snapshot.RoomId, StringComparison.Ordinal)) return false;
            return _current.LaunchGeneration <= 0L ||
                   snapshot.LaunchGeneration <= 0L ||
                   _current.LaunchGeneration == snapshot.LaunchGeneration;
        }
    }
}
