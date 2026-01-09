namespace AbilityKit.Ability.Share.Battle.SearchTarget.Shapes
{
    public interface IRectParamResolver2D
    {
        bool ResolveRectParams(SearchContext context, out float halfWidth, out float halfLength);

        bool RequiresPosition { get; }
    }
}
