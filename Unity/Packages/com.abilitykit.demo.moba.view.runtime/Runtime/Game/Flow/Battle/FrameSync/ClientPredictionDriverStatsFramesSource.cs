using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Game.Flow.Battle.FrameSync
{
    public sealed class ClientPredictionDriverStatsFramesSource : IWorldAuthorityFramesSource
    {
        private readonly IClientPredictionDriverStats _stats;

        public ClientPredictionDriverStatsFramesSource(IClientPredictionDriverStats stats)
        {
            _stats = stats;
        }

        public bool TryGetFrames(WorldId worldId, out FrameIndex confirmed, out FrameIndex predicted)
        {
            confirmed = default;
            predicted = default;
            if (_stats == null) return false;
            return _stats.TryGetFrames(worldId, out confirmed, out predicted);
        }
    }
}
