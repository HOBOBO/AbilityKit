using System;
using AbilityKit.Ability;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public static class SkillPipelineContextExtensions
    {
        public static int GetSkillId(this IAbilityPipelineContext ctx)
        {
            if (ctx is SkillPipelineContext spc) return spc.SkillId;
            return ctx.GetData<int>(MobaSkillPipelineSharedKeys.SkillId);
        }

        public static int GetSkillSlot(this IAbilityPipelineContext ctx)
        {
            if (ctx is SkillPipelineContext spc) return spc.SkillSlot;
            return ctx.GetData<int>(MobaSkillPipelineSharedKeys.SkillSlot);
        }

        public static int GetCasterActorId(this IAbilityPipelineContext ctx)
        {
            if (ctx is SkillPipelineContext spc) return spc.CasterActorId;
            return ctx.GetData<int>(MobaSkillPipelineSharedKeys.CasterActorId);
        }

        public static int GetTargetActorId(this IAbilityPipelineContext ctx)
        {
            if (ctx is SkillPipelineContext spc) return spc.TargetActorId;
            return ctx.GetData<int>(MobaSkillPipelineSharedKeys.TargetActorId);
        }

        public static Vec3 GetAimPos(this IAbilityPipelineContext ctx)
        {
            if (ctx is SkillPipelineContext spc) return spc.AimPos;
            return ctx.GetData<Vec3>(MobaSkillPipelineSharedKeys.AimPos);
        }

        public static Vec3 GetAimDir(this IAbilityPipelineContext ctx)
        {
            if (ctx is SkillPipelineContext spc) return spc.AimDir;
            return ctx.GetData<Vec3>(MobaSkillPipelineSharedKeys.AimDir);
        }

        public static void SetFailReason(this IAbilityPipelineContext ctx, string reason)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            ctx.SetData(MobaSkillPipelineSharedKeys.FailReason, reason);
        }

        public static string GetFailReason(this IAbilityPipelineContext ctx)
        {
            return ctx.GetData<string>(MobaSkillPipelineSharedKeys.FailReason);
        }

        public static int NextCastSequence(this IAbilityPipelineContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            var current = ctx.GetData<int>(MobaSkillPipelineSharedKeys.CastSequence);
            current++;
            ctx.SetData(MobaSkillPipelineSharedKeys.CastSequence, current);
            return current;
        }
    }
}
