using System;

namespace AbilityKit.Core.Mathematics
{
    /// <summary>
    /// 保存三维单精度浮点分量，正 X、正 Y、正 Z 分别为右、上、前方向。
    /// </summary>
    public readonly struct Vec3 : IEquatable<Vec3>
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
        /// 保存 Z 单精度分量，不校验取值范围。
        /// </summary>
        public readonly float Z;

        /// <summary>
        /// 直接保存三个分量，不归一化或校验有限性。
        /// </summary>
        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// 获取三个分量均为零的向量。
        /// </summary>
        public static Vec3 Zero => new Vec3(0f, 0f, 0f);
        /// <summary>
        /// 获取三个分量均为一的向量。
        /// </summary>
        public static Vec3 One => new Vec3(1f, 1f, 1f);
        /// <summary>
        /// 获取正 Y 轴单位向量。
        /// </summary>
        public static Vec3 Up => new Vec3(0f, 1f, 0f);
        /// <summary>
        /// 获取负 Y 轴单位向量。
        /// </summary>
        public static Vec3 Down => new Vec3(0f, -1f, 0f);
        /// <summary>
        /// 获取正 X 轴单位向量。
        /// </summary>
        public static Vec3 Right => new Vec3(1f, 0f, 0f);
        /// <summary>
        /// 获取负 X 轴单位向量。
        /// </summary>
        public static Vec3 Left => new Vec3(-1f, 0f, 0f);
        /// <summary>
        /// 获取正 Z 轴单位向量。
        /// </summary>
        public static Vec3 Forward => new Vec3(0f, 0f, 1f);
        /// <summary>
        /// 获取负 Z 轴单位向量。
        /// </summary>
        public static Vec3 Back => new Vec3(0f, 0f, -1f);

        /// <summary>
        /// 返回各分量平方的浮点和，不开方；极大或非有限分量可能溢出或产生 NaN。
        /// </summary>
        public float SqrMagnitude => X * X + Y * Y + Z * Z;
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
        public Vec3 Normalized
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
        public static float Dot(in Vec3 a, in Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        /// <summary>
        /// 计算 a 叉乘 b；正 X 轴叉乘正 Y 轴为正 Z 轴，交换操作数使方向反转。
        /// </summary>
        public static Vec3 Cross(in Vec3 a, in Vec3 b)
        {
            return new Vec3(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X);
        }

        /// <summary>
        /// 逐分量调用 System.MathF.Min，不使用 MathUtil 的二元比较选择规则。
        /// </summary>
        public static Vec3 Min(in Vec3 a, in Vec3 b)
        {
            return new Vec3(System.MathF.Min(a.X, b.X), System.MathF.Min(a.Y, b.Y), System.MathF.Min(a.Z, b.Z));
        }

        /// <summary>
        /// 逐分量调用 System.MathF.Max，不使用 MathUtil 的二元比较选择规则。
        /// </summary>
        public static Vec3 Max(in Vec3 a, in Vec3 b)
        {
            return new Vec3(System.MathF.Max(a.X, b.X), System.MathF.Max(a.Y, b.Y), System.MathF.Max(a.Z, b.Z));
        }

        /// <summary>
        /// 计算差向量的长度，继承 MathUtil.Sqrt 的量化与回退边界。
        /// </summary>
        public static float Distance(in Vec3 a, in Vec3 b) => (a - b).Magnitude;

        /// <summary>
        /// 按分量计算 a + (b - a) * t，因子限制到零至一；不单独处理端点或非有限值。
        /// </summary>
        public static Vec3 Lerp(in Vec3 a, in Vec3 b, float t)
        {
            t = MathUtil.Clamp01(t);
            return a + (b - a) * t;
        }

        /// <summary>
        /// 逐分量相加，不检查浮点溢出或非有限值。
        /// </summary>
        public static Vec3 operator +(in Vec3 a, in Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        /// <summary>
        /// 逐分量相减或取反，不检查浮点溢出或非有限值。
        /// </summary>
        public static Vec3 operator -(in Vec3 a, in Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        /// <summary>
        /// 逐分量相减或取反，不检查浮点溢出或非有限值。
        /// </summary>
        public static Vec3 operator -(in Vec3 v) => new Vec3(-v.X, -v.Y, -v.Z);
        /// <summary>
        /// 按标量逐分量缩放，不归一化或限制结果范围。
        /// </summary>
        public static Vec3 operator *(in Vec3 v, float s) => new Vec3(v.X * s, v.Y * s, v.Z * s);
        /// <summary>
        /// 按标量逐分量缩放，不归一化或限制结果范围。
        /// </summary>
        public static Vec3 operator *(float s, in Vec3 v) => new Vec3(v.X * s, v.Y * s, v.Z * s);
        /// <summary>
        /// 逐分量执行浮点除法，零除数产生无穷或 NaN，不抛整数除零异常。
        /// </summary>
        public static Vec3 operator /(in Vec3 v, float s) => new Vec3(v.X / s, v.Y / s, v.Z / s);

        /// <summary>
        /// 原样复制 X、Y、Z 至 System.Numerics.Vector3，不改变坐标或单位。
        /// </summary>
        public System.Numerics.Vector3 ToNumerics() => new System.Numerics.Vector3(X, Y, Z);
        /// <summary>
        /// 原样读取 System.Numerics.Vector3 的 X、Y、Z，不改变坐标或单位。
        /// </summary>
        public static Vec3 FromNumerics(in System.Numerics.Vector3 v) => new Vec3(v.X, v.Y, v.Z);

        /// <summary>
        /// 逐分量使用 float.Equals 比较，不采用容差；NaN 分量可以相等，正负零相等。
        /// </summary>
        public bool Equals(Vec3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        /// <summary>
        /// 逐分量使用 float.Equals 比较，不采用容差；NaN 分量可以相等，正负零相等。
        /// </summary>
        public override bool Equals(object? obj) => obj is Vec3 other && Equals(other);
        /// <summary>
        /// 组合分量的进程内哈希，符合值相等规则；不得用于持久化或跨进程稳定标识。
        /// </summary>
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        /// <summary>
        /// 按当前区域设置输出诊断文本，不作为协议或稳定序列化格式。
        /// </summary>
        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
