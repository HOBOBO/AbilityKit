namespace AbilityKit.Ability.Share.Battle.SearchTarget.Shapes
{
    public sealed class DataRectParamsResolver2D : IRectParamResolver2D
    {
        private readonly int _widthKey;
        private readonly int _lengthKey;

        public DataRectParamsResolver2D(int widthKey, int lengthKey)
        {
            _widthKey = widthKey;
            _lengthKey = lengthKey;
        }

        public bool RequiresPosition => false;

        public bool ResolveRectParams(SearchContext context, out float halfWidth, out float halfLength)
        {
            halfWidth = 0f;
            halfLength = 0f;
            if (context == null) return false;

            if (!context.TryGetData<float>(_widthKey, out var w)) return false;
            if (!context.TryGetData<float>(_lengthKey, out var l)) return false;

            if (w < 0f) w = -w;
            if (l < 0f) l = -l;

            halfWidth = w * 0.5f;
            halfLength = l * 0.5f;
            return halfWidth > 0f && halfLength > 0f;
        }
    }
}
