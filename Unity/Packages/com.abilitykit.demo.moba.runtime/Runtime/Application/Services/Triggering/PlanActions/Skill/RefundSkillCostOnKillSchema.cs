using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public sealed class RefundSkillCostOnKillSchema : MobaPlanActionSchemaBase<RefundSkillCostOnKillArgs>
    {
        public static readonly RefundSkillCostOnKillSchema Instance = new RefundSkillCostOnKillSchema();
        protected override string ActionName => TriggeringConstants.Actions.RefundSkillCostOnKill;
        public override RefundSkillCostOnKillArgs ParseArgs(Dictionary<string, ActionArgValue> args, ExecCtx<IWorldResolver> ctx) =>
            new RefundSkillCostOnKillArgs(ReadInt(args, ctx, 0, "skill_id", "skillid"));
        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        { error = null; return true; }
    }
}
