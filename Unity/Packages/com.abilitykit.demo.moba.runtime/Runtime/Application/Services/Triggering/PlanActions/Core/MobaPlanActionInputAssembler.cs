using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Pipeline;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// 动作执行输入的集中组装器。
    /// 用于把执行上下文、trace scope、参与者、瞄准、目标和来源等前置数据从各个计划动作模块中剥离出来。
    /// </summary>
    internal static class MobaPlanActionInputAssembler
    {
        public static MobaPlanActionInput Assemble(object triggerArgs, ExecCtx<IWorldResolver> ctx, in MobaCombatExecutionContext executionContext)
        {
            if (!MobaPlanActionExecutionContextResolver.TryResolveTraceScope(ctx, out var traceScope))
            {
                var payloadType = triggerArgs != null ? triggerArgs.GetType().FullName : "null";
                throw new System.InvalidOperationException($"[MobaPlanActionInputAssembler] Missing formal effect trace scope. payloadType={payloadType}. Plan actions must execute inside MobaEffectExecutionService with an active effect trace scope.");
            }

            var casterActorId = executionContext.SourceActorId;
            var targetActorId = executionContext.TargetActorId;
            if (TryResolvePayloadTargetActorId(triggerArgs, out var payloadTargetActorId)
                || TryResolvePayloadTargetActorId(executionContext.Payload, out payloadTargetActorId))
            {
                targetActorId = payloadTargetActorId;
            }

            var aimPosition = Vec3.Zero;
            var aimDirection = Vec3.Zero;
            var hasAimPosition = false;
            var hasAimDirection = false;

            if (TryResolveAim(triggerArgs, out var payloadAimPosition, out var payloadAimDirection))
            {
                aimPosition = payloadAimPosition;
                aimDirection = payloadAimDirection;
                hasAimPosition = aimPosition.SqrMagnitude > 0f;
                hasAimDirection = aimDirection.SqrMagnitude > 0f;
            }
            else if (TryResolveAim(executionContext.Payload, out payloadAimPosition, out payloadAimDirection))
            {
                aimPosition = payloadAimPosition;
                aimDirection = payloadAimDirection;
                hasAimPosition = aimPosition.SqrMagnitude > 0f;
                hasAimDirection = aimDirection.SqrMagnitude > 0f;
            }

            return new MobaPlanActionInput(
                executionContext,
                traceScope,
                casterActorId,
                targetActorId,
                aimPosition,
                aimDirection,
                hasAimPosition,
                hasAimDirection);
        }

        private static bool TryResolvePayloadTargetActorId(object payload, out int actorId)
        {
            actorId = 0;
            return payload is IMobaActorContextProvider actorContext
                   && actorContext.TryGetTargetActorId(out actorId)
                   && actorId > 0;
        }

        private static bool TryResolveAim(object payload, out Vec3 aimPosition, out Vec3 aimDirection)
        {
            aimPosition = Vec3.Zero;
            aimDirection = Vec3.Zero;

            if (payload is SkillPipelineContext skillContext)
            {
                aimPosition = skillContext.AimPos;
                aimDirection = skillContext.AimDir;
                return HasAim(in aimPosition, in aimDirection);
            }

            if (payload is IEffectContext effectContext && effectContext.TryGetSkill(out var skill))
            {
                aimPosition = skill.AimPos;
                aimDirection = skill.AimDir;
                return HasAim(in aimPosition, in aimDirection);
            }

            if (payload is IAbilityPipelineContext abilityContext)
            {
                var hasAimPosition = abilityContext.TryGetData(AbilityContextKeys.AimPos.ToKeyString(), out aimPosition)
                                     && aimPosition.SqrMagnitude > 0f;
                if (!hasAimPosition && abilityContext.TryGetData(AbilityContextKeys.AreaCenter.ToKeyString(), out aimPosition))
                {
                    hasAimPosition = aimPosition.SqrMagnitude > 0f;
                }

                var hasAimDirection = abilityContext.TryGetData(AbilityContextKeys.AimDir.ToKeyString(), out aimDirection)
                                      && aimDirection.SqrMagnitude > 0f;
                return hasAimPosition || hasAimDirection;
            }

            if (payload is ProjectileEventArgs projectileEvent)
            {
                aimPosition = projectileEvent.Position;
                aimDirection = projectileEvent.Direction;
                return HasAim(in aimPosition, in aimDirection);
            }

            if (payload is AreaEventArgs areaEvent)
            {
                aimPosition = areaEvent.Center;
                return aimPosition.SqrMagnitude > 0f;
            }

            return false;
        }

        private static bool HasAim(in Vec3 aimPosition, in Vec3 aimDirection)
        {
            return aimPosition.SqrMagnitude > 0f || aimDirection.SqrMagnitude > 0f;
        }

        public static MobaEffectActionInput AssembleEffect(in MobaPlanActionInput core)
        {
            return new MobaEffectActionInput(in core);
        }

        public static MobaProjectileActionInput AssembleProjectile(in MobaPlanActionInput core)
        {
            var effect = AssembleEffect(in core);
            return new MobaProjectileActionInput(in effect, in core);
        }

        public static MobaSummonActionInput AssembleSummon(in MobaPlanActionInput core, ExecCtx<IWorldResolver> ctx)
        {
            var effect = AssembleEffect(in core);
            ctx.Context.TryResolve<MobaActorLookupService>(out var actors);
            return new MobaSummonActionInput(in effect, in core, actors);
        }

        public static bool TryResolveTargets(
            in MobaActionTargetRequest request,
            in MobaPlanActionInput coreInput,
            in MobaEffectActionInput effectInput,
            ExecCtx<IWorldResolver> ctx,
            string actionName,
            System.Collections.Generic.List<int> results)
        {
            return MobaActionTargetResolver.TryResolveTargets(in request, in coreInput, in effectInput, ctx, actionName, results);
        }
    }
}
