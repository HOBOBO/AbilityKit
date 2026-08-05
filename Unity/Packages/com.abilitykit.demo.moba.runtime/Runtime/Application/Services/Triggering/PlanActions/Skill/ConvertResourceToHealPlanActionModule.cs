using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.ConvertResourceToHeal)]
    public sealed class ConvertResourceToHealPlanActionModule : MobaPlanActionModuleBase<ConvertResourceToHealArgs, ConvertResourceToHealPlanActionModule>
    {
        protected override IActionSchema<ConvertResourceToHealArgs, IWorldResolver> Schema => ConvertResourceToHealSchema.Instance;

        protected override void Execute(object triggerArgs, ConvertResourceToHealArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (args.Amount <= 0f) return;
            if (args.HealRatio <= 0f) return;
            if (args.ResourceType == ResourceType.None)
            {
                LogRejected(ctx, "invalid resource type.");
                return;
            }

            if (!ctx.Context.TryResolve<MobaActorLookupService>(out var actors) || actors == null)
            {
                LogRejected(ctx, "MobaActorLookupService not found.");
                return;
            }

            if (!ctx.Context.TryResolve<MobaDamageService>(out var damage) || damage == null)
            {
                LogRejected(ctx, "MobaDamageService not found.");
                return;
            }

            ctx.Context.TryResolve<MobaCombatActivityService>(out var combatActivity);

            var coreInput = MobaPlanActionInputResolver.Resolve(triggerArgs, ctx);
            var effectInput = new MobaEffectActionInput(in coreInput);
            if (!effectInput.HasCasterActor)
            {
                LogRejected(ctx, "missing healer actor.");
                return;
            }

            var targets = PooledMobaPlanActionLists.GetIntList();
            try
            {
                if (!MobaActionTargetResolver.TryResolveTargets(in args.TargetRequest, in coreInput, in effectInput, ctx, TriggeringConstants.Actions.ConvertResourceToHeal, targets))
                {
                    return;
                }

                for (int i = 0; i < targets.Count; i++)
                {
                    ApplyToTarget(actors, damage, combatActivity, effectInput.CasterActorId, args, targets[i], ctx);
                }
            }
            finally
            {
                PooledMobaPlanActionLists.Release(targets);
            }
        }

        private static void ApplyToTarget(
            MobaActorLookupService actors,
            MobaDamageService damage,
            MobaCombatActivityService combatActivity,
            int healerActorId,
            ConvertResourceToHealArgs args,
            int targetActorId,
            ExecCtx<IWorldResolver> ctx)
        {
            if (targetActorId <= 0) return;
            if (combatActivity != null && !combatActivity.IsOutOfCombat(targetActorId, args.OutOfCombatSeconds)) return;
            if (!actors.TryGetActorEntity(targetActorId, out var entity) || entity == null) return;
            if (!entity.hasResourceContainer || entity.resourceContainer.Value == null || entity.resourceContainer.Value.Map == null) return;
            if (!entity.resourceContainer.Value.Map.TryGetValue(args.ResourceType, out var state) || state == null) return;

            var consumed = Math.Min(state.Current, args.Amount);
            if (consumed <= 0f) return;

            var requestedHeal = consumed * args.HealRatio;
            if (requestedHeal <= 0f) return;

            var healed = damage.ApplyHeal(healerActorId, targetActorId, (int)args.HealType, requestedHeal, args.ReasonKind, args.ReasonParam);
            if (healed <= 0f) return;

            state.Current -= consumed;
            if (state.Current < 0f) state.Current = 0f;
            MobaResourceAttributeContextProjector.Refresh(entity);
            MobaPlanActionDiagnostics.Applied(ctx.Context, TriggeringConstants.Actions.ConvertResourceToHeal, $"healer={healerActorId}, target={targetActorId}, type={args.ResourceType}, consumed={consumed:0.###}, healed={healed:0.###}, current={state.Current:0.###}");
        }
    }
}
