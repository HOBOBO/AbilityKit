using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Common.MotionSystem.Core
{
    public struct MotionOutput
    {
        public Vec3 DesiredDelta;
        public Vec3 AppliedDelta;
        public Vec3 NewVelocity;
        public Vec3 NewForward;

        public void Clear()
        {
            DesiredDelta = Vec3.Zero;
            AppliedDelta = Vec3.Zero;
            NewVelocity = Vec3.Zero;
            NewForward = Vec3.Zero;
        }
    }
}
