namespace AbilityKit.Battle.SearchTarget.Scorers
{
    /// <summary>
    /// 固定分数评分器
    /// </summary>
    [TargetScorer(0x2001, "Zero")]
    public sealed class ZeroScorer : ITargetScorer
    {
        public bool RequiresPosition => false;

        public float Score(in SearchQuery query, SearchContext context, IEntityId candidate)
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
        private readonly int _seedKey;

        public SeededHashRandomScorer(int seedKey)
        {
            _seedKey = seedKey;
        }

        public bool RequiresPosition => false;

        public float Score(in SearchQuery query, SearchContext context, IEntityId candidate)
        {
            if (context == null) return 0f;
            if (!context.TryGetData<int>(_seedKey, out var seed)) seed = 0;

            unchecked
            {
                uint x = (uint)(seed * 0x9E3779B9) ^ (uint)candidate.ActorId;
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
        private readonly IEntityId _source;

        public DistanceToEntityScorer(IEntityId source)
        {
            _source = source;
        }

        public bool RequiresPosition => true;

        public float Score(in SearchQuery query, SearchContext context, IEntityId candidate)
        {
            if (!context.TryGetService<IPositionProvider>(out var pos) || pos == null) return float.NegativeInfinity;
            if (!pos.TryGetPosition(_source, out var src)) return float.NegativeInfinity;
            if (!pos.TryGetPosition(candidate, out var p)) return float.NegativeInfinity;

            var dx = p.X - src.X;
            var dy = p.Y - src.Y;
            return -(dx * dx + dy * dy);
        }
    }
}
