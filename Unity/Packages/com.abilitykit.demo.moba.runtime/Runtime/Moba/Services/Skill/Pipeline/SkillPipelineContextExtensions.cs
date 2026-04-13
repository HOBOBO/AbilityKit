using System;
using AbilityKit.Core.Generic;
using AbilityKit.Core.Math;

namespace AbilityKit.Demo.Moba.Services
{
    using AbilityKit.Ability;
    /// <summary>
    /// SkillPipelineContext 专用扩展方法
    /// 提供从上下文属性或共享数据读取的能力
    /// </summary>
    public static class SkillPipelineContextExtensions
    {
        /// <summary>
        /// 获取技能ID（优先从属性读取）
        /// </summary>
        public static int GetSkillId(this SkillPipelineContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            return ctx.SkillId;
        }

        /// <summary>
        /// 获取技能槽位（优先从属性读取）
        /// </summary>
        public static int GetSkillSlot(this SkillPipelineContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            return ctx.SkillSlot;
        }

        /// <summary>
        /// 获取施法者角色ID（优先从属性读取）
        /// </summary>
        public static int GetCasterActorId(this SkillPipelineContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            return ctx.CasterActorId;
        }

        /// <summary>
        /// 获取目标角色ID（优先从属性读取）
        /// </summary>
        public static int GetTargetActorId(this SkillPipelineContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            return ctx.TargetActorId;
        }

        /// <summary>
        /// 获取目标位置（优先从属性读取）
        /// </summary>
        public static Vec3 GetAimPos(this SkillPipelineContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            return ctx.AimPos;
        }

        /// <summary>
        /// 获取目标方向（优先从属性读取）
        /// </summary>
        public static Vec3 GetAimDir(this SkillPipelineContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            return ctx.AimDir;
        }

        /// <summary>
        /// 获取失败原因（优先从属性读取）
        /// </summary>
        public static string GetFailReason(this SkillPipelineContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            return ctx.FailReason;
        }

        /// <summary>
        /// 设置失败原因
        /// </summary>
        public static void SetFailReason(this IAbilityPipelineContext ctx, string reason)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            ctx.SetData(AbilityContextKeys.FailReason.ToKeyString(), reason);
        }

        /// <summary>
        /// 获取施法序列号
        /// </summary>
        public static int GetCastSequence(this IAbilityPipelineContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            return ctx.GetData(AbilityContextKeys.CastSequence.ToKeyString(), 0);
        }

        /// <summary>
        /// 获取下一个施法序列号
        /// </summary>
        public static int NextCastSequence(this IAbilityPipelineContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            var current = ctx.GetData(AbilityContextKeys.CastSequence.ToKeyString(), 0);
            current++;
            ctx.SetData(AbilityContextKeys.CastSequence.ToKeyString(), current);
            return current;
        }
    }
}
