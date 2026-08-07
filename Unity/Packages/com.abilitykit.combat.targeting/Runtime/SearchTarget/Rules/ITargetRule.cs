namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 目标规则接口
    /// </summary>
    public interface ITargetRule
    {
        bool IsMatch(in SearchQuery query, SearchContext context, EntityId candidate);
    }
}
