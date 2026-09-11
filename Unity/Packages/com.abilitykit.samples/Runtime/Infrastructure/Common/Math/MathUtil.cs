#nullable enable

using System;

namespace AbilityKit.Samples.Logic.Infrastructure.Common.Math
{
    /// <summary>
    /// 数学工具类
    /// </summary>
    public static class MathUtil
    {
        /// <summary>
        /// 计算伤害（带方差）
        /// </summary>
        public static float CalculateDamage(float attack, float defense, float bonus = 0, float variance = 0.1f)
        {
            var baseDamage = attack + bonus - defense * 0.5f;
            var randomFactor = 1.0f - variance + (float)new Random().NextDouble() * variance * 2;
            return System.Math.Max(1, baseDamage * (float)randomFactor);
        }

        /// <summary>
        /// 线性插值
        /// </summary>
        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * System.Math.Clamp(t, 0, 1);
        }

        /// <summary>
        /// 计算距离
        /// </summary>
        public static float Distance(float x1, float x2)
        {
            return System.Math.Abs(x2 - x1);
        }

        /// <summary>
        /// 计算百分比变化
        /// </summary>
        public static float PercentChange(float original, float current)
        {
            if (original == 0) return 0;
            return (current - original) / original * 100;
        }
    }
}
