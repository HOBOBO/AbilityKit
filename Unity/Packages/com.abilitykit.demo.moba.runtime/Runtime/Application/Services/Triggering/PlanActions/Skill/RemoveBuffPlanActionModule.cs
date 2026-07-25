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

            var coreInput = MobaPlanActionInputResolver.Resolve(triggerArgs, ctx);
            var effectInput = new MobaEffectActionInput(in coreInput);
            var targets = PooledMobaPlanActionLists.GetIntList();
            try
            {
                if (!MobaActionTargetResolver.TryResolveTargets(in args.TargetRequest, in coreInput, in effectInput, ctx, ActionName, targets))
                {
                    return;
                }

                // A cleanse targets every matching slow, regardless of which actor applied it.
                var sourceActorId = args.RemoveSlow ? 0 : (args.SourceActorId > 0 ? args.SourceActorId : effectInput.CasterActorId);
                var removed = 0;
                for (var i = 0; i < targets.Count; i++)
                {
                    var targetActorId = targets[i];
                    if (targetActorId <= 0) continue;
                    removed += args.RemoveSlow
                        ? buffs.RemoveBuffsWithTagImmediate(targetActorId, "Debuff.Slow", sourceActorId, args.RemoveAll, args.Reason)
                        : buffs.RemoveBuffsImmediate(targetActorId, args.BuffId, sourceActorId, args.RemoveAll, args.Reason);
                }

                LogApplied($"buffId={args.BuffId} removeSlow={args.RemoveSlow} source={sourceActorId} targets={targets.Count} removed={removed} reason={args.Reason}");
            }
            finally
            {
                PooledMobaPlanActionLists.Release(targets);
            }
        }
    }
}
