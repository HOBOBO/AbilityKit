using System;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Input;

namespace AbilityKit.Demo.Moba.Services.Motion
{
    /// <summary>
    /// 计算本帧所有者跟随的速度上限，供直线和导航驱动共同使用。
    /// </summary>
    internal static class MobaOwnerFollowMovement
    {
        internal const float DistanceEpsilon = 0.001f;

        internal static float OutwardDot(global::ActorEntity owner, in Vec3 toOwner)
        {
            var velocity = owner.hasMotion && owner.motion.Initialized ? owner.motion.State.Velocity : Vec3.Zero;
            return velocity.X * toOwner.X + velocity.Z * toOwner.Z;
        }

        internal static float ResolveSpeed(global::ActorEntity actor, MobaActorRegistry actors,
            in MobaActorIntent intent, float speed, float deltaTime)
        {
            if (deltaTime <= 0f || speed <= 0f || !actor.hasOwnerLink || !actor.hasTransform ||
                !actors.TryGet(actor.ownerLink.OwnerActorId, out var owner) || owner == null || !owner.isEnabled)
                return 0f;

            var position = actor.transform.Value.Position;
            var target = intent.MoveTarget;
            var delta = new Vec3(target.X - position.X, 0f, target.Z - position.Z);
            var distance = DeterministicMathBridge.Magnitude(in delta);
            if (distance <= DistanceEpsilon) return 0f;

            /*
             * 沿径向只补本帧可以关闭的距离。所有者的上帧实际位移速度提供远离量，
             * 不预读玩家输入；受阻、静止、切向或靠近均不会增加追赶速度。
             */
            var outwardSpeed = Math.Max(0f, OutwardDot(owner, in delta) / distance);
            var step = distance - intent.MoveStopRange + outwardSpeed * deltaTime;
            return step > DistanceEpsilon ? Math.Min(speed, step / deltaTime) : 0f;
        }
    }
}
