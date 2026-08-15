namespace AbilityKit.Core.Mathematics
{
    public static class MathUtil
    {
        public const float Epsilon = 1e-6f;

        // 定点开方的确定域上限：被开方值达到该量级后回退硬件 sqrt（保持旧行为）。
        // 战斗模拟域内的坐标/距离平方（向量长度上限约 44721）远小于此值。
        private const float DeterministicSqrtMax = 2.0e9f;

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

        /// <summary>
        /// 确定性开方：确定域（[0, 2e9)）内走定点整数算法，跨平台（.NET/Mono/IL2CPP）位一致，
        /// 保证模拟数学（Vec2/Vec3.Magnitude、Normalized、Quat 等）不产生跨端漂移。
        /// 负数返回 NaN、超大/非有限值回退硬件 sqrt，均保持历史行为。
        /// </summary>
        public static float Sqrt(float v)
        {
            if (v >= 0f && v < DeterministicSqrtMax)
            {
                return AbilityKit.Deterministic.DeterministicMath.Sqrt(
                    AbilityKit.Deterministic.Fixed64.FromSingle(v)).ToSingle();
            }

            return (float)System.Math.Sqrt(v);
        }

        public static float Max(float a, float b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
    }
}
