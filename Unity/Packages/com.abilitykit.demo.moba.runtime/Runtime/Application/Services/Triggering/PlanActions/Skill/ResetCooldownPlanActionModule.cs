using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.ResetCooldown)]
    public sealed class ResetCooldownPlanActionModule : MobaPlanActionModuleBase<ResetCooldownArgs, ResetCooldownPlanActionModule>
    {
        protected override IActionSchema<ResetCooldownArgs, IWorldResolver> Schema => ResetCooldownSchema.Instance;

        protected override void Execute(object triggerArgs, ResetCooldownArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!ctx.Context.TryResolve<MobaActorLookupService>(out var actors) || actors == null)
            {
                LogRejected(ctx, "MobaActorLookupService not found");
                return;
            }

            if (!MobaPlanActionInputResolver.TryResolve(
                    triggerArgs,
                    ctx,
                    out var coreInput))
            {
                LogRejected(ctx, "requires combat execution context.");
                return;
            }

            var effectInput = new MobaEffectActionInput(in coreInput);
            if (!effectInput.HasCasterActor)
            {
                LogRejected(ctx, "caster actor not found");
                return;
            }

            var skillId = args.SkillId;
            var skillSlot = args.SkillSlot;
            if (skillId <= 0 || skillSlot <= 0)
            {
                throw new InvalidOperationException($"[Plan] reset_cooldown failed: invalid skill. actorId={effectInput.CasterActorId}, skillId={skillId}, slot={skillSlot}");
            }

            var targets = PooledMobaPlanActionLists.GetIntList();
            try
            {
                if (!MobaActionTargetResolver.TryResolveTargets(in args.TargetRequest, in coreInput, in effectInput, ctx, TriggeringConstants.Actions.ResetCooldown, targets) || targets.Count == 0)
                {
                    LogRejected(ctx, $"no matching targets. actorId={effectInput.CasterActorId}, skillId={skillId}, slot={skillSlot}");
                    return;
                }

                if (!MobaSkillRuntimeAccess.TryGetActiveSkill(actors, effectInput.CasterActorId, skillSlot, skillId, out var runtime))
                {
                    throw new InvalidOperationException($"[Plan] reset_cooldown failed: active skill not found. actorId={effectInput.CasterActorId}, skillId={skillId}, slot={skillSlot}");
                }

                if (args.ReductionMs <= 0)
                {
                    if (!MobaSkillRuntimeAccess.TrySetActiveSkillCooldown(actors, effectInput.CasterActorId, skillSlot, skillId, 0L, 0))
                    {
                        throw new InvalidOperationException($"[Plan] reset_cooldown failed to clear active skill. actorId={effectInput.CasterActorId}, skillId={skillId}, slot={skillSlot}");
                    }

                    LogApplied(ctx, $"actorId={effectInput.CasterActorId}, skillId={skillId}, slot={skillSlot}, mode=reset, matchedTargets={targets.Count}");
                    return;
                }

                if (!ctx.Context.TryResolve<IFrameTime>(out var frameTime) || frameTime == null)
                {
                    throw new InvalidOperationException($"[Plan] reset_cooldown reduction requires IFrameTime. actorId={effectInput.CasterActorId}, skillId={skillId}, slot={skillSlot}");
                }

                var nowMs = MobaSkillRuntimeAccess.GetCurrentTimeMs(frameTime);
                if (runtime.CooldownEndTimeMs <= nowMs)
                {
                    LogApplied(ctx, $"actorId={effectInput.CasterActorId}, skillId={skillId}, slot={skillSlot}, mode=reduce, reductionMs={args.ReductionMs}, alreadyReady=true, matchedTargets={targets.Count}");
                    return;
                }

                var reducedEndTimeMs = Math.Max(nowMs, runtime.CooldownEndTimeMs - args.ReductionMs);
                var reducedDurationMs = Math.Max(0, runtime.CooldownDurationMs - args.ReductionMs);
                if (!MobaSkillRuntimeAccess.TrySetActiveSkillCooldown(actors, effectInput.CasterActorId, skillSlot, skillId, reducedEndTimeMs, reducedDurationMs))
                {
                    throw new InvalidOperationException($"[Plan] reset_cooldown failed to reduce active skill. actorId={effectInput.CasterActorId}, skillId={skillId}, slot={skillSlot}, reductionMs={args.ReductionMs}");
                }

                LogApplied(ctx, $"actorId={effectInput.CasterActorId}, skillId={skillId}, slot={skillSlot}, mode=reduce, reductionMs={args.ReductionMs}, endMs={reducedEndTimeMs}, matchedTargets={targets.Count}");
            }
            finally
            {
                PooledMobaPlanActionLists.Release(targets);
            }
        }
    }
}
