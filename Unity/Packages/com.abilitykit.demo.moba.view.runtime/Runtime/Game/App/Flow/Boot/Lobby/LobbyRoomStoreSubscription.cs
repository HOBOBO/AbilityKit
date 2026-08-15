using System;
using AbilityKit.Game.Battle.Agent;

namespace AbilityKit.Game.Flow
{
    internal sealed class LobbyRoomStoreSubscription : IDisposable
    {
        private ClientRoomStore _store;
        private Action<ClientRoomSnapshot> _snapshotChanged;
        private Action<ClientRoomMembershipChange> _membershipChanged;
        private Action<ClientRoomPlayerStateChanges> _playerStateChanged;
        private int _generation;

        public ClientRoomStore Store => _store;
        public bool IsStale => _store?.IsStale == true;

        public void Attach(
            ClientRoomStore store,
            Action<ClientRoomSnapshot> snapshotChanged,
            Action<ClientRoomMembershipChange> membershipChanged,
            Action<ClientRoomPlayerStateChanges> playerStateChanged)
        {
            Detach();
            if (store == null) return;

            var generation = _generation;
            _store = store;
            _snapshotChanged = snapshot =>
            {
                if (generation == _generation) snapshotChanged?.Invoke(snapshot);
            };
            _membershipChanged = change =>
            {
                if (generation == _generation) membershipChanged?.Invoke(change);
            };
            _playerStateChanged = changes =>
            {
                if (generation == _generation) playerStateChanged?.Invoke(changes);
            };

            _store.OnSnapshotChanged += _snapshotChanged;
            _store.OnMembershipChanged += _membershipChanged;
            _store.OnPlayerStateChanged += _playerStateChanged;

            if (_store.Current != null)
            {
                _snapshotChanged(_store.Current);
            }
        }

        public void Detach()
        {
            _generation++;
            if (_store != null)
            {
                _store.OnSnapshotChanged -= _snapshotChanged;
                _store.OnMembershipChanged -= _membershipChanged;
                _store.OnPlayerStateChanged -= _playerStateChanged;
            }

            _store = null;
            _snapshotChanged = null;
            _membershipChanged = null;
            _playerStateChanged = null;
        }

        public void Dispose()
        {
            Detach();
        }
    }
}
