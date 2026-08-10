using System;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.ECS.Entitas;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;


namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// 消耗资源的 Plan Action 模块，在技能释放前扣除资源（蓝量、能量等）。
    /// </summary>
    [PlanActionModule(order: MobaPlanActionModuleOrders.ConsumeResource)]
    public sealed class ConsumeResourcePlanActionModule : MobaPlanActionModuleBase<ConsumeResourceArgs, ConsumeResourcePlanActionModule>
    {
        protected override IActionSchema<ConsumeResourceArgs, IWorldResolver> Schema => ConsumeResourceSchema.Instance;

        protected override void Execute(object triggerArgs, ConsumeResourceArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (args.Amount <= 0) return;

            if (!ctx.Context.TryResolve<MobaActorLookupService>(out var actors) || actors == null)
            {
                LogRejected(ctx, "MobaActorLookupService not found");
                return;
            }

            if (!MobaPlanActionInputResolver.TryResolveEffect(
                    triggerArgs,
                    ctx,
                    out var input))
            {
                LogRejected(ctx, "requires combat execution context.");
                return;
            }

            if (!input.HasCasterActor)
            {
                LogRejected(ctx, "caster actor not found");
                return;
            }

            var casterActorId = input.CasterActorId;

            if (!actors.TryGetActorEntity(casterActorId, out var entity) || entity == null)
            {
                LogRejected(ctx, $"caster unit not found, actorId={casterActorId}");
                return;
            }

            if (!entity.hasSkillLoadout)
            {
                LogRejected(ctx, $"caster has no skill loadout, actorId={casterActorId}");
                return;
            }

            if (args.ResourceType == ResourceType.None)
            {
                throw new InvalidOperationException($"[Plan] consume_resource failed: invalid resource type. actorId={casterActorId}, amount={args.Amount}");
            }

            if (!entity.hasResourceContainer || entity.resourceContainer.Value == null || entity.resourceContainer.Value.Map == null)
            {
                throw new InvalidOperationException($"[Plan] consume_resource failed: resource container not found. actorId={casterActorId}, type={args.ResourceType}, amount={args.Amount}");
            }

            var resources = entity.resourceContainer.Value.Map;
            if (!resources.TryGetValue(args.ResourceType, out var state) || state == null)
            {
                throw new InvalidOperationException($"[Plan] consume_resource failed: resource state not found. actorId={casterActorId}, type={args.ResourceType}, amount={args.Amount}");
            }

            if (state.Current < args.Amount)
            {
                throw new InvalidOperationException($"[Plan] consume_resource failed: {args.FailMessageKey}. actorId={casterActorId}, type={args.ResourceType}, amount={args.Amount}, current={state.Current}");
            }

            state.Current -= args.Amount;
            LogApplied(ctx, $"actorId={casterActorId}, type={args.ResourceType}, amount={args.Amount}, remaining={state.Current}");
        }
    }
}
