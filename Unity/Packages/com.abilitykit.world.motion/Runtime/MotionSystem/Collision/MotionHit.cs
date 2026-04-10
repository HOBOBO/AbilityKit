using AbilityKit.Core.Math;

namespace AbilityKit.Core.Common.MotionSystem.Collision
{
    public readonly struct MotionHit
    {
        public readonly bool Hit;
        public readonly int TargetId;
        public readonly Vec3 Normal;
        public readonly float Time01;

        public MotionHit(bool hit, int targetId, in Vec3 normal, float time01)
        {
            Hit = hit;
            TargetId = targetId;
            Normal = normal;
            Time01 = time01;
        }

        public static MotionHit None => new MotionHit(false, 0, Vec3.Zero, 0f);
    }
}
