namespace AbilityKit.Ability.Share.Battle.SearchTarget.Shapes
{
    public sealed class DataCircleParamsResolver2D : ICircleParamResolver2D
    {
        private readonly int _radiusKey;

        public DataCircleParamsResolver2D(int radiusKey)
        {
            _radiusKey = radiusKey;
        }

        public bool RequiresPosition => false;

        public bool ResolveCircleParams(SearchContext context, out float radius)
        {
            radius = 0f;
            if (context == null) return false;
            if (!context.TryGetData<float>(_radiusKey, out radius)) return false;
            if (radius < 0f) radius = -radius;
            return radius > 0f;
        }
    }
}
