using System;

namespace AbilityKit.Deterministic
{

/// <summary>
/// Repeatable xoroshiro128+ random stream for deterministic simulation code.
/// </summary>
public sealed class DeterministicRandom
{
    private ulong _s0;
    private ulong _s1;

    public DeterministicRandom(ulong seed = 0x12345678UL)
    {
        SetSeed(seed);
    }

    public ulong Sequence { get; private set; }

    public void SetSeed(ulong seed)
    {
        var splitMix = new SplitMix64(seed);
        _s0 = splitMix.Next();
        _s1 = splitMix.Next();
        Sequence = 0;
    }

    public ulong NextUInt64()
    {
        Sequence++;
        return NextRaw();
    }

    public int NextInt32(int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive)
        {
            throw new ArgumentException("Minimum must be less than maximum.", nameof(minInclusive));
        }

        var range = (ulong)((long)maxExclusive - minInclusive);
        var threshold = (0UL - range) % range;
        ulong value;

        do
        {
            value = NextUInt64();
        }
        while (value < threshold);

        return checked((int)((long)(value % range) + minInclusive));
    }

    public Fixed64 NextFixed01()
    {
        var raw = (long)(NextUInt64() >> 32);
        return Fixed64.FromRaw(raw);
    }

    public Fixed64 NextFixed(Fixed64 minInclusive, Fixed64 maxExclusive)
    {
        if (minInclusive >= maxExclusive)
        {
            throw new ArgumentException("Minimum must be less than maximum.", nameof(minInclusive));
        }

        return minInclusive + ((maxExclusive - minInclusive) * NextFixed01());
    }

    private ulong NextRaw()
    {
        var result = _s0 + _s1;
        var s1 = _s0 ^ _s1;
        _s0 = RotateLeft(_s0, 55) ^ s1 ^ (s1 << 14);
        _s1 = RotateLeft(s1, 36);
        return result;
    }

    private static ulong RotateLeft(ulong value, int count)
    {
        return (value << count) | (value >> (64 - count));
    }

    private sealed class SplitMix64
    {
        private ulong _value;

        public SplitMix64(ulong seed)
        {
            _value = seed;
        }

        public ulong Next()
        {
            var z = _value += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
        }
    }
}
