namespace AbilityKit.Ability.Share.Battle.SearchTarget.Shapes
{
    public interface ISectorParamResolver2D
    {
        bool ResolveSectorParams(SearchContext context, out float radius, out float cosHalfAngle);

        bool RequiresPosition { get; }
    }
}
