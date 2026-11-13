using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Variables.Numeric;

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
            if (namedArgs.ContainsKey("reduction_ratio") && (namedArgs.ContainsKey("reduction_ms") || namedArgs.ContainsKey("reductionms") || namedArgs.ContainsKey("reduce_ms") || namedArgs.ContainsKey("reducems")))
                throw new InvalidOperationException("reduction_ratio and reduction_ms are mutually exclusive");
            double? ratio = null;
            if (namedArgs.TryGetValue("reduction_ratio", out var ratioArg))
            {
                if (ratioArg.Kind != ActionArgKind.NumericValue || ratioArg.Ref.Kind != ENumericValueRefKind.Const)
                    throw new InvalidOperationException("reduction_ratio requires a constant number");
                ratio = ratioArg.Ref.ConstValue;
            }
            return new ResetCooldownArgs(skillId, skillSlot, reductionMs, in targetRequest, ratio);
        }

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            if (!RequireAny(args, "skill", out error, "skill_id", "skillid", "id", "skill_slot", "skillslot", "slot")) return false;
            if (HasAny(args, "reduction_ratio"))
            {
                if (HasAny(args, "reduction_ms", "reductionms", "reduce_ms", "reducems"))
                { error = "reduction_ratio and reduction_ms are mutually exclusive"; return false; }
                foreach (var pair in args)
                    if (pair.Key == "reduction_ratio" && (pair.Value.Kind != ActionArgKind.NumericValue || pair.Value.Ref.Kind != ENumericValueRefKind.Const ||
                        double.IsNaN(pair.Value.Ref.ConstValue) || double.IsInfinity(pair.Value.Ref.ConstValue) || pair.Value.Ref.ConstValue < 0 || pair.Value.Ref.ConstValue > 1))
                    { error = "reduction_ratio must be a constant in [0,1]"; return false; }
            }
            error = null;
            return true;
        }
    }
}
