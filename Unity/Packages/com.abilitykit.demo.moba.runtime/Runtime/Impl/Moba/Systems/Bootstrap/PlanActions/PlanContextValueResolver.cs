using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public static class PlanContextValueResolver
    {
        public static bool TryGetCasterActorId(object args, out int casterActorId)
        {
            casterActorId = 0;
            if (EffectContextPlanValueResolver.TryGetCasterActorId(args, out casterActorId)) return true;
            if (SkillPipelineContextPlanValueResolver.TryGetCasterActorId(args, out casterActorId)) return true;
            return SkillCastContextPlanValueResolver.TryGetCasterActorId(args, out casterActorId);
        }

        public static bool TryGetAimPos(object args, out Vec3 aimPos)
        {
            if (EffectContextPlanValueResolver.TryGetAimPos(args, out aimPos)) return true;
            if (SkillPipelineContextPlanValueResolver.TryGetAimPos(args, out aimPos)) return true;
            return SkillCastContextPlanValueResolver.TryGetAimPos(args, out aimPos);
        }

        public static bool TryGetAimDir(object args, out Vec3 aimDir)
        {
            if (EffectContextPlanValueResolver.TryGetAimDir(args, out aimDir)) return true;
            if (SkillPipelineContextPlanValueResolver.TryGetAimDir(args, out aimDir)) return true;
            return SkillCastContextPlanValueResolver.TryGetAimDir(args, out aimDir);
        }

        public static bool TryGetAim(object args, out Vec3 aimPos, out Vec3 aimDir)
        {
            if (EffectContextPlanValueResolver.TryGetAim(args, out aimPos, out aimDir)) return true;
            if (SkillPipelineContextPlanValueResolver.TryGetAim(args, out aimPos, out aimDir)) return true;
            return SkillCastContextPlanValueResolver.TryGetAim(args, out aimPos, out aimDir);
        }
    }
}
