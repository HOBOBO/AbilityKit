using AbilityKit.Core.Math;

namespace AbilityKit.Ability.Impl.Triggering.SummonSpawning
{
    public readonly struct SpawnSummonContextData
    {
        public readonly int CasterActorId;
        public readonly object TargetObj;
        public readonly Vec3 AimPos;
        public readonly Vec3 FixedPos;

        public SpawnSummonContextData(int casterActorId, object targetObj, in Vec3 aimPos, in Vec3 fixedPos)
        {
            CasterActorId = casterActorId;
            TargetObj = targetObj;
            AimPos = aimPos;
            FixedPos = fixedPos;
        }
    }
}
