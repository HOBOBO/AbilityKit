using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.ModifyResource)]
    public sealed class ModifyResourcePlanActionModule : MobaPlanActionModuleBase<ModifyResourceArgs, ModifyResourcePlanActionModule>
    {
        protected override IActionSchema<ModifyResourceArgs, IWorldResolver> Schema => ModifyResourceSchema.Instance;

        protected override void Execute(object triggerArgs, ModifyResourceArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (Math.Abs(args.Amount) <= float.Epsilon) return;
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

            var coreInput = MobaPlanActionInputResolver.Resolve(triggerArgs, ctx);
            var effectInput = new MobaEffectActionInput(in coreInput);
            var targets = PooledMobaPlanActionLists.GetIntList();
            try
            {
                if (!MobaActionTargetResolver.TryResolveTargets(in args.TargetRequest, in coreInput, in effectInput, ctx, TriggeringConstants.Actions.ModifyResource, targets))
                {
                    return;
                }

                for (int i = 0; i < targets.Count; i++)
                {
                    ApplyToTarget(actors, args, targets[i], ctx);
                }
            }
            finally
            {
                PooledMobaPlanActionLists.Release(targets);
            }
        }

        private static void ApplyToTarget(MobaActorLookupService actors, ModifyResourceArgs args, int actorId, ExecCtx<IWorldResolver> ctx)
        {
            if (actorId <= 0) return;
            if (!actors.TryGetActorEntity(actorId, out var entity) || entity == null) return;
            if (!entity.hasResourceContainer || entity.resourceContainer.Value == null || entity.resourceContainer.Value.Map == null) return;
            if (!entity.resourceContainer.Value.Map.TryGetValue(args.ResourceType, out var state) || state == null) return;

            var next = state.Current + args.Amount;
            if (args.HasMinValue && next < args.MinValue) next = args.MinValue;
            if (args.HasMaxValue && next > args.MaxValue) next = args.MaxValue;
            else if (state.LastMax > 0f && next > state.LastMax) next = state.LastMax;

            state.Current = next;
            MobaResourceAttributeContextProjector.Refresh(entity);
            MobaPlanActionDiagnostics.Applied(ctx.Context, TriggeringConstants.Actions.ModifyResource, $"actorId={actorId}, type={args.ResourceType}, amount={args.Amount:0.###}, current={state.Current:0.###}");
        }
    }
}
