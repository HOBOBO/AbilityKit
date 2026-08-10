using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Events.Summon;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.RemoveSummon)]
    public sealed class RemoveSummonPlanActionModule : MobaPlanActionModuleBase<RemoveSummonArgs, RemoveSummonPlanActionModule>
    {
        protected override IActionSchema<RemoveSummonArgs, IWorldResolver> Schema => RemoveSummonSchema.Instance;

        protected override void Execute(object triggerArgs, RemoveSummonArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!ctx.Context.TryResolve<MobaSummonService>(out var summons) || summons == null)
            {
                LogRejected(ctx, "cannot resolve MobaSummonService.");
                return;
            }

            if (args.SummonActorId > 0)
            {
                var ok = summons.TryDespawn(args.SummonActorId, args.Reason == SummonDespawnReason.None ? SummonDespawnReason.ManualRemove : args.Reason);
                LogApplied(ctx, $"direct. actorId={args.SummonActorId} removed={ok}");
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
                if (args.RootOwnerActorId > 0)
                {
                    ownerActorIds.Add(args.RootOwnerActorId);
                }
                else if (!MobaActionTargetResolver.TryResolveTargets(in args.TargetRequest, in coreInput, in effectInput, ctx, TriggeringConstants.Actions.RemoveSummon, ownerActorIds) || ownerActorIds.Count == 0)
                {
                    if (coreInput.HasCasterActor)
                    {
                        ownerActorIds.Add(coreInput.CasterActorId);
                    }
                }

                var reason = args.Reason == SummonDespawnReason.None ? SummonDespawnReason.ManualRemove : args.Reason;
                var removed = 0;
                for (var i = 0; i < ownerActorIds.Count; i++)
                {
                    var ownerActorId = ownerActorIds[i];
                    if (ownerActorId <= 0) continue;
                    removed += summons.RemoveSummons(ownerActorId, args.SummonId, args.RemoveAll, reason);
                }

                LogApplied(ctx, $"summonId={args.SummonId} owners={ownerActorIds.Count} removed={removed} reason={reason}");
            }
            finally
            {
                PooledMobaPlanActionLists.Release(ownerActorIds);
            }
        }
    }
}
