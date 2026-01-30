using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaLobbySnapshotService : IService
    {
        private readonly MobaLobbyStateService _lobby;
        private int _lastVersion;
        private FrameIndex _lastFrame;

        public MobaLobbySnapshotService(MobaLobbyStateService lobby)
        {
            _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            _lastVersion = -1;
            _lastFrame = new FrameIndex(-999999);
        }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            // Don’t spam every frame; send on version change or at most every 10 frames.
            var version = _lobby.Version;
            var shouldSend = version != _lastVersion || (frame.Value - _lastFrame.Value) >= 10;

            if (!shouldSend)
            {
                snapshot = default;
                return false;
            }

            _lastVersion = version;
            _lastFrame = frame;

            var players = _lobby.GetPlayers();
            var payload = MobaLobbyCodec.SerializeSnapshot(_lobby.Started, players, version);
            snapshot = new WorldStateSnapshot((int)MobaOpCode.LobbySnapshot, payload);
            return true;
        }

        public void Dispose()
        {
            _lastVersion = -1;
            _lastFrame = new FrameIndex(-999999);
        }
    }
}
