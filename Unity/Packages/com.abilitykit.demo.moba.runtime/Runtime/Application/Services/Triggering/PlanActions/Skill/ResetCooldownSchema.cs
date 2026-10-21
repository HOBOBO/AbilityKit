using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public sealed class ResetCooldownSchema : MobaPlanActionSchemaBase<ResetCooldownArgs>
    {
        public static readonly ResetCooldownSchema Instance = new ResetCooldownSchema();

        protected override string ActionName => TriggeringConstants.Actions.ResetCooldown;

        public override ResetCooldownArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            return ParseArgs(namedArgs, ctx, default(TriggerActionParseContext));
        }

        public override ResetCooldownArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx, in TriggerActionParseContext parseContext)
        {
            var skillId = ReadInt(namedArgs, ctx, 0, "skill_id", "skillid", "id");
            var skillSlot = ReadInt(namedArgs, ctx, 0, "skill_slot", "skillslot", "slot");
            var reductionMs = ReadInt(namedArgs, ctx, 0, "reduction_ms", "reductionms", "reduce_ms", "reducems");
            var targetRequest = MobaActionTargetSchemaReader.Read(namedArgs, ctx, in parseContext);
            return new ResetCooldownArgs(skillId, skillSlot, reductionMs, in targetRequest);
        }

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            if (!RequireAny(args, "skill", out error, "skill_id", "skillid", "id", "skill_slot", "skillslot", "slot")) return false;
            error = null;
            return true;
        }
    }
}
