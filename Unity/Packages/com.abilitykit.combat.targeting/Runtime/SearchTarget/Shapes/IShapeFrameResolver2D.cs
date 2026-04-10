namespace AbilityKit.Battle.SearchTarget.Shapes
{
    public interface IShapeFrameResolver2D
    {
        bool ResolveFrame(SearchContext context, out ShapeFrame2D frame);

        bool RequiresPosition { get; }
    }
}
