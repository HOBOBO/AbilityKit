#nullable enable

using System;
using System.Collections.Generic;

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
        public event Action<ShooterRoomSessionChange>? RoomChanged;

        public bool TryApply(ShooterGatewayStagedRoomSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (string.IsNullOrWhiteSpace(snapshot.RoomId)) return false;

            var projected = ShooterRoomSessionSnapshot.FromGateway(snapshot);
            ShooterRoomSessionChange? change = null;
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

                if (_current != null &&
                    string.Equals(_current.RoomId, projected.RoomId, StringComparison.Ordinal))
                {
                    change = BuildChange(_current, projected);
                }

                _current = projected;
            }

            SnapshotChanged?.Invoke(projected);
            if (change?.HasChanges == true) RoomChanged?.Invoke(change);
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

        private static ShooterRoomSessionChange BuildChange(
            ShooterRoomSessionSnapshot previous,
            ShooterRoomSessionSnapshot current)
        {
            var previousByAccount = IndexMembers(previous.Members);
            var currentByAccount = IndexMembers(current.Members);
            var joined = new List<string>();
            var left = new List<string>();
            var memberChanges = new List<ShooterRoomSessionMemberChange>();

            for (var i = 0; i < current.Members.Count; i++)
            {
                var member = current.Members[i];
                if (!previousByAccount.TryGetValue(member.AccountId, out var oldMember))
                {
                    joined.Add(member.AccountId);
                    continue;
                }

                if (oldMember.IsOnline != member.IsOnline || oldMember.LobbyReady != member.LobbyReady)
                {
                    memberChanges.Add(new ShooterRoomSessionMemberChange(
                        member.AccountId,
                        oldMember.IsOnline,
                        member.IsOnline,
                        oldMember.LobbyReady,
                        member.LobbyReady));
                }
            }

            for (var i = 0; i < previous.Members.Count; i++)
            {
                var member = previous.Members[i];
                if (!currentByAccount.ContainsKey(member.AccountId)) left.Add(member.AccountId);
            }

            return new ShooterRoomSessionChange(
                current.RoomId,
                previous.RoomRevision,
                current.RoomRevision,
                previous.OwnerAccountId,
                current.OwnerAccountId,
                previous.Phase,
                current.Phase,
                current.PhaseReason,
                joined,
                left,
                memberChanges);
        }

        private static Dictionary<string, ShooterRoomSessionMember> IndexMembers(
            IReadOnlyList<ShooterRoomSessionMember> members)
        {
            var result = new Dictionary<string, ShooterRoomSessionMember>(StringComparer.Ordinal);
            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (!string.IsNullOrWhiteSpace(member.AccountId)) result[member.AccountId] = member;
            }

            return result;
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
            RoomChanged = null;
        }
    }
}
