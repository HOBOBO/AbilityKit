namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 目标规则接口
    /// </summary>
    public interface ITargetRule
    {
        bool Test(in SearchQuery query, SearchContext context, IEntityId candidate);
        bool RequiresPosition { get; }
    }
}
