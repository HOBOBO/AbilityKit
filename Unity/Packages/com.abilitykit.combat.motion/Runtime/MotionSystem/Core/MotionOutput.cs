using AbilityKit.Combat.MotionSystem.Constraints;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.MotionSystem.Core
{
    public struct MotionOutput
    {
        public Vec3 DesiredDelta;
        public Vec3 AppliedDelta;
        public Vec3 NewVelocity;
        public Vec3 NewForward;

        /// <summary>主导贡献源（如 dash/blink）携带的碰撞策略；仅当 <see cref="HasDominantCollisionPolicy"/> 为真时有效。</summary>
        public MotionCollisionConstraints DominantCollisionPolicy;

        /// <summary>本帧是否有主导源提供了碰撞策略覆盖。</summary>
        public bool HasDominantCollisionPolicy;

        public void Clear()
        {
            DesiredDelta = Vec3.Zero;
            AppliedDelta = Vec3.Zero;
            NewVelocity = Vec3.Zero;
            NewForward = Vec3.Zero;
            HasDominantCollisionPolicy = false;
        }
    }
}
