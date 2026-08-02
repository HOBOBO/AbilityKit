using MemoryPack;

namespace AbilityKit.Protocol.Moba.Generated.GatewayFrameSync
{
    /// <summary>
    /// 观战订阅响应：WorldId + TickRate + CurrentFrame，供观战客户端初始化世界。
    /// </summary>
    [MemoryPackable]
    public readonly partial struct WireSpectatorSubscribeRes
    {
        [MemoryPackOrder(0)] public readonly ulong WorldId;
        [MemoryPackOrder(1)] public readonly int TickRate;
        [MemoryPackOrder(2)] public readonly int CurrentFrame;

        public WireSpectatorSubscribeRes(ulong worldId, int tickRate, int currentFrame)
        {
            WorldId = worldId;
            TickRate = tickRate;
            CurrentFrame = currentFrame;
        }
    }

    [MemoryPackable]
    public readonly partial struct WireFrameSyncMetrics
    {
        [MemoryPackOrder(0)] public readonly ulong RoomId;
        [MemoryPackOrder(1)] public readonly ulong WorldId;
        [MemoryPackOrder(2)] public readonly string? BattleId;
        [MemoryPackOrder(3)] public readonly int CurrentFrame;
        [MemoryPackOrder(4)] public readonly int TickRate;
        [MemoryPackOrder(5)] public readonly int ObserverCount;
        [MemoryPackOrder(6)] public readonly double AvgTickDeltaMs;
        [MemoryPackOrder(7)] public readonly double LastTickDeltaMs;
        [MemoryPackOrder(8)] public readonly double EffectiveHz;
        [MemoryPackOrder(9)] public readonly int TotalFramesReceived;
        [MemoryPackOrder(10)] public readonly int CatchUpHistoryFrames;
        [MemoryPackOrder(11)] public readonly int RecordingFrameCount;
        [MemoryPackOrder(12)] public readonly long UptimeSeconds;

        [MemoryPackConstructor]
        public WireFrameSyncMetrics(
            ulong roomId, ulong worldId, string? battleId,
            int currentFrame, int tickRate, int observerCount,
            double avgTickDeltaMs, double lastTickDeltaMs, double effectiveHz,
            int totalFramesReceived, int catchUpHistoryFrames,
            int recordingFrameCount, long uptimeSeconds)
        {
            RoomId = roomId;
            WorldId = worldId;
            BattleId = battleId;
            CurrentFrame = currentFrame;
            TickRate = tickRate;
            ObserverCount = observerCount;
            AvgTickDeltaMs = avgTickDeltaMs;
            LastTickDeltaMs = lastTickDeltaMs;
            EffectiveHz = effectiveHz;
            TotalFramesReceived = totalFramesReceived;
            CatchUpHistoryFrames = catchUpHistoryFrames;
            RecordingFrameCount = recordingFrameCount;
            UptimeSeconds = uptimeSeconds;
        }
    }
}
