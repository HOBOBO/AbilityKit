using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.RemoveArea)]
    public sealed class RemoveAreaPlanActionModule : MobaPlanActionModuleBase<RemoveAreaArgs, RemoveAreaPlanActionModule>
    {
        protected override IActionSchema<RemoveAreaArgs, IWorldResolver> Schema => RemoveAreaSchema.Instance;

        protected override void Execute(object triggerArgs, RemoveAreaArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!ctx.Context.TryResolve<MobaAreaRuntimeService>(out var areas) || areas == null)
            {
                LogRejected(ctx, "cannot resolve MobaAreaRuntimeService.");
                return;
            }

            if (args.AreaId > 0)
            {
                var ok = areas.DespawnArea(args.AreaId);
                LogApplied(ctx, $"direct. areaId={args.AreaId} removed={ok}");
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
            var ownerActorIds = PooledMobaPlanActionLists.GetIntList();
            try
            {
                if (args.OwnerActorId > 0)
                {
                    ownerActorIds.Add(args.OwnerActorId);
                }
                else if (!MobaActionTargetResolver.TryResolveTargets(in args.TargetRequest, in coreInput, in effectInput, ctx, TriggeringConstants.Actions.RemoveArea, ownerActorIds) || ownerActorIds.Count == 0)
                {
                    if (coreInput.HasCasterActor)
                    {
                        ownerActorIds.Add(coreInput.CasterActorId);
                    }
                }

                var removed = 0;
                if (ownerActorIds.Count > 0)
                {
                    for (var i = 0; i < ownerActorIds.Count; i++)
                    {
                        removed += areas.DespawnAreas(ownerActorIds[i], args.TemplateId, args.RemoveAll);
                        if (removed > 0 && !args.RemoveAll) break;
                    }
                }
                else
                {
                    removed = areas.DespawnAreas(0, args.TemplateId, args.RemoveAll);
                }

                LogApplied(ctx, $"templateId={args.TemplateId} owners={ownerActorIds.Count} removed={removed}");
            }
            finally
            {
                PooledMobaPlanActionLists.Release(ownerActorIds);
            }
        }
    }
}
