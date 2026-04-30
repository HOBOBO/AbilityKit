namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 目标评分器接口
    /// </summary>
    public interface ITargetScorer
    {
        float Score(in SearchQuery query, SearchContext context, IEntityId candidate);
        bool RequiresPosition { get; }
    }
}
