using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.RemoveSkillParamModifiers)]
    public sealed class RemoveSkillParamModifiersPlanActionModule :
        MobaPlanActionModuleBase<RemoveSkillParamModifiersArgs, RemoveSkillParamModifiersPlanActionModule>
    {
        protected override IActionSchema<RemoveSkillParamModifiersArgs, IWorldResolver> Schema =>
            RemoveSkillParamModifiersSchema.Instance;

        protected override void Execute(object triggerArgs, RemoveSkillParamModifiersArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (args.SourceId == 0)
            {
                LogRejected(ctx, "source_id must be non-zero.");
                return;
            }
            if (!TryResolveRequired(ctx, out MobaSkillParamModifierService modifiers)) return;
            if (!MobaPlanActionInputResolver.TryResolve(triggerArgs, ctx, out var coreInput))
            {
                LogRejected(ctx, "requires combat execution context.");
                return;
            }

            var effectInput = new MobaEffectActionInput(in coreInput);
            var targetRequest = args.TargetRequest;
            var targets = PooledMobaPlanActionLists.GetIntList();
            try
            {
                if (!MobaActionTargetResolver.TryResolveTargets(
                        in targetRequest, in coreInput, in effectInput, ctx,
                        TriggeringConstants.Actions.RemoveSkillParamModifiers, targets)) return;
                for (var i = 0; i < targets.Count; i++)
                {
                    var actorId = targets[i];
                    if (actorId <= 0) continue;
                    modifiers.ClearSource(actorId, args.SourceId);
                    LogApplied(ctx, $"actorId={actorId}, sourceId={args.SourceId}.");
                }
            }
            finally
            {
                PooledMobaPlanActionLists.Release(targets);
            }
        }
    }
}
