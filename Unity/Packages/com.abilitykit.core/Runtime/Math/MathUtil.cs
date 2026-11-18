namespace AbilityKit.Core.Mathematics
{
    /// <summary>
    /// 提供绝对容差比较、区间限制、插值及混合定点和浮点路径的平方根计算。
    /// </summary>
    public static class MathUtil
    {
        /// <summary>
        /// 获取默认绝对容差 1e-6；具体算法可能比较长度平方或缩放分量，不表示统一的相对误差。
        /// </summary>
        public const float Epsilon = 1e-6f;

        /*
         * 非负且小于该上限的输入使用定点辅助路径，其余输入保持系统平方根回退。
         */
        private const float DeterministicSqrtMax = 2.0e9f;

        /// <summary>
        /// 判断绝对值是否不大于容差，包含边界；负容差或 NaN 不命中，不校验参数。
        /// </summary>
        public static bool IsZero(float v, float epsilon = Epsilon) => Abs(v) <= epsilon;

        /// <summary>
        /// 按绝对差与容差比较，包含边界；不采用相对误差，相同无穷值之差为 NaN，因此不近似相等。
        /// </summary>
        public static bool Approximately(float a, float b, float epsilon = Epsilon) => Abs(a - b) <= epsilon;

        /// <summary>
        /// 正数返回 1，负数返回 -1；正负零及 NaN 返回 0。
        /// </summary>
        public static int Sign(float v)
        {
            if (v > 0f) return 1;
            if (v < 0f) return -1;
            return 0;
        }

        /// <summary>
        /// 依次比较下限、上限并返回边界或原值；不校验区间顺序，浮点 NaN 不会被修复。
        /// </summary>
        public static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        /// <summary>
        /// 依次比较下限、上限并返回边界或原值；不校验区间顺序，浮点 NaN 不会被修复。
        /// </summary>
        public static int Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        /// <summary>
        /// 将值限制到零至一的闭区间；无穷截到边界，NaN 原样通过。
        /// </summary>
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);

        /// <summary>
        /// 按 Clamp01 的规则将值限制到零至一；NaN 原样通过。
        /// </summary>
        public static float Saturate(float v) => Clamp01(v);

        /// <summary>
        /// 按符号比较返回绝对值；负零保留符号位，NaN 仍产生非数结果。
        /// </summary>
        public static float Abs(float v) => v >= 0f ? v : -v;

        /// <summary>
        /// 先限制因子到零至一，再计算 a + (b - a) * t；无端点快捷返回，溢出和非有限值仍参与运算。
        /// </summary>
        public static float Lerp(float a, float b, float t)
        {
            t = Clamp01(t);
            return a + (b - a) * t;
        }

        /// <summary>
        /// 对非负且小于 2e9 的输入使用 Q32.32 转换及整数开方，其他输入回退系统平方根。
        /// </summary>
        /// <remarks>
        /// Q32.32 转换截断低于 2^-32 的正输入；负数返回 NaN，正无穷返回正无穷。局部整数算法不能证明所有浮点计算已完成跨平台位级验收。
        /// </remarks>
        public static float Sqrt(float v)
        {
            if (v >= 0f && v < DeterministicSqrtMax)
            {
                return AbilityKit.Deterministic.DeterministicMath.Sqrt(
                    AbilityKit.Deterministic.Fixed64.FromSingle(v)).ToSingle();
            }

            return (float)System.Math.Sqrt(v);
        }

        /// <summary>
        /// 严格大于时返回左值，否则返回右值；相等或包含 NaN 导致比较失败时选择右操作数。
        /// </summary>
        public static float Max(float a, float b) => a > b ? a : b;
        /// <summary>
        /// 严格小于时返回左值，否则返回右值；相等或包含 NaN 导致比较失败时选择右操作数。
        /// </summary>
        public static float Min(float a, float b) => a < b ? a : b;
    }
}
