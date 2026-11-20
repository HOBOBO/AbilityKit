using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Game.Flow.Battle.FrameSync
{
    public sealed class ClientPredictionDriverStatsFramesSource : IWorldAuthorityFramesSource, IWorldRestoreFloorSource
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

        /// <summary>
        /// 只经预测驱动的显式恢复边界读取，不复用统计确认帧。
        /// </summary>
        public bool TryGetRestoreFloor(WorldId worldId, out FrameIndex frame)
        {
            frame = default;
            return _stats is IClientPredictionRestoreFloor boundary && boundary.TryGetRestoreFloor(worldId, out frame);
        }
    }
}
