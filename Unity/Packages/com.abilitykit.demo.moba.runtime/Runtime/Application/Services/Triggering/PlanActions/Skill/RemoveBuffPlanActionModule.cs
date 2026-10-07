using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.RemoveBuff)]
    public sealed class RemoveBuffPlanActionModule : MobaPlanActionModuleBase<RemoveBuffArgs, RemoveBuffPlanActionModule>
    {
        protected override IActionSchema<RemoveBuffArgs, IWorldResolver> Schema => RemoveBuffSchema.Instance;

        protected override void Execute(object triggerArgs, RemoveBuffArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!TryResolveRequired(ctx, out MobaBuffService buffs))
            {
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
            string tagName = null;
            if (args.TagId > 0 &&
                (!TryResolveRequired(ctx, out IMobaTagDefinitionQueryService definitions) ||
                 !definitions.TryGetActiveTagName(args.TagId, out tagName)))
            {
                LogRejected(ctx, $"unknown active gameplay tag definition: {args.TagId}");
                return;
            }
            var targets = PooledMobaPlanActionLists.GetIntList();
            try
            {
                if (!MobaActionTargetResolver.TryResolveTargets(in args.TargetRequest, in coreInput, in effectInput, ctx, ActionName, targets))
                {
                    return;
                }

                var sourceActorId = args.SourceActorId > 0 ? args.SourceActorId :
                    args.TagId > 0 ? 0 : effectInput.CasterActorId;
                var removed = 0;
                for (var i = 0; i < targets.Count; i++)
                {
                    var targetActorId = targets[i];
                    if (targetActorId <= 0) continue;
                    removed += args.TagId > 0
                        ? buffs.RemoveBuffsWithTagImmediate(targetActorId, tagName, sourceActorId, args.RemoveAll, args.Reason)
                        : buffs.RemoveBuffsImmediate(targetActorId, args.BuffId, sourceActorId, args.RemoveAll, args.Reason);
                }

                LogApplied($"buffId={args.BuffId} tagId={args.TagId} source={sourceActorId} targets={targets.Count} removed={removed} reason={args.Reason}");
            }
            finally
            {
                PooledMobaPlanActionLists.Release(targets);
            }
        }
    }
}
