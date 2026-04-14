using AbilityKit.Core.Generic;
using AbilityKit.Core.Math;
using AbilityKit.Pipeline;

namespace AbilityKit.Demo.Moba.Services
{
    using AbilityKit.Ability;
    /// <summary>
    /// 上下文强类型访问扩展方法
    /// 提供基于 AbilityContextKeys 枚举的强类型数据访问
    /// </summary>
    public static class AbilityContextExtensions
    {
        // ========== 溯源相关 ==========

        /// <summary>
        /// 获取溯源上下文ID
        /// </summary>
        public static long GetSourceContextId(this IAbilityPipelineContext ctx)
        {
            return ctx.GetData(AbilityContextKeys.SourceContextId.ToKeyString(), 0L);
        }

        /// <summary>
        /// 设置溯源上下文ID
        /// </summary>
        public static void SetSourceContextId(this IAbilityPipelineContext ctx, long sourceContextId)
        {
            ctx.SetData(AbilityContextKeys.SourceContextId.ToKeyString(), sourceContextId);
        }

        /// <summary>
        /// 获取上下文类型
        /// </summary>
        public static bool TryGetContextKind(this IAbilityPipelineContext ctx, out int kind)
        {
            return ctx.TryGetData(AbilityContextKeys.ContextKind.ToKeyString(), out kind);
        }

        /// <summary>
        /// 设置上下文类型
        /// </summary>
        public static void SetContextKind(this IAbilityPipelineContext ctx, int kind)
        {
            ctx.SetData(AbilityContextKeys.ContextKind.ToKeyString(), kind);
        }

        // ========== 参与者相关 ==========

        /// <summary>
        /// 获取源角色ID
        /// </summary>
        public static int GetSourceActorId(this IAbilityPipelineContext ctx)
        {
            return ctx.GetData(AbilityContextKeys.SourceActorId.ToKeyString(), 0);
        }

        /// <summary>
        /// 获取目标角色ID
        /// </summary>
        public static int GetTargetActorId(this IAbilityPipelineContext ctx)
        {
            return ctx.GetData(AbilityContextKeys.TargetActorId.ToKeyString(), 0);
        }

        /// <summary>
        /// 设置参与者
        /// </summary>
        public static void SetParticipants(this IAbilityPipelineContext ctx, int sourceActorId, int targetActorId)
        {
            ctx.SetData(AbilityContextKeys.SourceActorId.ToKeyString(), sourceActorId);
            ctx.SetData(AbilityContextKeys.TargetActorId.ToKeyString(), targetActorId);
        }

        // ========== 技能相关 ==========

        /// <summary>
        /// 获取技能ID
        /// </summary>
        public static int GetSkillId(this IAbilityPipelineContext ctx)
        {
            return ctx.GetData(AbilityContextKeys.SkillId.ToKeyString(), 0);
        }

        /// <summary>
        /// 获取技能槽位
        /// </summary>
        public static int GetSkillSlot(this IAbilityPipelineContext ctx)
        {
            return ctx.GetData(AbilityContextKeys.SkillSlot.ToKeyString(), 0);
        }

        /// <summary>
        /// 获取技能等级
        /// </summary>
        public static int GetSkillLevel(this IAbilityPipelineContext ctx)
        {
            return ctx.GetData(AbilityContextKeys.SkillLevel.ToKeyString(), 0);
        }

        /// <summary>
        /// 设置技能信息
        /// </summary>
        public static void SetSkillInfo(this IAbilityPipelineContext ctx, int skillId, int slot, int level)
        {
            ctx.SetData(AbilityContextKeys.SkillId.ToKeyString(), skillId);
            ctx.SetData(AbilityContextKeys.SkillSlot.ToKeyString(), slot);
            ctx.SetData(AbilityContextKeys.SkillLevel.ToKeyString(), level);
        }

        /// <summary>
        /// 获取施法序列号
        /// </summary>
        public static int GetCastSequence(this IAbilityPipelineContext ctx)
        {
            return ctx.GetData(AbilityContextKeys.CastSequence.ToKeyString(), 0);
        }

        /// <summary>
        /// 设置施法序列号
        /// </summary>
        public static void SetCastSequence(this IAbilityPipelineContext ctx, int sequence)
        {
            ctx.SetData(AbilityContextKeys.CastSequence.ToKeyString(), sequence);
        }

        /// <summary>
        /// 获取目标位置
        /// </summary>
        public static Vec3 GetAimPos(this IAbilityPipelineContext ctx)
        {
            return ctx.GetData(AbilityContextKeys.AimPos.ToKeyString(), Vec3.Zero);
        }

        /// <summary>
        /// 获取目标方向
        /// </summary>
        public static Vec3 GetAimDir(this IAbilityPipelineContext ctx)
        {
            return ctx.GetData(AbilityContextKeys.AimDir.ToKeyString(), Vec3.Forward);
        }

        /// <summary>
        /// 设置目标位置和方向
        /// </summary>
        public static void SetAim(this IAbilityPipelineContext ctx, in Vec3 aimPos, in Vec3 aimDir)
        {
            ctx.SetData(AbilityContextKeys.AimPos.ToKeyString(), aimPos);
            ctx.SetData(AbilityContextKeys.AimDir.ToKeyString(), aimDir);
        }

        /// <summary>
        /// 获取失败原因
        /// </summary>
        public static string GetFailReason(this IAbilityPipelineContext ctx)
        {
            return ctx.GetData<string>(AbilityContextKeys.FailReason.ToKeyString(), null);
        }

        /// <summary>
        /// 设置失败原因
        /// </summary>
        public static void SetFailReason(this IAbilityPipelineContext ctx, string reason)
        {
            ctx.SetData(AbilityContextKeys.FailReason.ToKeyString(), reason);
        }

        // ========== Buff 相关 ==========

        /// <summary>
        /// 尝试获取BuffID
        /// </summary>
        public static bool TryGetBuffId(this IAbilityPipelineContext ctx, out int buffId)
        {
            return ctx.TryGetData(AbilityContextKeys.BuffId.ToKeyString(), out buffId);
        }

        /// <summary>
        /// 获取BuffID（不存在返回0）
        /// </summary>
        public static int GetBuffId(this IAbilityPipelineContext ctx)
        {
            return ctx.GetData(AbilityContextKeys.BuffId.ToKeyString(), 0);
        }

        /// <summary>
        /// 设置BuffID
        /// </summary>
        public static void SetBuffId(this IAbilityPipelineContext ctx, int buffId)
        {
            ctx.SetData(AbilityContextKeys.BuffId.ToKeyString(), buffId);
        }

        /// <summary>
        /// 获取Buff叠加层数
        /// </summary>
        public static int GetBuffStackCount(this IAbilityPipelineContext ctx)
        {
            return ctx.GetData(AbilityContextKeys.BuffStackCount.ToKeyString(), 0);
        }

        /// <summary>
        /// 设置Buff叠加层数
        /// </summary>
        public static void SetBuffStackCount(this IAbilityPipelineContext ctx, int stackCount)
        {
            ctx.SetData(AbilityContextKeys.BuffStackCount.ToKeyString(), stackCount);
        }

        // ========== 子弹相关 ==========

        /// <summary>
        /// 尝试获取子弹ID
        /// </summary>
        public static bool TryGetProjectileId(this IAbilityPipelineContext ctx, out int projectileId)
        {
            return ctx.TryGetData(AbilityContextKeys.ProjectileId.ToKeyString(), out projectileId);
        }

        /// <summary>
        /// 设置子弹ID
        /// </summary>
        public static void SetProjectileId(this IAbilityPipelineContext ctx, int projectileId)
        {
            ctx.SetData(AbilityContextKeys.ProjectileId.ToKeyString(), projectileId);
        }

        /// <summary>
        /// 设置子弹发射信息
        /// </summary>
        public static void SetProjectileLaunch(this IAbilityPipelineContext ctx, in Vec3 position, in Vec3 direction, float speed)
        {
            ctx.SetData(AbilityContextKeys.LaunchPosition.ToKeyString(), position);
            ctx.SetData(AbilityContextKeys.LaunchDirection.ToKeyString(), direction);
            ctx.SetData(AbilityContextKeys.ProjectileSpeed.ToKeyString(), speed);
        }

        /// <summary>
        /// 获取子弹命中触发器ID
        /// </summary>
        public static int GetHitTriggerPlanId(this IAbilityPipelineContext ctx)
        {
            return ctx.GetData(AbilityContextKeys.HitTriggerPlanId.ToKeyString(), 0);
        }

        /// <summary>
        /// 设置子弹命中触发器ID
        /// </summary>
        public static void SetHitTriggerPlanId(this IAbilityPipelineContext ctx, int triggerPlanId)
        {
            ctx.SetData(AbilityContextKeys.HitTriggerPlanId.ToKeyString(), triggerPlanId);
        }

        // ========== AOE 区域相关 ==========

        /// <summary>
        /// 尝试获取AOE区域ID
        /// </summary>
        public static bool TryGetAreaId(this IAbilityPipelineContext ctx, out int areaId)
        {
            return ctx.TryGetData(AbilityContextKeys.AreaId.ToKeyString(), out areaId);
        }

        /// <summary>
        /// 设置AOE区域ID
        /// </summary>
        public static void SetAreaId(this IAbilityPipelineContext ctx, int areaId)
        {
            ctx.SetData(AbilityContextKeys.AreaId.ToKeyString(), areaId);
        }

        /// <summary>
        /// 设置AOE区域信息
        /// </summary>
        public static void SetAreaInfo(this IAbilityPipelineContext ctx, in Vec3 center, float radius)
        {
            ctx.SetData(AbilityContextKeys.AreaCenter.ToKeyString(), center);
            ctx.SetData(AbilityContextKeys.AreaRadius.ToKeyString(), radius);
        }

        /// <summary>
        /// 获取AOE进入触发器ID
        /// </summary>
        public static int GetAreaEnterTriggerPlanId(this IAbilityPipelineContext ctx)
        {
            return ctx.GetData(AbilityContextKeys.AreaEnterTriggerPlanId.ToKeyString(), 0);
        }

        /// <summary>
        /// 设置AOE进入触发器ID
        /// </summary>
        public static void SetAreaEnterTriggerPlanId(this IAbilityPipelineContext ctx, int triggerPlanId)
        {
            ctx.SetData(AbilityContextKeys.AreaEnterTriggerPlanId.ToKeyString(), triggerPlanId);
        }

        /// <summary>
        /// 获取AOE离开触发器ID
        /// </summary>
        public static int GetAreaLeaveTriggerPlanId(this IAbilityPipelineContext ctx)
        {
            return ctx.GetData(AbilityContextKeys.AreaLeaveTriggerPlanId.ToKeyString(), 0);
        }

        /// <summary>
        /// 设置AOE离开触发器ID
        /// </summary>
        public static void SetAreaLeaveTriggerPlanId(this IAbilityPipelineContext ctx, int triggerPlanId)
        {
            ctx.SetData(AbilityContextKeys.AreaLeaveTriggerPlanId.ToKeyString(), triggerPlanId);
        }

        // ========== 命中相关 ==========

        /// <summary>
        /// 尝试获取命中位置
        /// </summary>
        public static bool TryGetHitPosition(this IAbilityPipelineContext ctx, out Vec3 hitPosition)
        {
            return ctx.TryGetData(AbilityContextKeys.HitPosition.ToKeyString(), out hitPosition);
        }

        /// <summary>
        /// 设置命中位置
        /// </summary>
        public static void SetHitPosition(this IAbilityPipelineContext ctx, in Vec3 position)
        {
            ctx.SetData(AbilityContextKeys.HitPosition.ToKeyString(), position);
        }

        /// <summary>
        /// 尝试获取命中法线
        /// </summary>
        public static bool TryGetHitNormal(this IAbilityPipelineContext ctx, out Vec3 hitNormal)
        {
            return ctx.TryGetData(AbilityContextKeys.HitNormal.ToKeyString(), out hitNormal);
        }

        /// <summary>
        /// 设置命中法线
        /// </summary>
        public static void SetHitNormal(this IAbilityPipelineContext ctx, in Vec3 normal)
        {
            ctx.SetData(AbilityContextKeys.HitNormal.ToKeyString(), normal);
        }

        // ========== 被动技能相关 ==========

        /// <summary>
        /// 尝试获取被动技能ID
        /// </summary>
        public static bool TryGetPassiveSkillId(this IAbilityPipelineContext ctx, out int passiveSkillId)
        {
            return ctx.TryGetData(AbilityContextKeys.PassiveSkillId.ToKeyString(), out passiveSkillId);
        }

        /// <summary>
        /// 设置被动技能ID
        /// </summary>
        public static void SetPassiveSkillId(this IAbilityPipelineContext ctx, int passiveSkillId)
        {
            ctx.SetData(AbilityContextKeys.PassiveSkillId.ToKeyString(), passiveSkillId);
        }

        /// <summary>
        /// 获取被动技能冷却结束时间
        /// </summary>
        public static long GetPassiveCooldownEndTimeMs(this IAbilityPipelineContext ctx)
        {
            return ctx.GetData(AbilityContextKeys.PassiveCooldownEndTimeMs.ToKeyString(), 0L);
        }

        /// <summary>
        /// 设置被动技能冷却结束时间
        /// </summary>
        public static void SetPassiveCooldownEndTimeMs(this IAbilityPipelineContext ctx, long endTimeMs)
        {
            ctx.SetData(AbilityContextKeys.PassiveCooldownEndTimeMs.ToKeyString(), endTimeMs);
        }
    }
}
