namespace AbilityKit.Ability.Share.Battle.SearchTarget.Shapes
{
    public interface ICircleParamResolver2D
    {
        bool ResolveCircleParams(SearchContext context, out float radius);

        bool RequiresPosition { get; }
    }
}
