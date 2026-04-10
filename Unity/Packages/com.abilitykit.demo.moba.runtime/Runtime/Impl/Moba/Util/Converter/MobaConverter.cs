using AbilityKit.Core.Math;

namespace AbilityKit.Ability.Share.Impl.Moba.Util.Converter
{
    // 逻辑层通用数据转换工具（用于将外部输入转换为逻辑层使用的数据结构）
    public static class MobaConverter
    {
        // 将 System.Numerics.Vector3 转为逻辑层 Vec3
        public static Vec3 ToVec3(in System.Numerics.Vector3 v)
        {
            return new Vec3(v.X, v.Y, v.Z);
        }

        // 将 System.Numerics.Quaternion 转为逻辑层 Quat
        public static Quat ToQuat(in System.Numerics.Quaternion q)
        {
            return new Quat(q.X, q.Y, q.Z, q.W);
        }

        // 从欧拉角（弧度）创建逻辑层 Quat（按 Yaw-Pitch-Roll 顺序组合：Y * X * Z）
        public static Quat ToQuatFromEulerRad(float pitchRad, float yawRad, float rollRad)
        {
            var qx = Quat.FromAxisAngle(Vec3.Right, pitchRad);
            var qy = Quat.FromAxisAngle(Vec3.Up, yawRad);
            var qz = Quat.FromAxisAngle(Vec3.Forward, rollRad);
            return (qy * qx * qz).Normalized;
        }

        // 从朝向角（Yaw，弧度）创建逻辑层 Quat（常用于 MOBA 的平面转向）
        public static Quat ToYawRotationRad(float yawRad)
        {
            return Quat.FromAxisAngle(Vec3.Up, yawRad).Normalized;
        }

        // 将平面坐标（x,z）转换为 3D 坐标（y 默认 0）
        public static Vec3 ToXZ(float x, float z, float y = 0f)
        {
            return new Vec3(x, y, z);
        }

        // 创建逻辑层 Transform3
        public static Transform3 ToTransform(in Vec3 position, in Quat rotation, in Vec3 scale)
        {
            return new Transform3(position, rotation, scale);
        }

        // 创建逻辑层 Transform3（常用：单位缩放）
        public static Transform3 ToTransform(in Vec3 position, in Quat rotation)
        {
            return new Transform3(position, rotation, Vec3.One);
        }

        // 创建逻辑层 Transform3（常用：Yaw 转向 + 单位缩放）
        public static Transform3 ToTransformYaw(in Vec3 position, float yawRad)
        {
            return new Transform3(position, ToYawRotationRad(yawRad), Vec3.One);
        }
    }
}
