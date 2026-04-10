using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Core.Math;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    internal static class SkillCastContextPlanValueResolver
    {
        public static bool TryGetCasterActorId(object args, out int casterActorId)
        {
            casterActorId = 0;
            if (args is SkillCastContext scc)
            {
                casterActorId = scc.CasterActorId;
                return casterActorId > 0;
            }

            return false;
        }

        public static bool TryGetTargetActorId(object args, out int targetActorId)
        {
            targetActorId = 0;
            if (args is SkillCastContext scc)
            {
                targetActorId = scc.TargetActorId;
                return targetActorId > 0;
            }

            return false;
        }

        public static bool TryGetAim(object args, out Vec3 aimPos, out Vec3 aimDir)
        {
            aimPos = Vec3.Zero;
            aimDir = Vec3.Zero;

            if (args is SkillCastContext scc)
            {
                aimPos = scc.AimPos;
                aimDir = scc.AimDir;
                return true;
            }

            return false;
        }

        public static bool TryGetAimPos(object args, out Vec3 aimPos)
        {
            aimPos = Vec3.Zero;

            if (args is SkillCastContext scc)
            {
                aimPos = scc.AimPos;
                return true;
            }

            return false;
        }

        public static bool TryGetAimDir(object args, out Vec3 aimDir)
        {
            aimDir = Vec3.Zero;

            if (args is SkillCastContext scc)
            {
                aimDir = scc.AimDir;
                return true;
            }

            return false;
        }
    }
}
