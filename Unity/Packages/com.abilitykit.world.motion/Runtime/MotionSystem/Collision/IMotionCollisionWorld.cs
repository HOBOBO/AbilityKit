using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Common.MotionSystem.Collision
{
    public interface IMotionCollisionWorld
    {
        bool Sweep(
            int moverId,
            in Vec3 start,
            in Vec3 desiredDelta,
            float radius,
            int obstacleMask,
            int ignoreMask,
            out MotionHit hit,
            out Vec3 appliedDelta);

        bool Overlap(
            int moverId,
            in Vec3 position,
            float radius,
            int obstacleMask,
            int ignoreMask);

        bool TryProjectToFree(
            int moverId,
            in Vec3 position,
            float radius,
            int obstacleMask,
            int ignoreMask,
            out Vec3 projectedPosition);
    }
}
