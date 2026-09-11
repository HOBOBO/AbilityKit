using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Collections;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.QueryTargetCollection)]
    public sealed class QueryTargetCollectionPlanActionModule :
        MobaPlanActionModuleBase<QueryTargetCollectionArgs, QueryTargetCollectionPlanActionModule>
    {
        private const string ActorElementTypeId = "moba.actor";

        protected override IActionSchema<QueryTargetCollectionArgs, IWorldResolver> Schema =>
            QueryTargetCollectionSchema.Instance;

        protected override void Execute(
            object triggerArgs,
            QueryTargetCollectionArgs args,
            ExecCtx<IWorldResolver> ctx)
        {
            if (!(ctx.Collections is IMutableTriggerCollectionResolver collections))
            {
                LogRejected(ctx, "requires a mutable trigger collection resolver.");
                return;
            }
            if (!MobaPlanActionInputResolver.TryResolve(triggerArgs, ctx, out var coreInput))
            {
                LogRejected(ctx, "requires combat execution context.");
                return;
            }

            var effectInput = new MobaEffectActionInput(in coreInput);
            var targetRequest = args.TargetRequest;
            var resultTarget = args.ResultTarget;
            var targets = PooledMobaPlanActionLists.GetIntList();
            try
            {
                if (!MobaActionTargetResolver.TryResolveTargetCollection(
                        in targetRequest,
                        in coreInput,
                        in effectInput,
                        ctx,
                        ActionName,
                        targets))
                    return;

                var handle = collections.Register(
                    TriggerCollection.FromInt32(targets, ActorElementTypeId));
                if (!TriggerActionOutput.TryWrite(
                        in ctx,
                        in resultTarget,
                        handle.Value,
                        out var writeError))
                {
                    collections.Release(handle);
                    LogRejected(ctx, writeError);
                    return;
                }

                LogApplied(ctx, $"registered target collection handle={handle.Value}, count={targets.Count}.");
            }
            finally
            {
                PooledMobaPlanActionLists.Release(targets);
            }
        }
    }
}
