using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// Resolves the common placement inputs used by temporary entities such as
    /// summons and areas. Domain modules still own their lifetime and runtime
    /// registration; only the placement contract is shared here.
    /// </summary>
    internal static class MobaTemporaryEntityPositionResolver
    {
        public static bool TryResolve(
            MobaActorLookupService actors,
            int casterActorId,
            int targetActorId,
            in Vec3 aimPosition,
            bool hasAimPosition,
            SpawnSummonPositionMode positionMode,
            out Vec3 position)
        {
            position = Vec3.Zero;
            switch (positionMode)
            {
                case SpawnSummonPositionMode.Caster:
                    return TryGetActorPosition(actors, casterActorId, out position);
                case SpawnSummonPositionMode.Target:
                    return TryGetActorPosition(actors, targetActorId, out position);
                case SpawnSummonPositionMode.AimPos:
                    if (!hasAimPosition) return false;
                    position = new Vec3(aimPosition.X, 0, aimPosition.Z);
                    return true;
                case SpawnSummonPositionMode.Fixed:
                    return true;
                default:
                    return false;
            }
        }

        public static bool TryGetActorPosition(MobaActorLookupService actors, int actorId, out Vec3 position)
        {
            position = Vec3.Zero;
            if (actorId <= 0 || actors == null) return false;
            if (!actors.TryGetActorEntity(actorId, out var actor) || actor == null || !actor.hasTransform)
                return false;

            position = actor.transform.Value.Position;
            return true;
        }
    }
}
