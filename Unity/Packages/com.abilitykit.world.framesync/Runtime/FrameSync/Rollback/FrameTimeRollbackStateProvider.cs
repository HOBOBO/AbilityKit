using System;
using AbilityKit.Core.Serialization;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    public sealed class FrameTimeRollbackStateProvider : IRollbackStateProvider
    {
        public const int DefaultKey = 900002;

        private const int PayloadVersion = 1;
        private readonly FrameTime _frameTime;

        public FrameTimeRollbackStateProvider(FrameTime frameTime, int key = DefaultKey)
        {
            _frameTime = frameTime ?? throw new ArgumentNullException(nameof(frameTime));
            Key = key;
        }

        public int Key { get; }

        public byte[] Export(FrameIndex frame)
        {
            return BinaryObjectCodec.Encode(new Payload(
                PayloadVersion,
                _frameTime.Frame.Value,
                _frameTime.Time,
                _frameTime.DeltaTime,
                _frameTime.FrameToTime(new FrameIndex(1))));
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Frame-time rollback payload is empty. frame={frame.Value}");
            }

            var state = BinaryObjectCodec.Decode<Payload>(payload);
            if (state.Version != PayloadVersion)
            {
                throw new InvalidOperationException(
                    $"Unsupported frame-time rollback payload version: {state.Version}");
            }

            _frameTime.Restore(
                new FrameIndex(state.Frame),
                state.Time,
                state.DeltaTime,
                state.FixedDelta);
        }

        public readonly struct Payload
        {
            [BinaryMember(0)] public readonly int Version;
            [BinaryMember(1)] public readonly int Frame;
            [BinaryMember(2)] public readonly float Time;
            [BinaryMember(3)] public readonly float DeltaTime;
            [BinaryMember(4)] public readonly float FixedDelta;

            public Payload(int version, int frame, float time, float deltaTime, float fixedDelta)
            {
                Version = version;
                Frame = frame;
                Time = time;
                DeltaTime = deltaTime;
                FixedDelta = fixedDelta;
            }
        }
    }
}
