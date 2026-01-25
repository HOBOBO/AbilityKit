namespace AbilityKit.Ability.Share.Math
{
    public static class MathUtil
    {
        public const float Epsilon = 1e-6f;

        public static bool IsZero(float v, float epsilon = Epsilon) => Abs(v) <= epsilon;

        public static bool Approximately(float a, float b, float epsilon = Epsilon) => Abs(a - b) <= epsilon;

        public static int Sign(float v)
        {
            if (v > 0f) return 1;
            if (v < 0f) return -1;
            return 0;
        }

        public static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        public static int Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        public static float Clamp01(float v) => Clamp(v, 0f, 1f);

        public static float Saturate(float v) => Clamp01(v);

        public static float Abs(float v) => v >= 0f ? v : -v;

        public static float Lerp(float a, float b, float t)
        {
            t = Clamp01(t);
            return a + (b - a) * t;
        }

        public static float Sqrt(float v) => (float)System.Math.Sqrt(v);

        public static float Max(float a, float b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
    }
}
