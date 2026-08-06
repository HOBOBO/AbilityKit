namespace AbilityKit.Battle.SearchTarget.Scorers
{
    /// <summary>
    /// 固定分数评分器
    /// </summary>
    [TargetScorer(0x2001, "Zero")]
    public sealed class ZeroScorer : ITargetScorer
    {
        public float Score(in SearchQuery query, SearchContext context, EntityId candidate)
        {
            return 0f;
        }
    }

    /// <summary>
    /// 基于哈希的确定性随机评分器
    /// </summary>
    [TargetScorer(0x2002, "SeededHashRandom")]
    public sealed class SeededHashRandomScorer : ITargetScorer
    {
        private readonly SearchContextKey<int> _seedKey;
        private readonly int _seed;
        private readonly bool _usesContextSeed;

        public SeededHashRandomScorer(int seed)
        {
            _seed = seed;
        }

        public SeededHashRandomScorer(SearchContextKey<int> seedKey)
        {
            _seedKey = seedKey ?? throw new System.ArgumentNullException(nameof(seedKey));
            _usesContextSeed = true;
        }

        public float Score(in SearchQuery query, SearchContext context, EntityId candidate)
        {
            var seed = _seed;
            if (_usesContextSeed && (context == null || !context.TryGetData(_seedKey, out seed)))
            {
                seed = 0;
            }

            unchecked
            {
                var value = candidate.Value;
                uint x = (uint)(seed * 0x9E3779B9) ^ (uint)value ^ (uint)(value >> 32);
                x ^= x >> 16;
                x *= 0x7FEB352D;
                x ^= x >> 15;
                x *= 0x846CA68B;
                x ^= x >> 16;

                return (x & 0x00FFFFFFu) / 16777216f;
            }
        }
    }

    /// <summary>
    /// 基于距离的评分器（距离越近分数越高）
    /// </summary>
    [TargetScorer(0x2004, "DistanceToEntity")]
    public sealed class DistanceToEntityScorer : ITargetScorer
    {
        private readonly EntityId _source;

        public DistanceToEntityScorer(EntityId source)
        {
            _source = source;
        }

        public float Score(in SearchQuery query, SearchContext context, EntityId candidate)
        {
            var pos = context.PositionProvider;
            if (pos == null) return float.NegativeInfinity;
            if (!pos.TryGetPosition(_source, out var src)) return float.NegativeInfinity;
            if (!pos.TryGetPosition(candidate, out var p)) return float.NegativeInfinity;

            var dx = p.X - src.X;
            var dy = p.Y - src.Y;
            return -(dx * dx + dy * dy);
        }
    }
}
