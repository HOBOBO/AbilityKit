using System.Collections.Generic;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Demo.Moba.Systems;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.AddBuff)]
    public sealed class AddBuffPlanActionModule : MobaPlanActionModuleBase<AddBuffArgs, AddBuffPlanActionModule>
    {
        protected override IActionSchema<AddBuffArgs, IWorldResolver> Schema => AddBuffSchema.Instance;

        protected override void Execute(object triggerArgs, AddBuffArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!TryResolveRequired(ctx, out MobaBuffService buffSvc))
            {
                return;
            }

            if (args.BuffIds == null || args.BuffIds.Length == 0)
            {
                LogRejected("requires buffIds");
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
            var sourceActorId = effectInput.CasterActorId;

            var targets = PooledMobaPlanActionLists.GetIntList();
            try
            {
                if (!MobaActionTargetResolver.TryResolveTargets(in args.TargetRequest, in coreInput, in effectInput, ctx, ActionName, targets))
                {
                    return;
                }

                for (int i = 0; i < targets.Count; i++)
                {
                    ApplyBuffs(buffSvc, args, effectInput, sourceActorId, targets[i]);
                }
            }
            finally
            {
                PooledMobaPlanActionLists.Release(targets);
            }
        }

        private static void ApplyBuffs(MobaBuffService buffSvc, AddBuffArgs args, MobaEffectActionInput input, int sourceActorId, int targetActorId)
        {
            if (targetActorId <= 0) return;

            var origin = input.BuildOrigin(sourceActorId, targetActorId, MobaTraceKind.EffectExecution, 0);
            for (int i = 0; i < args.BuffIds.Length; i++)
            {
                var buffId = args.BuffIds[i];
                if (buffId <= 0) continue;
                var buffOrigin = BuffOriginContext.FromOrigin(in origin);
                buffSvc.ApplyBuffImmediate(targetActorId, buffId, sourceActorId, durationOverrideMs: 0, buffOrigin);
            }
        }
    }
}
