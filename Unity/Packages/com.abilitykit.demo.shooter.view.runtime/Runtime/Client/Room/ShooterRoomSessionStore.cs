#nullable enable

using System;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterRoomSessionStore : IDisposable
    {
        private readonly IShooterRoomGatewaySnapshotFeed? _feed;
        private readonly object _gate = new object();
        private ShooterRoomSessionSnapshot? _current;
        private string _ignoredRoomId = string.Empty;
        private bool _disposed;

        public ShooterRoomSessionStore(IShooterRoomGatewaySnapshotFeed? feed = null)
        {
            _feed = feed;
            if (_feed == null) return;
            _feed.SnapshotChanged += HandleSnapshotChanged;
            if (_feed.Current != null) TryApply(_feed.Current);
        }

        public ShooterRoomSessionSnapshot? Current
        {
            get { lock (_gate) return _current; }
        }

        public event Action<ShooterRoomSessionSnapshot?>? SnapshotChanged;

        public bool TryApply(ShooterGatewayStagedRoomSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (string.IsNullOrWhiteSpace(snapshot.RoomId)) return false;

            var projected = ShooterRoomSessionSnapshot.FromGateway(snapshot);
            lock (_gate)
            {
                if (_disposed) return false;
                if (string.Equals(_ignoredRoomId, projected.RoomId, StringComparison.Ordinal)) return false;
                if (_current != null &&
                    string.Equals(_current.RoomId, projected.RoomId, StringComparison.Ordinal) &&
                    projected.RoomRevision <= _current.RoomRevision)
                {
                    return false;
                }

                _current = projected;
            }

            SnapshotChanged?.Invoke(projected);
            return true;
        }

        public void Reset()
        {
            ResetCore(string.Empty);
        }

        public void IgnoreAndReset(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));
            ResetCore(roomId);
        }

        private void ResetCore(string ignoredRoomId)
        {
            var changed = false;
            lock (_gate)
            {
                if (_disposed) return;
                _ignoredRoomId = ignoredRoomId ?? string.Empty;
                changed = _current != null;
                _current = null;
            }

            if (changed) SnapshotChanged?.Invoke(null);
        }

        private void HandleSnapshotChanged(ShooterGatewayStagedRoomSnapshot snapshot)
        {
            TryApply(snapshot);
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _current = null;
                _ignoredRoomId = string.Empty;
            }

            if (_feed != null) _feed.SnapshotChanged -= HandleSnapshotChanged;
            SnapshotChanged = null;
        }
    }
}
