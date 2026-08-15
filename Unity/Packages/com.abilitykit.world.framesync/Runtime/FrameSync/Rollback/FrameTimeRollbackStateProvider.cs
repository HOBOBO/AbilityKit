using System;
using MemoryPack;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    public sealed class FrameTimeRollbackStateProvider : IRollbackStateProvider
    {
        public const int DefaultKey = 900002;

        // v2 (2026-08-15): FrameTime 累计时刻定点化（Q32.32），快照以 raw long 存储 Time/FixedDelta。
        private const int PayloadVersion = 2;
        private readonly FrameTime _frameTime;

        public FrameTimeRollbackStateProvider(FrameTime frameTime, int key = DefaultKey)
        {
            _frameTime = frameTime ?? throw new ArgumentNullException(nameof(frameTime));
            Key = key;
        }

        public int Key { get; }

        public byte[] Export(FrameIndex frame)
        {
            return MemoryPackSerializer.Serialize(new FrameTimeRollbackStatePayload(
                PayloadVersion,
                _frameTime.Frame.Value,
                _frameTime.TimeRaw,
                _frameTime.DeltaTime,
                _frameTime.FixedDeltaRaw));
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Frame-time rollback payload is empty. frame={frame.Value}");
            }

            var state = MemoryPackSerializer.Deserialize<FrameTimeRollbackStatePayload>(payload);
            if (state.Version != PayloadVersion)
            {
                throw new InvalidOperationException(
                    $"Unsupported frame-time rollback payload version: {state.Version}");
            }

            _frameTime.RestoreRaw(
                new FrameIndex(state.Frame),
                state.TimeRaw,
                state.DeltaTime,
                state.FixedDeltaRaw);
        }
    }

    [MemoryPackable]
    public readonly partial struct FrameTimeRollbackStatePayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly int Frame;
        [MemoryPackOrder(2)] public readonly long TimeRaw;
        [MemoryPackOrder(3)] public readonly float DeltaTime;
        [MemoryPackOrder(4)] public readonly long FixedDeltaRaw;

        public FrameTimeRollbackStatePayload(int version, int frame, long timeRaw, float deltaTime, long fixedDeltaRaw)
        {
            Version = version;
            Frame = frame;
            TimeRaw = timeRaw;
            DeltaTime = deltaTime;
            FixedDeltaRaw = fixedDeltaRaw;
        }
    }
}
