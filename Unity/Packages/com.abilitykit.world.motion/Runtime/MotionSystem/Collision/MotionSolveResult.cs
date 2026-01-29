using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Common.MotionSystem.Collision
{
    public readonly struct MotionSolveResult
    {
        public readonly Vec3 AppliedDelta;
        public readonly MotionHit Hit;

        public MotionSolveResult(in Vec3 appliedDelta, in MotionHit hit)
        {
            AppliedDelta = appliedDelta;
            Hit = hit;
        }

        public static MotionSolveResult NoHit(in Vec3 appliedDelta) => new MotionSolveResult(appliedDelta, MotionHit.None);
    }
}
