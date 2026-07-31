using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.MotionSystem.Collision
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

        /// <summary>
        /// 沿位移方向把落点投影到障碍物边界外：当 <paramref name="to"/> 落在障碍物内时，
        /// 沿 <c>to→from</c> 方向找到最近的出墙点（障碍物沿位移连线的边界）。
        /// 用于穿墙位移（blink/pass-wall）终点落墙内的修正。
        /// </summary>
        bool TryProjectToFreeDirectional(
            int moverId,
            in Vec3 from,
            in Vec3 to,
            float radius,
            int obstacleMask,
            int ignoreMask,
            out Vec3 projectedPosition);
    }
}
