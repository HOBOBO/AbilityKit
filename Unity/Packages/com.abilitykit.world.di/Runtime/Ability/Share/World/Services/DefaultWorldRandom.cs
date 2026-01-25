using System;

namespace AbilityKit.Ability.World.Services
{
    public sealed class DefaultWorldRandom : IWorldRandom
    {
        private readonly Random _random;

        public DefaultWorldRandom()
        {
            _random = new Random();
        }

        public DefaultWorldRandom(int seed)
        {
            _random = new Random(seed);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            return _random.Next(minInclusive, maxExclusive);
        }

        public float NextFloat01()
        {
            return (float)_random.NextDouble();
        }
    }
}
