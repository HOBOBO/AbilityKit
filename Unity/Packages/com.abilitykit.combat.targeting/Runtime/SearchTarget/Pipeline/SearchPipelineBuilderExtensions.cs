namespace AbilityKit.Battle.SearchTarget
{
    public static class SearchPipelineBuilderExtensions
    {
        public static SearchPipelineBuilder FilterCircle(this SearchPipelineBuilder builder, IVec2 origin, float radius)
        {
            return builder.Filter(new Rules.CircleShapeRule(origin, radius));
        }

        public static SearchPipelineBuilder FilterSector(this SearchPipelineBuilder builder, IVec2 origin, IVec2 forward, float radius, float halfAngleDegrees)
        {
            return builder.Filter(new Rules.SectorShapeRule(origin, forward, radius, halfAngleDegrees));
        }

        public static SearchPipelineBuilder FilterWhitelist(this SearchPipelineBuilder builder, IActorIdSet set)
        {
            return builder.Filter(new Rules.WhitelistRule(set));
        }

        public static SearchPipelineBuilder FilterBlacklist(this SearchPipelineBuilder builder, IActorIdSet set)
        {
            return builder.Filter(new Rules.BlacklistRule(set));
        }

        public static SearchPipelineBuilder Exclude(this SearchPipelineBuilder builder, IEntityId entity)
        {
            return builder.Filter(new Rules.ExcludeEntityRule(entity));
        }

        public static SearchPipelineBuilder ScoreByDistance(this SearchPipelineBuilder builder, IEntityId source)
        {
            return builder.ScoreBy(new Scorers.DistanceToEntityScorer(source));
        }

        public static SearchPipelineBuilder ScoreByRandom(this SearchPipelineBuilder builder, int seedKey = 0)
        {
            return builder.ScoreBy(new Scorers.SeededHashRandomScorer(seedKey));
        }

        public static SearchPipelineBuilder TopK(this SearchPipelineBuilder builder, int k)
        {
            return builder.Take(k).Select(new Selectors.TopKByScoreSelector());
        }
    }
}
