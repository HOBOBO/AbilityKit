using System;

namespace AbilityKit.Core.Mathematics
{
    /// <summary>
    /// 按 X、Y、Z、W 保存四元数分量；构造不归一化，默认零值不是恒等旋转。
    /// </summary>
    public readonly struct Quat : IEquatable<Quat>
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
        /// 保存 W 单精度分量，不校验取值范围。
        /// </summary>
        public readonly float W;

        /// <summary>
        /// 直接保存 X、Y、Z、W，不校验长度、归一化状态或有限性。
        /// </summary>
        public Quat(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        /// <summary>
        /// 获取分量为零、零、零、一的恒等旋转。
        /// </summary>
        public static Quat Identity => new Quat(0f, 0f, 0f, 1f);

        /// <summary>
        /// 归一化轴后使用弧度角的半角正余弦创建四元数；零轴得到零向量部及半角余弦标量部，不保证为单位四元数。
        /// </summary>
        public static Quat FromAxisAngle(in Vec3 axis, float angleRad)
        {
            var ax = axis.Normalized;
            var half = angleRad * 0.5f;
            var s = System.MathF.Sin(half);
            var c = System.MathF.Cos(half);
            return new Quat(ax.X * s, ax.Y * s, ax.Z * s, c);
        }

        /// <summary>
        /// 以前方向为局部正 Z，上方向叉乘前方向生成正 X；退化时改用世界正 Y，仍退化则返回恒等旋转。
        /// </summary>
        /// <remarks>
        /// 前方向归一化后退化则返回恒等旋转；世界正 Y 回退仍共线时同样返回恒等旋转，不报告失败原因。
        /// </remarks>
        public static Quat LookRotation(in Vec3 forward, in Vec3 up)
        {
            var f = forward.Normalized;
            if (f.SqrMagnitude <= MathUtil.Epsilon) return Identity;

            var r = Vec3.Cross(up, f).Normalized;
            if (r.SqrMagnitude <= MathUtil.Epsilon)
            {
                r = Vec3.Cross(Vec3.Up, f).Normalized;
                if (r.SqrMagnitude <= MathUtil.Epsilon) return Identity;
            }

            var u = Vec3.Cross(f, r);

            /*
             * 右、上、前基向量作为矩阵列，随后按矩阵迹或最大对角分量选择转换分支。
             */
            var m00 = r.X; var m01 = u.X; var m02 = f.X;
            var m10 = r.Y; var m11 = u.Y; var m12 = f.Y;
            var m20 = r.Z; var m21 = u.Z; var m22 = f.Z;

            var trace = m00 + m11 + m22;
            if (trace > 0f)
            {
                var s = MathUtil.Sqrt(trace + 1f) * 2f;
                var inv = 1f / s;
                return new Quat(
                    (m21 - m12) * inv,
                    (m02 - m20) * inv,
                    (m10 - m01) * inv,
                    0.25f * s).Normalized;
            }

            if (m00 > m11 && m00 > m22)
            {
                var s = MathUtil.Sqrt(1f + m00 - m11 - m22) * 2f;
                var inv = 1f / s;
                return new Quat(
                    0.25f * s,
                    (m01 + m10) * inv,
                    (m02 + m20) * inv,
                    (m21 - m12) * inv).Normalized;
            }

            if (m11 > m22)
            {
                var s = MathUtil.Sqrt(1f + m11 - m00 - m22) * 2f;
                var inv = 1f / s;
                return new Quat(
                    (m01 + m10) * inv,
                    0.25f * s,
                    (m12 + m21) * inv,
                    (m02 - m20) * inv).Normalized;
            }

            {
                var s = MathUtil.Sqrt(1f + m22 - m00 - m11) * 2f;
                var inv = 1f / s;
                return new Quat(
                    (m02 + m20) * inv,
                    (m12 + m21) * inv,
                    0.25f * s,
                    (m10 - m01) * inv).Normalized;
            }
        }

        /// <summary>
        /// 长度平方不大于 Epsilon 时返回恒等旋转，否则通过 MathUtil.Sqrt 求长度并归一化；NaN 不触发退化回退。
        /// </summary>
        public Quat Normalized
        {
            get
            {
                var lenSq = X * X + Y * Y + Z * Z + W * W;
                if (lenSq <= MathUtil.Epsilon) return Identity;
                var inv = 1f / MathUtil.Sqrt(lenSq);
                return new Quat(X * inv, Y * inv, Z * inv, W * inv);
            }
        }

        /// <summary>
        /// 将 X、Y、Z 取反并保留 W；单位四元数的共轭等于其逆。
        /// </summary>
        public Quat Conjugate => new Quat(-X, -Y, -Z, W);

        /// <summary>
        /// 长度平方不大于 Epsilon 时返回恒等旋转，否则将共轭除以长度平方，不校验有限性。
        /// </summary>
        public Quat Inverse
        {
            get
            {
                /*
                 * 除以长度平方以支持非单位四元数；退化阈值仍采用既有恒等回退。
                 */
                var lenSq = X * X + Y * Y + Z * Z + W * W;
                if (lenSq <= MathUtil.Epsilon) return Identity;
                var inv = 1f / lenSq;
                return new Quat(-X * inv, -Y * inv, -Z * inv, W * inv);
            }
        }

        /// <summary>
        /// 计算 Hamilton 乘积，不归一化；用于有效旋转时 a * b 表示先应用 b 再应用 a。
        /// </summary>
        public static Quat operator *(in Quat a, in Quat b)
        {
            return new Quat(
                a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
                a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
                a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
                a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);
        }

        /// <summary>
        /// 执行 q * (v, 0) * Inverse(q) 并取向量部，不提前归一化；零四元数得到零向量。
        /// </summary>
        /// <remarks>
        /// 近零四元数受到逆运算的恒等回退影响，不能作为有效旋转使用；普通浮点运算和轴角三角函数不承诺完整锁步确定性。
        /// </remarks>
        public Vec3 Rotate(in Vec3 v)
        {
            /*
             * 通过四元数夹乘执行旋转，不隐式归一化原始输入。
             */
            var qv = new Quat(v.X, v.Y, v.Z, 0f);
            var r = this * qv * Inverse;
            return new Vec3(r.X, r.Y, r.Z);
        }

        /// <summary>
        /// 原样复制 X、Y、Z、W 至 System.Numerics.Quaternion，不归一化。
        /// </summary>
        public System.Numerics.Quaternion ToNumerics() => new System.Numerics.Quaternion(X, Y, Z, W);
        /// <summary>
        /// 原样读取 System.Numerics.Quaternion 的 X、Y、Z、W，不归一化。
        /// </summary>
        public static Quat FromNumerics(in System.Numerics.Quaternion q) => new Quat(q.X, q.Y, q.Z, q.W);

        /// <summary>
        /// 逐分量使用 float.Equals 比较，不采用容差；NaN 分量可以相等，正负零相等。
        /// </summary>
        public bool Equals(Quat other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && W.Equals(other.W);
        /// <summary>
        /// 逐分量使用 float.Equals 比较，不采用容差；NaN 分量可以相等，正负零相等。
        /// </summary>
        public override bool Equals(object? obj) => obj is Quat other && Equals(other);
        /// <summary>
        /// 组合分量的进程内哈希，符合值相等规则；不得用于持久化或跨进程稳定标识。
        /// </summary>
        public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);
        /// <summary>
        /// 按当前区域设置输出诊断文本，不作为协议或稳定序列化格式。
        /// </summary>
        public override string ToString() => $"({X}, {Y}, {Z}, {W})";
    }
}
