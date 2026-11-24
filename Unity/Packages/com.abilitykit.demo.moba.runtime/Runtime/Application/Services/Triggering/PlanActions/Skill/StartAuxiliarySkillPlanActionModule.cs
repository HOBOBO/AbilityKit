using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// 以当前逻辑动作的施法者为所有者启动无槽位的辅助技能。
    /// </summary>
    [PlanActionModule(order: MobaPlanActionModuleOrders.StartAuxiliarySkill)]
    public sealed class StartAuxiliarySkillPlanActionModule : MobaPlanActionModuleBase<int, StartAuxiliarySkillPlanActionModule>
    {
        protected override IActionSchema<int, IWorldResolver> Schema => AuxiliarySkillSchema.Instance;

        protected override void Execute(object triggerArgs, int skillId, ExecCtx<IWorldResolver> ctx)
        {
            if (skillId <= 0 || !MobaPlanActionInputResolver.TryResolve(triggerArgs, ctx, out var input) ||
                !input.HasCasterActor || !ctx.Context.TryResolve<SkillCastCoordinator>(out var skills))
            {
                LogRejected(ctx, "requires a skill ID, caster and SkillCastCoordinator.");
                return;
            }

            var result = skills.TryCastAuxiliarySkill(input.CasterActorId, skillId);
            if (!result.Success) LogRejected(ctx, result.FailReason);
        }

        private sealed class AuxiliarySkillSchema : MobaPlanActionSchemaBase<int>
        {
            internal static readonly AuxiliarySkillSchema Instance = new AuxiliarySkillSchema();

            protected override string ActionName => TriggeringConstants.Actions.StartAuxiliarySkill;

            public override int ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
                => ReadInt(namedArgs, ctx, 0, "skill_id");

            public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
            {
                foreach (var arg in args)
                {
                    if (arg.Key != "skill_id") continue;
                    error = null;
                    return true;
                }

                error = "skill_id is required.";
                return false;
            }
        }
    }
}
