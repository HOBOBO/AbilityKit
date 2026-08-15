using AbilityKit.Deterministic;

namespace AbilityKit.Core.Mathematics
{
    /// <summary>
    /// 定点数学桥（全仓库唯一实现）：把漂移敏感运算（开方、向量长度、归一化）切换到
    /// AbilityKit.Deterministic 的整数确定性实现，输入输出保持 float
    /// （边界处单次 IEEE 换算，跨平台 .NET/Mono/IL2CPP 位一致）。
    /// 其余 float 算术（加减乘除/比较）本身即 IEEE 位一致。
    /// 坐标需在 Fixed64 表示范围内（约 ±2.1e9），战斗场景坐标远小于此。
    /// 历史注记：collision/motion/demo.moba 各自的 internal 副本已于 P4 收敛为本实现（同算法）。
    /// </summary>
    public static class DeterministicMathBridge
    {
        /// <summary>与 <see cref="MathUtil.Epsilon"/> 对齐的定点 epsilon。</summary>
        public static Fixed64 Epsilon { get; } = Fixed64.FromSingle(MathUtil.Epsilon);

        /// <summary>
        /// 守卫式 float → Fixed64：NaN/Inf 归零（对齐数值管线旧的 NaN/Inf 防护行为），
        /// 有限值精确换算。配置/表现边界换算统一走这里；希望非有限值直接失败的入口
        /// （如出生参数校验）可自行使用 <see cref="Fixed64.FromSingle"/>。
        /// </summary>
        public static Fixed64 ToFixed(float value)
        {
            return float.IsFinite(value) ? Fixed64.FromSingle(value) : Fixed64.Zero;
        }

        /// <summary>Fixed64 → float 边界视图（单次 IEEE 换算）。</summary>
        public static float ToSingle(Fixed64 value)
        {
            return value.ToSingle();
        }

        public static FixedVec3 ToFixed(in Vec3 value)
        {
            return new FixedVec3(Fixed64.FromSingle(value.X), Fixed64.FromSingle(value.Y), Fixed64.FromSingle(value.Z));
        }

        public static Vec3 ToVec3(in FixedVec3 value)
        {
            return new Vec3(value.X.ToSingle(), value.Y.ToSingle(), value.Z.ToSingle());
        }

        /// <summary>确定性归一化；零向量返回零（与 Vec3.Normalized 语义一致）。</summary>
        public static Vec3 Normalize(in Vec3 value)
        {
            return ToVec3(ToFixed(value).Normalized);
        }

        /// <summary>确定性四元数归一化（配置转换/朝向计算用）；零四元数返回 Identity。</summary>
        public static Quat Normalize(in Quat value)
        {
            var x = Fixed64.FromSingle(value.X);
            var y = Fixed64.FromSingle(value.Y);
            var z = Fixed64.FromSingle(value.Z);
            var w = Fixed64.FromSingle(value.W);
            var len = DeterministicMath.Sqrt((x * x) + (y * y) + (z * z) + (w * w));
            if (len == Fixed64.Zero)
            {
                return Quat.Identity;
            }

            return new Quat((x / len).ToSingle(), (y / len).ToSingle(), (z / len).ToSingle(), (w / len).ToSingle());
        }

        /// <summary>确定性向量长度。</summary>
        public static float Magnitude(in Vec3 value)
        {
            return DeterministicMath.Sqrt(ToFixed(value).SqrMagnitude).ToSingle();
        }

        /// <summary>确定性开方（调用方自行保证非负）。</summary>
        public static float Sqrt(float value)
        {
            return DeterministicMath.Sqrt(Fixed64.FromSingle(value)).ToSingle();
        }

        /// <summary>确定性三角函数（float 边界；内核为 CORDIC 整数实现，跨运行时位一致）。</summary>
        public static float Cos(float radians)
        {
            return DeterministicMath.Cos(ToFixed(radians)).ToSingle();
        }

        public static float Sin(float radians)
        {
            return DeterministicMath.Sin(ToFixed(radians)).ToSingle();
        }

        public static float Tan(float radians)
        {
            return DeterministicMath.Tan(ToFixed(radians)).ToSingle();
        }

        public static float Atan2(float y, float x)
        {
            return DeterministicMath.Atan2(ToFixed(y), ToFixed(x)).ToSingle();
        }
    }
}
