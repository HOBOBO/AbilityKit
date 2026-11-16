using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Demo.Moba.Systems;


namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public sealed class AddBuffSchema : MobaPlanActionSchemaBase<AddBuffArgs>
    {
        public static readonly AddBuffSchema Instance = new AddBuffSchema();

        protected override string ActionName => TriggeringConstants.Actions.AddBuff;

        public override AddBuffArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            return ParseArgs(namedArgs, ctx, default);
        }

        public override AddBuffArgs ParseArgs(
            Dictionary<string, ActionArgValue> namedArgs,
            ExecCtx<IWorldResolver> ctx,
            in TriggerActionParseContext parseContext)
        {
            var buffIds = ReadPositiveInts(namedArgs, ctx, "buffids", "buff_ids", "buffid", "buff_id", "id", "ids");
            // Preserve the triggering payload while resolving target_payload_field_id.
            // This is required for generic reactions such as DamageResult -> add_buff;
            // the action must derive its target from the event payload rather than from
            // a caller-specific runtime path.
            var targetRequest = MobaActionTargetSchemaReader.Read(namedArgs, ctx, in parseContext);
            return new AddBuffArgs(buffIds, in targetRequest, ReadInt(namedArgs, ctx, 0, "duration_ms"),
                MobaEffectMagnitudeSchemaReader.ReadString(namedArgs, in ctx, "duration_skill_level_parameter"));
        }

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            var hasDuration = false;
            var hasParameter = false;
            foreach (var pair in args)
            {
                if (string.Equals(pair.Key, "duration_ms", StringComparison.OrdinalIgnoreCase))
                {
                    hasDuration = true;
                    if (pair.Value.Kind != ActionArgKind.NumericValue ||
                        pair.Value.Ref.Kind != ENumericValueRefKind.Const ||
                        double.IsNaN(pair.Value.Ref.ConstValue) || double.IsInfinity(pair.Value.Ref.ConstValue) ||
                        pair.Value.Ref.ConstValue < 0 || pair.Value.Ref.ConstValue > int.MaxValue ||
                        pair.Value.Ref.ConstValue != Math.Truncate(pair.Value.Ref.ConstValue))
                    { error = "duration_ms must be a non-negative integer"; return false; }
                }
                if (string.Equals(pair.Key, "duration_skill_level_parameter", StringComparison.OrdinalIgnoreCase))
                {
                    hasParameter = true;
                    if (pair.Value.Kind != ActionArgKind.StringValue || string.IsNullOrWhiteSpace(pair.Value.StringValue))
                    { error = "duration_skill_level_parameter must be a non-empty string"; return false; }
                }
            }
            if (hasDuration && hasParameter)
            { error = "duration_ms and duration_skill_level_parameter are mutually exclusive"; return false; }
            return RequireAny(args, "buffIds", out error, "buffids", "buff_ids", "buffid", "buff_id");
        }
    }
}
