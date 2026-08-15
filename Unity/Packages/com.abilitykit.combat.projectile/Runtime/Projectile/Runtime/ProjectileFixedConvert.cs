using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;

namespace AbilityKit.Combat.Projectile
{
    /// <summary>
    /// float 边界（出生参数、事件导出、碰撞世界查询）与内部定点运动学之间的转换：
    /// 仅是 <see cref="DeterministicMathBridge"/> 的扩展方法语法糖（算法唯一实现于 Core）。
    /// FromSingle/ToSingle 都是单次 IEEE 乘/除，跨平台位一致；这些转换只发生在边界，
    /// 不进入逐帧运动积分路径（积分全程 Fixed64/FixedVec3）。
    /// 注意：float 标量扩展走 <see cref="Fixed64.FromSingle"/>（非有限值抛异常，出生参数 fail-loud）。
    /// </summary>
    internal static class ProjectileFixedConvert
    {
        public static Fixed64 ToFixed(this float value) => Fixed64.FromSingle(value);

        public static FixedVec3 ToFixed(this in Vec3 value) => DeterministicMathBridge.ToFixed(value);

        public static Vec3 ToVec3(this in FixedVec3 value) => DeterministicMathBridge.ToVec3(value);
    }
}
