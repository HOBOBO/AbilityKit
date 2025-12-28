namespace AbilityKit.Ability.Share.Math
{
    public static class MathUtil
    {
        public const float Epsilon = 1e-6f;

        public static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        public static float Clamp01(float v) => Clamp(v, 0f, 1f);

        public static float Abs(float v) => v >= 0f ? v : -v;

        public static float Sqrt(float v) => (float)System.Math.Sqrt(v);

        public static float Max(float a, float b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
    }
}
