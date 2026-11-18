using System;

namespace AbilityKit.Core.Mathematics
{
    /// <summary>
    /// 保存二维单精度浮点分量，默认值为零向量；不校验分量有限性。
    /// </summary>
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        /// <summary>
        /// 保存 X 单精度分量，不校验取值范围。
        /// </summary>
        public readonly float X;
        /// <summary>
        /// 保存 Y 单精度分量，不校验取值范围。
        /// </summary>
        public readonly float Y;

        /// <summary>
        /// 直接保存两个分量，不归一化或校验有限性。
        /// </summary>
        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// 获取两个分量均为零的向量。
        /// </summary>
        public static Vec2 Zero => new Vec2(0f, 0f);
        /// <summary>
        /// 获取两个分量均为一的向量。
        /// </summary>
        public static Vec2 One => new Vec2(1f, 1f);

        /// <summary>
        /// 返回各分量平方的浮点和，不开方；极大或非有限分量可能溢出或产生 NaN。
        /// </summary>
        public float SqrMagnitude => X * X + Y * Y;
        /// <summary>
        /// 使用 MathUtil.Sqrt 计算平方长度的平方根，继承其定点量化及系统回退边界。
        /// </summary>
        public float Magnitude => MathUtil.Sqrt(SqrMagnitude);

        /// <summary>
        /// 长度大于零时逐分量除以长度，否则返回零向量；长度通过 MathUtil.Sqrt 计算。
        /// </summary>
        /// <remarks>
        /// 长度为零或 NaN 时返回零向量；极小平方长度可能量化为零，极大平方和可能溢出，不构成有限性验证。
        /// </remarks>
        public Vec2 Normalized
        {
            get
            {
                var mag = Magnitude;
                return mag > 0f ? this / mag : Zero;
            }
        }

        /// <summary>
        /// 计算对应分量乘积之和，不归一化输入或限制结果范围。
        /// </summary>
        public static float Dot(in Vec2 a, in Vec2 b) => a.X * b.X + a.Y * b.Y;

        /// <summary>
        /// 逐分量调用 System.MathF.Min，不使用 MathUtil 的二元比较选择规则。
        /// </summary>
        public static Vec2 Min(in Vec2 a, in Vec2 b) => new Vec2(System.MathF.Min(a.X, b.X), System.MathF.Min(a.Y, b.Y));
        /// <summary>
        /// 逐分量调用 System.MathF.Max，不使用 MathUtil 的二元比较选择规则。
        /// </summary>
        public static Vec2 Max(in Vec2 a, in Vec2 b) => new Vec2(System.MathF.Max(a.X, b.X), System.MathF.Max(a.Y, b.Y));

        /// <summary>
        /// 逐分量相加，不检查浮点溢出或非有限值。
        /// </summary>
        public static Vec2 operator +(in Vec2 a, in Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        /// <summary>
        /// 逐分量相减或取反，不检查浮点溢出或非有限值。
        /// </summary>
        public static Vec2 operator -(in Vec2 a, in Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        /// <summary>
        /// 逐分量相减或取反，不检查浮点溢出或非有限值。
        /// </summary>
        public static Vec2 operator -(in Vec2 v) => new Vec2(-v.X, -v.Y);
        /// <summary>
        /// 按标量逐分量缩放，不归一化或限制结果范围。
        /// </summary>
        public static Vec2 operator *(in Vec2 v, float s) => new Vec2(v.X * s, v.Y * s);
        /// <summary>
        /// 按标量逐分量缩放，不归一化或限制结果范围。
        /// </summary>
        public static Vec2 operator *(float s, in Vec2 v) => new Vec2(v.X * s, v.Y * s);
        /// <summary>
        /// 逐分量执行浮点除法，零除数产生无穷或 NaN，不抛整数除零异常。
        /// </summary>
        public static Vec2 operator /(in Vec2 v, float s) => new Vec2(v.X / s, v.Y / s);

        /// <summary>
        /// 逐分量使用 float.Equals 比较，不采用容差；NaN 分量可以相等，正负零相等。
        /// </summary>
        public bool Equals(Vec2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        /// <summary>
        /// 逐分量使用 float.Equals 比较，不采用容差；NaN 分量可以相等，正负零相等。
        /// </summary>
        public override bool Equals(object? obj) => obj is Vec2 other && Equals(other);
        /// <summary>
        /// 组合分量的进程内哈希，符合值相等规则；不得用于持久化或跨进程稳定标识。
        /// </summary>
        public override int GetHashCode() => HashCode.Combine(X, Y);
        /// <summary>
        /// 按当前区域设置输出诊断文本，不作为协议或稳定序列化格式。
        /// </summary>
        public override string ToString() => $"({X}, {Y})";
    }
}
