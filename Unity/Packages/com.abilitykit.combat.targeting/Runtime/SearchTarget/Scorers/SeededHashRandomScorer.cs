using AbilityKit.Ability.Share.ECS;
using AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Battle.SearchTarget.Scorers
{
    public sealed class SeededHashRandomScorer : ITargetScorer
    {
        private readonly int _seedKey;

        public SeededHashRandomScorer(int seedKey)
        {
            _seedKey = seedKey;
        }

        public bool RequiresPosition => false;

        public float Score(in SearchQuery query, SearchContext context, EcsEntityId candidate)
        {
            if (context == null) return 0f;
            if (!context.TryGetData<int>(_seedKey, out var seed)) seed = 0;

            // Deterministic pseudo-random based on (seed, actorId).
            // Returns [0,1).
            unchecked
            {
                uint x = (uint)(seed * 0x9E3779B9) ^ (uint)candidate.ActorId;
                x ^= x >> 16;
                x *= 0x7FEB352D;
                x ^= x >> 15;
                x *= 0x846CA68B;
                x ^= x >> 16;

                // 24-bit mantissa for stable float.
                return (x & 0x00FFFFFFu) / 16777216f;
            }
        }
    }
}
