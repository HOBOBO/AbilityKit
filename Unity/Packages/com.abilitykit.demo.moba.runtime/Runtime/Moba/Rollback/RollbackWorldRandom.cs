using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Core.Generic;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Demo.Moba.Rollback
{
    public sealed class RollbackWorldRandom : IWorldRandom, IRollbackStateProvider
    {
        public const int DefaultKey = 10010;

        private int _seed;
        private uint _state;

        public int Key => DefaultKey;

        public int Seed => _seed;

        public RollbackWorldRandom()
        {
            SetSeed(0);
        }

        public void SetSeed(int seed)
        {
            _seed = seed;

            // SplitMix32-like scrambling to produce a non-zero internal state.
            unchecked
            {
                uint x = (uint)seed;
                x += 0x9E3779B9u;
                x ^= x >> 16;
                x *= 0x85EBCA6Bu;
                x ^= x >> 13;
                x *= 0xC2B2AE35u;
                x ^= x >> 16;
                _state = x != 0 ? x : 0x6D2B79F5u;
            }
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;

            var range = (uint)(maxExclusive - minInclusive);
            var v = NextUInt();
            var r = (int)(v % range);
            return minInclusive + r;
        }

        public float NextFloat01()
        {
            // Use 24 bits to construct a float in [0, 1).
            var v = NextUInt();
            var mantissa = v & 0x00FFFFFFu;
            return mantissa / 16777216f;
        }

        private uint NextUInt()
        {
            // xorshift32
            unchecked
            {
                var x = _state;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _state = x;
                return x;
            }
        }

        public byte[] Export(FrameIndex frame)
        {
            return BinaryObjectCodec.Encode(new Payload(1, _seed, _state));
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                SetSeed(_seed);
                return;
            }

            var p = BinaryObjectCodec.Decode<Payload>(payload);
            _seed = p.Seed;
            _state = p.State != 0 ? p.State : 0x6D2B79F5u;
        }

        public readonly struct Payload
        {
            [BinaryMember(0)] public readonly int Version;
            [BinaryMember(1)] public readonly int Seed;
            [BinaryMember(2)] public readonly uint State;

            public Payload(int version, int seed, uint state)
            {
                Version = version;
                Seed = seed;
                State = state;
            }
        }
    }
}
