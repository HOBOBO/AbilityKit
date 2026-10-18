using System;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Demo.Moba.Share;

namespace AbilityKit.Game.Flow
{
    public interface IGameplayProjectionModule : IDisposable
    {
        void Bind(FrameSnapshotDispatcher snapshots, BattleGameplayPresentationState state);
    }

    public sealed class TeamDeathmatchProjectionModule : IGameplayProjectionModule
    {
        private IDisposable _match;
        private IDisposable _respawn;

        public void Bind(FrameSnapshotDispatcher snapshots, BattleGameplayPresentationState state)
        {
            if (snapshots == null) throw new ArgumentNullException(nameof(snapshots));
            if (state == null) throw new ArgumentNullException(nameof(state));
            Dispose();
            _match = snapshots.Subscribe<GameplayMatchData>(GameplayMatchSnapshotRoute.OpCode,
                (_, value) => state.Apply(in value));
            _respawn = snapshots.Subscribe<GameplayRespawnData[]>(GameplayRespawnSnapshotRoute.OpCode,
                (_, value) => state.Apply(value));
        }

        public void Dispose()
        {
            _match?.Dispose();
            _respawn?.Dispose();
            _match = null;
            _respawn = null;
        }
    }
}
