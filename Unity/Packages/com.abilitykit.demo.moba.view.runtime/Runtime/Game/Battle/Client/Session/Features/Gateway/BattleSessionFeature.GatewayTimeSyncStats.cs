using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private TimeSyncStatsSnapshot BuildCurrentTimeSyncStats(uint opCode, int intervalMs, double alpha, int timeoutMs)
        {
            var worldIdValue = _plan.World.WorldId;
            var worldId = worldIdValue != null ? new WorldId(worldIdValue) : default;
            return BuildTimeSyncStats(worldId, opCode, intervalMs, alpha, timeoutMs);
        }

        private Dictionary<string, TimeSyncStatsSnapshot> BuildTimeSyncStatsByWorld(
            TimeSyncStatsSnapshot current,
            uint opCode,
            int intervalMs,
            double alpha,
            int timeoutMs)
        {
            var snapshots = new Dictionary<string, TimeSyncStatsSnapshot>();
            foreach (var kv in _runtime.GatewayRoom.WorldStartAnchors)
            {
                snapshots[kv.Key.Value] =
                    BuildTimeSyncStats(kv.Key, opCode, intervalMs, alpha, timeoutMs);
            }

            var worldIdValue = _plan.World.WorldId;
            if (worldIdValue != null)
            {
                snapshots[worldIdValue] = current;
            }
            return snapshots;
        }

        private TimeSyncStatsSnapshot BuildTimeSyncStats(WorldId worldId, uint opCode, int intervalMs, double alpha, int timeoutMs)
        {
            var hasAnchor = TryGetWorldStartAnchor(worldId, out var anchor);

            return new TimeSyncStatsSnapshot
            {
                OpCode = opCode,
                IntervalMs = intervalMs,
                Alpha = alpha,
                TimeoutMs = timeoutMs,

                HasAnchor = hasAnchor,
                AnchorStartServerTicks = anchor.StartServerTicks,
                AnchorServerTickFrequency = anchor.ServerTickFrequency,
                AnchorStartFrame = anchor.StartFrame,
                AnchorFixedDeltaSeconds = anchor.FixedDeltaSeconds,

                HasClockSync = _state.GatewayRoomTimeSync.HasClockSync,
                OffsetSecondsEwma = _state.GatewayRoomTimeSync.ClockOffsetSecondsEwma,
                RttSecondsEwma = _state.GatewayRoomTimeSync.RttSecondsEwma,
                Samples = _state.GatewayRoomTimeSync.Samples,

                IdealFrameRaw = ResolveIdealFrameRaw(worldId),
                IdealFrameSafetyMarginFrames = ResolveIdealFrameSafetyMarginFrames(worldId),
                IdealFrameLimit = ResolveIdealFrameLimit(worldId)
            };
        }
    }
}
