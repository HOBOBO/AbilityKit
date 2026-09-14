using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public sealed class AddShieldSchema : MobaPlanActionSchemaBase<AddShieldArgs>
    {
        public static readonly AddShieldSchema Instance = new AddShieldSchema();

        protected override string ActionName => TriggeringConstants.Actions.AddShield;

        public override AddShieldArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            var shieldId = ReadInt(namedArgs, ctx, 0, "shield_id", "shieldid", "id");
            var value = ReadFloat(namedArgs, ctx, 0f, "shield_value", "value", "amount");
            var absorbRatio = ReadFloat(namedArgs, ctx, 1f, "absorb_ratio", "absorbratio", "ratio");
            var priority = ReadInt(namedArgs, ctx, 0, "priority");
            var damageTypeMask = ReadInt(namedArgs, ctx, 0, "damage_type_mask", "damagetypemask", "damage_mask", "damagemask");
            var durationFrames = ReadInt(namedArgs, ctx, 0, "duration_frames", "durationframes", "frames");
            var durationMs = ReadInt(namedArgs, ctx, 0, "duration_ms", "durationms", "duration", "duration_millis");
            var stackingPolicy = ReadEnum(namedArgs, ctx, ShieldStackingPolicy.Independent, "stacking_policy", "stackingpolicy");
            var consumePolicy = ReadEnum(namedArgs, ctx, ShieldConsumePolicy.PriorityThenOldest, "consume_policy", "consumepolicy");
            var targetRequest = MobaActionTargetSchemaReader.Read(namedArgs, ctx);
            var magnitude = MobaEffectMagnitudeSchemaReader.Read(namedArgs, in ctx, value);
            TryReadBlackboardTarget(namedArgs, out var resultTarget, "result", "result_id", "shield_result");
            TryReadBlackboardTarget(namedArgs, out var resultCountTarget, "result_count", "shield_count");
            return new AddShieldArgs(shieldId, value, absorbRatio, priority, damageTypeMask, durationFrames, durationMs, stackingPolicy, consumePolicy, in targetRequest, magnitude, resultTarget, resultCountTarget);
        }

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            if (MobaEffectMagnitudeSchemaReader.HasMagnitudeArgs(args)) { error = null; return true; }
            return RequireAny(args, "shield_value", out error, "shield_value", "value", "amount");
        }
    }
}
