using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Server;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaSnapshotRouter : IWorldStateSnapshotProvider
    {
        private readonly MobaEnterGameSnapshotService _enter;
        private readonly MobaActorTransformSnapshotService _transform;
        private readonly MobaLobbySnapshotService _lobby;

        public MobaSnapshotRouter(MobaEnterGameSnapshotService enter, MobaActorTransformSnapshotService transform, MobaLobbySnapshotService lobby)
        {
            _enter = enter ?? throw new ArgumentNullException(nameof(enter));
            _transform = transform ?? throw new ArgumentNullException(nameof(transform));
            _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
        }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            if (_enter.TryGetSnapshot(frame, out snapshot)) return true;
            if (_transform.TryGetSnapshot(frame, out snapshot)) return true;
            return _lobby.TryGetSnapshot(frame, out snapshot);
        }
    }
}
