using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Modifiers;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.AddSkillParamModifier)]
    public sealed class AddSkillParamModifierPlanActionModule :
        MobaPlanActionModuleBase<AddSkillParamModifierArgs, AddSkillParamModifierPlanActionModule>
    {
        protected override IActionSchema<AddSkillParamModifierArgs, IWorldResolver> Schema =>
            AddSkillParamModifierSchema.Instance;

        protected override void Execute(object triggerArgs, AddSkillParamModifierArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!MobaSkillParamModifierKeys.IsTriggerWritable(args.ParameterId) ||
                !MobaSkillParamModifierKeys.TryResolve(args.ParameterId, out var key))
            {
                LogRejected(ctx, $"unsupported skill parameter id: {args.ParameterId}.");
                return;
            }
            if (!args.Operation.IsBuiltin())
            {
                LogRejected(ctx, $"unsupported modifier operation: {(int)args.Operation}.");
                return;
            }
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
                        TriggeringConstants.Actions.AddSkillParamModifier, targets)) return;
                for (var i = 0; i < targets.Count; i++)
                {
                    var actorId = targets[i];
                    if (actorId <= 0) continue;
                    modifiers.AddFixed(actorId, key, args.Operation, args.Value, args.SourceId, args.Priority);
                    LogApplied(ctx, $"actorId={actorId}, parameterId={args.ParameterId}, op={args.Operation}, value={args.Value:0.###}, sourceId={args.SourceId}.");
                }
            }
            finally
            {
                PooledMobaPlanActionLists.Release(targets);
            }
        }
    }
}
