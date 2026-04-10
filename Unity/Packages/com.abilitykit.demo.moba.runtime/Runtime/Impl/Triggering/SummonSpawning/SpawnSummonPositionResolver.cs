using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Core.Math;

namespace AbilityKit.Ability.Impl.Triggering.SummonSpawning
{
    public static class SpawnSummonPositionResolver
    {
        public static Vec3 ResolveAnchorPos(SpawnSummonSpec.PositionMode mode, MobaActorRegistry registry, int casterActorId, int targetActorId, in Vec3 aimPos, in Vec3 fixedPos)
        {
            switch (mode)
            {
                case SpawnSummonSpec.PositionMode.Target:
                {
                    if (targetActorId > 0 && registry != null && registry.TryGet(targetActorId, out var e) && e != null && e.hasTransform)
                    {
                        return e.transform.Value.Position;
                    }
                    return default;
                }
                case SpawnSummonSpec.PositionMode.AimPos:
                {
                    return aimPos;
                }
                case SpawnSummonSpec.PositionMode.Fixed:
                {
                    return fixedPos;
                }
                case SpawnSummonSpec.PositionMode.Caster:
                default:
                {
                    if (casterActorId > 0 && registry != null && registry.TryGet(casterActorId, out var e) && e != null && e.hasTransform)
                    {
                        return e.transform.Value.Position;
                    }
                    return default;
                }
            }
        }
    }
}
