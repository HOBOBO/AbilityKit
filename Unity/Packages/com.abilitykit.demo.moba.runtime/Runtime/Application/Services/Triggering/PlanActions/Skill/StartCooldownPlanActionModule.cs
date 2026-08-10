using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.StartCooldown)]
    public sealed class StartCooldownPlanActionModule : MobaPlanActionModuleBase<StartCooldownArgs, StartCooldownPlanActionModule>
    {
        protected override IActionSchema<StartCooldownArgs, IWorldResolver> Schema => StartCooldownSchema.Instance;

        protected override void Execute(object triggerArgs, StartCooldownArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (args.CooldownMs <= 0) return;

            if (!ctx.Context.TryResolve<MobaActorLookupService>(out var actors) || actors == null)
            {
                LogRejected(ctx, "MobaActorLookupService not found");
                return;
            }

            if (!MobaPlanActionInputResolver.TryResolveEffect(
                    triggerArgs,
                    ctx,
                    out var input))
            {
                LogRejected(ctx, "requires combat execution context.");
                return;
            }

            if (!input.HasCasterActor)
            {
                LogRejected(ctx, "caster actor not found");
                return;
            }

            var skillId = args.SkillId;
            var skillSlot = args.SkillSlot;

            if (skillId <= 0 || skillSlot <= 0)
            {
                throw new InvalidOperationException($"[Plan] start_cooldown failed: invalid skill. actorId={input.CasterActorId}, skillId={skillId}, slot={skillSlot}, cooldownMs={args.CooldownMs}");
            }

            var frameTime = ResolveFrameTime(ctx, input.CasterActorId, skillId, skillSlot, args.CooldownMs);
            var now = MobaSkillRuntimeAccess.GetCurrentTimeMs(frameTime);
            var cooldownEndTimeMs = now + args.CooldownMs;
            if (!MobaSkillRuntimeAccess.TrySetActiveSkillCooldown(actors, input.CasterActorId, skillSlot, skillId, cooldownEndTimeMs, args.CooldownMs))
            {
                throw new InvalidOperationException($"[Plan] start_cooldown failed: active skill not found. actorId={input.CasterActorId}, skillId={skillId}, slot={skillSlot}, cooldownMs={args.CooldownMs}");
            }

            LogApplied(ctx, $"actorId={input.CasterActorId}, skillId={skillId}, slot={skillSlot}, cooldownMs={args.CooldownMs}, endMs={cooldownEndTimeMs}");
        }

        private static IFrameTime ResolveFrameTime(ExecCtx<IWorldResolver> ctx, int actorId, int skillId, int skillSlot, int cooldownMs)
        {
            if (ctx.Context != null && ctx.Context.TryResolve<IFrameTime>(out var frameTime) && frameTime != null)
            {
                return frameTime;
            }

            throw new InvalidOperationException($"[Plan] start_cooldown requires IFrameTime for deterministic cooldown resolution. actorId={actorId}, skillId={skillId}, slot={skillSlot}, cooldownMs={cooldownMs}");
        }
    }
}
