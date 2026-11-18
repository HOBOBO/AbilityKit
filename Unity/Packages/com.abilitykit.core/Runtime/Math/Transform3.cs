using System;

namespace AbilityKit.Core.Mathematics
{
    /// <summary>
    /// 保存按逐轴缩放、旋转、平移顺序应用的三维变换；默认值不是恒等变换。
    /// </summary>
    public readonly struct Transform3 : IEquatable<Transform3>
    {
        /// <summary>
        /// 保存平移分量，单位由调用方决定。
        /// </summary>
        public readonly Vec3 Position;
        /// <summary>
        /// 保存旋转四元数，不自动归一化。
        /// </summary>
        public readonly Quat Rotation;
        /// <summary>
        /// 保存逐轴缩放，允许负数、零及非有限值；退化轴的逆操作使用零倒数。
        /// </summary>
        public readonly Vec3 Scale;

        /// <summary>
        /// 直接保存平移、旋转、缩放，不校验可逆性或旋转归一化状态。
        /// </summary>
        public Transform3(in Vec3 position, in Quat rotation, in Vec3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        /// <summary>
        /// 获取零平移、恒等旋转、单位缩放组成的恒等变换。
        /// </summary>
        public static Transform3 Identity => new Transform3(Vec3.Zero, Quat.Identity, Vec3.One);

        /// <summary>
        /// 将局部正 Z 按缩放和旋转转换，忽略平移；不保证单位长度。
        /// </summary>
        public Vec3 Forward => TransformDirection(Vec3.Forward);

        /// <summary>
        /// 将局部正 X 按缩放和旋转转换，忽略平移；不保证单位长度。
        /// </summary>
        public Vec3 Right => TransformDirection(Vec3.Right);

        /// <summary>
        /// 将局部正 Y 按缩放和旋转转换，忽略平移；不保证单位长度。
        /// </summary>
        public Vec3 Up => TransformDirection(Vec3.Up);

        /// <summary>
        /// 按逐轴缩放、旋转、平移顺序将局部点转换到世界空间。
        /// </summary>
        public Vec3 TransformPoint(in Vec3 localPoint)
        {
            var scaled = new Vec3(localPoint.X * Scale.X, localPoint.Y * Scale.Y, localPoint.Z * Scale.Z);
            var rotated = Rotation.Rotate(scaled);
            return rotated + Position;
        }

        /// <summary>
        /// 先减平移，再逆旋转，最后乘逐轴缩放倒数；缩放绝对值不大于 Epsilon 时该轴使用零倒数。
        /// </summary>
        public Vec3 InverseTransformPoint(in Vec3 worldPoint)
        {
            var p = worldPoint - Position;
            var invRot = Rotation.Inverse;
            var unrotated = invRot.Rotate(p);

            var ix = MathUtil.Abs(Scale.X) > MathUtil.Epsilon ? 1f / Scale.X : 0f;
            var iy = MathUtil.Abs(Scale.Y) > MathUtil.Epsilon ? 1f / Scale.Y : 0f;
            var iz = MathUtil.Abs(Scale.Z) > MathUtil.Epsilon ? 1f / Scale.Z : 0f;

            return new Vec3(unrotated.X * ix, unrotated.Y * iy, unrotated.Z * iz);
        }

        /// <summary>
        /// 先逐轴缩放再旋转，忽略平移，不自动归一化。
        /// </summary>
        public Vec3 TransformDirection(in Vec3 localDir)
        {
            /*
             * 方向变换契约包含缩放，不可从其他引擎的同名接口推断为仅旋转。
             */
            var scaled = new Vec3(localDir.X * Scale.X, localDir.Y * Scale.Y, localDir.Z * Scale.Z);
            return Rotation.Rotate(scaled);
        }

        /// <summary>
        /// 先逆旋转再乘逐轴缩放倒数，忽略平移；缩放绝对值不大于 Epsilon 时该轴使用零倒数。
        /// </summary>
        public Vec3 InverseTransformDirection(in Vec3 worldDir)
        {
            var invRot = Rotation.Inverse;
            var unrotated = invRot.Rotate(worldDir);

            var ix = MathUtil.Abs(Scale.X) > MathUtil.Epsilon ? 1f / Scale.X : 0f;
            var iy = MathUtil.Abs(Scale.Y) > MathUtil.Epsilon ? 1f / Scale.Y : 0f;
            var iz = MathUtil.Abs(Scale.Z) > MathUtil.Epsilon ? 1f / Scale.Z : 0f;

            return new Vec3(unrotated.X * ix, unrotated.Y * iy, unrotated.Z * iz);
        }

        /// <summary>
        /// 返回逆旋转、缩放倒数及逆平移组成的变换；非均匀缩放与旋转不可交换时不能表示一般精确逆变换。
        /// </summary>
        /// <remarks>
        /// 应使用 InverseTransformPoint 或 InverseTransformDirection 直接逆变换点或方向；该属性不存储一般逆矩阵，也不报告不可逆状态。
        /// </remarks>
        public Transform3 Inverse
        {
            get
            {
                var invRot = Rotation.Inverse;

                var ix = MathUtil.Abs(Scale.X) > MathUtil.Epsilon ? 1f / Scale.X : 0f;
                var iy = MathUtil.Abs(Scale.Y) > MathUtil.Epsilon ? 1f / Scale.Y : 0f;
                var iz = MathUtil.Abs(Scale.Z) > MathUtil.Epsilon ? 1f / Scale.Z : 0f;

                var invScale = new Vec3(ix, iy, iz);

                /*
                 * 先逆旋转再逐轴除缩放计算逆平移；返回变换仍受缩放与旋转不可交换的限制。
                 */
                var p = invRot.Rotate(-Position);
                p = new Vec3(p.X * ix, p.Y * iy, p.Z * iz);

                return new Transform3(p, invRot, invScale);
            }
        }

        /// <summary>
        /// 分别比较平移、旋转和缩放，不采用容差，也不比较几何作用是否等价。
        /// </summary>
        public bool Equals(Transform3 other) => Position.Equals(other.Position) && Rotation.Equals(other.Rotation) && Scale.Equals(other.Scale);
        /// <summary>
        /// 分别比较平移、旋转和缩放，不采用容差，也不比较几何作用是否等价。
        /// </summary>
        public override bool Equals(object? obj) => obj is Transform3 other && Equals(other);
        /// <summary>
        /// 组合分量的进程内哈希，符合值相等规则；不得用于持久化或跨进程稳定标识。
        /// </summary>
        public override int GetHashCode() => HashCode.Combine(Position, Rotation, Scale);
        /// <summary>
        /// 按当前区域设置输出诊断文本，不作为协议或稳定序列化格式。
        /// </summary>
        public override string ToString() => $"Pos={Position}, Rot={Rotation}, Scale={Scale}";
    }
}
