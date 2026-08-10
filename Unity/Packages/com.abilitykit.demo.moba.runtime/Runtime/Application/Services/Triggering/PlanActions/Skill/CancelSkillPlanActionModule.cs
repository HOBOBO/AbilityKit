using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.CancelSkill)]
    public sealed class CancelSkillPlanActionModule : MobaPlanActionModuleBase<CancelSkillArgs, CancelSkillPlanActionModule>
    {
        protected override IActionSchema<CancelSkillArgs, IWorldResolver> Schema => CancelSkillSchema.Instance;

        protected override void Execute(object triggerArgs, CancelSkillArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (!ctx.Context.TryResolve<SkillCastCoordinator>(out var skills) || skills == null)
            {
                LogRejected(ctx, "cannot resolve SkillCastCoordinator.");
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
            var targets = PooledMobaPlanActionLists.GetIntList();
            try
            {
                if (!MobaActionTargetResolver.TryResolveTargets(in args.TargetRequest, in coreInput, in effectInput, ctx, TriggeringConstants.Actions.CancelSkill, targets))
                {
                    if (effectInput.HasCasterActor) targets.Add(effectInput.CasterActorId);
                }

                var cancelled = 0;
                for (var i = 0; i < targets.Count; i++)
                {
                    var actorId = targets[i];
                    if (actorId <= 0) continue;
                    if (Cancel(skills, actorId, args)) cancelled++;
                    if (cancelled > 0 && !args.RemoveAll) break;
                }

                LogApplied(ctx, $"targets={targets.Count} cancelled={cancelled} mode={args.Mode} skillId={args.SkillId} slot={args.SkillSlot}");
            }
            finally
            {
                PooledMobaPlanActionLists.Release(targets);
            }
        }

        private static bool Cancel(SkillCastCoordinator skills, int actorId, CancelSkillArgs args)
        {
            var mode = args.Mode;
            if (mode == CancelSkillMode.Auto)
            {
                if (args.SkillSlot > 0) mode = CancelSkillMode.Slot;
                else if (args.SkillId > 0) mode = CancelSkillMode.SkillId;
                else mode = CancelSkillMode.All;
            }

            switch (mode)
            {
                case CancelSkillMode.Slot:
                    return skills.CancelBySlot(actorId, args.SkillSlot);
                case CancelSkillMode.SkillId:
                    if (args.SkillId <= 0) return false;
                    skills.CancelBySkillId(actorId, args.SkillId);
                    return true;
                case CancelSkillMode.All:
                default:
                    skills.CancelAll(actorId);
                    return true;
            }
        }
    }
}

