using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    internal static class EffectContextPlanValueResolver
    {
        public static bool TryGetCasterActorId(object args, out int casterActorId)
        {
            casterActorId = 0;

            if (args is EffectContextWrapper ec)
            {
                if (ec.Kind == EffectContextKind.Skill)
                {
                    casterActorId = ec.SourceActorId;
                }
                return casterActorId > 0;
            }

            return false;
        }

        public static bool TryGetTargetActorId(object args, out int targetActorId)
        {
            targetActorId = 0;

            if (args is EffectContextWrapper ec)
            {
                if (ec.Kind == EffectContextKind.Skill)
                {
                    targetActorId = ec.TargetActorId;
                }
                return targetActorId > 0;
            }

            if (args is IEffectContext ec2)
            {
                if (ec2.Kind != EffectContextKind.Skill) return false;
                targetActorId = ec2.TargetActorId;
                return targetActorId > 0;
            }

            return false;
        }

        public static bool TryGetAim(object args, out Vec3 aimPos, out Vec3 aimDir)
        {
            aimPos = Vec3.Zero;
            aimDir = Vec3.Zero;

            if (args is IEffectContext ec)
            {
                if (ec.Kind != EffectContextKind.Skill) return false;
                if (!ec.TryGetSkill(out var skill)) return false;

                aimPos = skill.AimPos;
                aimDir = skill.AimDir;
                return true;
            }

            return false;
        }

        public static bool TryGetAimPos(object args, out Vec3 aimPos)
        {
            aimPos = Vec3.Zero;

            if (args is IEffectContext ec)
            {
                if (ec.Kind != EffectContextKind.Skill) return false;
                if (!ec.TryGetSkill(out var skill)) return false;

                aimPos = skill.AimPos;
                return true;
            }

            return false;
        }

        public static bool TryGetAimDir(object args, out Vec3 aimDir)
        {
            aimDir = Vec3.Zero;

            if (args is IEffectContext ec)
            {
                if (ec.Kind != EffectContextKind.Skill) return false;
                if (!ec.TryGetSkill(out var skill)) return false;

                aimDir = skill.AimDir;
                return true;
            }

            return false;
        }
    }
}
