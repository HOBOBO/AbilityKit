using AbilityKit.Deterministic;

namespace AbilityKit.Triggering.Runtime.Random
{
    /// <summary>
    /// Compatibility adapter for the triggering API. The generator implementation is shared with
    /// world services and behavior trees through the deterministic package.
    /// </summary>
    public sealed class DeterministicRandom : IRandomProvider
    {
        private readonly AbilityKit.Deterministic.DeterministicRandom _stream;

        public DeterministicRandom(uint seed = 0x12345678)
        {
            _stream = new AbilityKit.Deterministic.DeterministicRandom(seed);
        }

        public ulong Seed => _stream.Seed;
        public ulong Sequence => _stream.Sequence;
        public uint CurrentSequence => unchecked((uint)_stream.Sequence);

        /// <summary>Compatibility overload for callers using the original uint seed API.</summary>
        public void SetSeed(uint seed) => _stream.SetSeed(seed);
        public void SetSeed(ulong seed) => _stream.SetSeed(seed);
        public ulong NextUInt64() => _stream.NextUInt64();
        public int NextInt32(int minInclusive, int maxExclusive) => _stream.NextInt32(minInclusive, maxExclusive);
        public float NextFloat01() => _stream.NextFloat01();
        public void CaptureState(out ulong state0, out ulong state1, out ulong sequence) => _stream.CaptureState(out state0, out state1, out sequence);
        public void RestoreState(ulong state0, ulong state1, ulong sequence) => _stream.RestoreState(state0, state1, sequence);

        public int Next(int min, int max) => NextInt32(min, max);
        public float NextFloat() => NextFloat01();
    }
}
