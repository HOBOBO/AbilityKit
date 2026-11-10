using AbilityKit.Deterministic;

namespace AbilityKit.Ability.World.Services
{
    public sealed class DefaultWorldRandom : IWorldRandom
    {
        private readonly DeterministicRandom _random;

        public DefaultWorldRandom()
        {
            _random = new DeterministicRandom(0);
        }

        public DefaultWorldRandom(int seed)
        {
            _random = new DeterministicRandom(unchecked((ulong)(uint)seed));
        }

        public ulong Seed => _random.Seed;
        public ulong Sequence => _random.Sequence;

        public void SetSeed(ulong seed) => _random.SetSeed(seed);
        public ulong NextUInt64() => _random.NextUInt64();
        public int NextInt32(int minInclusive, int maxExclusive) => _random.NextInt32(minInclusive, maxExclusive);
        public void CaptureState(out ulong state0, out ulong state1, out ulong sequence) => _random.CaptureState(out state0, out state1, out sequence);
        public void RestoreState(ulong state0, ulong state1, ulong sequence) => _random.RestoreState(state0, state1, sequence);

        public int NextInt(int minInclusive, int maxExclusive)
        {
            return _random.NextInt32(minInclusive, maxExclusive);
        }

        public float NextFloat01()
        {
            return _random.NextFloat01();
        }
    }
}
