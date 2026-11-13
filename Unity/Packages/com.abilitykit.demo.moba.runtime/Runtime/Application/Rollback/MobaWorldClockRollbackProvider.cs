using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.Services;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>Local rollback ownership of the independently accumulated world clock.</summary>
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaWorldClockRollbackProvider : IRollbackStateProvider, IRollbackStatePreflightProvider
    {
        public const int DefaultKey = 10046;
        private readonly WorldClock _clock;

        public MobaWorldClockRollbackProvider(WorldClock clock)
            => _clock = clock ?? throw new ArgumentNullException(nameof(clock));

        public int Key => DefaultKey;
        public byte[] Export(FrameIndex frame)
        {
            var state = new MobaWorldClockRollbackPayload(1, frame.Value, _clock.Time, _clock.DeltaTime);
            Validate(frame, state);
            return MemoryPackSerializer.Serialize(state);
        }

        public void ValidateImport(FrameIndex frame, byte[] payload) => Read(frame, payload);
        public void Import(FrameIndex frame, byte[] payload)
        {
            var state = Read(frame, payload);
            _clock.RestoreState(state.Time, state.DeltaTime);
        }

        private static MobaWorldClockRollbackPayload Read(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                throw new InvalidOperationException("Missing world clock rollback payload.");
            var state = MemoryPackSerializer.Deserialize<MobaWorldClockRollbackPayload>(payload);
            Validate(frame, state);
            // Reject trailing bytes and noncanonical encodings rather than hiding corrupted state.
            if (!payload.AsSpan().SequenceEqual(MemoryPackSerializer.Serialize(state)))
                throw new InvalidOperationException("Invalid world clock rollback encoding.");
            return state;
        }

        private static void Validate(FrameIndex frame, MobaWorldClockRollbackPayload state)
        {
            if (state.Version != 1 || state.Frame != frame.Value || state.Frame < 0 ||
                !IsValidTime(state.Time) || !IsValidTime(state.DeltaTime))
                throw new InvalidOperationException("Invalid world clock rollback state.");
        }

        internal static bool IsValidTime(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }

    [MemoryPackable]
    public readonly partial struct MobaWorldClockRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly int Frame;
        [MemoryPackOrder(2)] public readonly float Time;
        [MemoryPackOrder(3)] public readonly float DeltaTime;

        public MobaWorldClockRollbackPayload(int version, int frame, float time, float deltaTime)
        { Version = version; Frame = frame; Time = time; DeltaTime = deltaTime; }
    }
}
