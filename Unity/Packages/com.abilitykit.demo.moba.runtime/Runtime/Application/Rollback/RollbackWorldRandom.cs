using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.Services;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Triggering.Runtime;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>
    /// The authoritative simulation random stream for a MOBA world.
    ///
    /// All gameplay randomness must come through this service. Its seed, generator state and draw
    /// sequence are rollback state, so restoring a frame restores the exact next random value.
    /// </summary>
    [MobaRollbackProvider(DefaultKey, autoResolve: true)]
    public sealed class RollbackWorldRandom : IWorldRandom, ITriggerRandomSource, IRollbackStateProvider, IRollbackStatePreflightProvider, IMobaStateRecoveryProvider
    {
        public const int DefaultKey = 10010;
        public const int CurrentPayloadVersion = 2;

        private int _seed;
        private readonly DeterministicRandom _stream;

        public int Key => DefaultKey;

        public string Name => "Random";

        public int BattleSeed => _seed;
        public ulong Seed => unchecked((ulong)(uint)_seed);
        public ulong Sequence => _stream.Sequence;

        /// <summary>Number of values consumed from this stream.</summary>
        public ulong DrawCount => _stream.Sequence;

        public RollbackWorldRandom()
        {
            _stream = new DeterministicRandom();
            SetSeed(0);
        }

        public void SetSeed(int seed)
        {
            _seed = seed;
            _stream.SetSeed(unchecked((ulong)(uint)seed));
        }

        void IDeterministicRandomStream.SetSeed(ulong seed) => SetSeed(unchecked((int)(uint)seed));
        public ulong NextUInt64() => _stream.NextUInt64();
        public int NextInt32(int minInclusive, int maxExclusive) => NextInt(minInclusive, maxExclusive);
        public void CaptureState(out ulong state0, out ulong state1, out ulong sequence) => _stream.CaptureState(out state0, out state1, out sequence);
        public void RestoreState(ulong state0, ulong state1, ulong sequence) => _stream.RestoreState(state0, state1, sequence);

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxExclusive),
                    maxExclusive,
                    "The exclusive upper bound must be greater than the inclusive lower bound.");
            }

            return _stream.NextInt32(minInclusive, maxExclusive);
        }

        public float NextFloat01()
        {
            return _stream.NextFloat01();
        }

        public byte[] ExportState(FrameIndex frame)
        {
            return Export(frame);
        }

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            Import(frame, payload);
        }

        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            hash.AddInt(Key);
            hash.AddInt(_seed);
            _stream.CaptureState(out var state0, out var state1, out var sequence);
            hash.AddLong(unchecked((long)state0));
            hash.AddLong(unchecked((long)state1));
            hash.AddLong(unchecked((long)sequence));
        }

        public byte[] Export(FrameIndex frame)
        {
            _stream.CaptureState(out var state0, out var state1, out var sequence);
            return MemoryPackSerializer.Serialize(new MobaRollbackWorldRandomPayload(
                CurrentPayloadVersion,
                _seed,
                state0,
                state1,
                sequence));
        }

        public void ValidateImport(FrameIndex frame, byte[] payload)
        {
            _ = DeserializeAndValidate(payload);
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            var p = DeserializeAndValidate(payload);
            _seed = p.Seed;
            _stream.RestoreState(p.State0, p.State1, p.Sequence);
        }

        private static MobaRollbackWorldRandomPayload DeserializeAndValidate(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                throw new InvalidOperationException("World random rollback payload is empty.");

            var p = MemoryPackSerializer.Deserialize<MobaRollbackWorldRandomPayload>(payload);
            if (p.Version != CurrentPayloadVersion)
            {
                throw new NotSupportedException(
                    $"Unsupported world random rollback payload version: {p.Version}. " +
                    $"Expected {CurrentPayloadVersion}.");
            }

            if ((p.State0 | p.State1) == 0)
                throw new InvalidOperationException("World random rollback payload contains an invalid zero state.");

            return p;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaRollbackWorldRandomPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly int Seed;
        [MemoryPackOrder(2)] public readonly ulong State0;
        [MemoryPackOrder(3)] public readonly ulong State1;
        [MemoryPackOrder(4)] public readonly ulong Sequence;

        public MobaRollbackWorldRandomPayload(
            int version,
            int seed,
            ulong state0,
            ulong state1,
            ulong sequence)
        {
            Version = version;
            Seed = seed;
            State0 = state0;
            State1 = state1;
            Sequence = sequence;
        }
    }
}
