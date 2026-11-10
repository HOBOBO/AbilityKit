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
            // an Annie-specific runtime path.
            var targetRequest = MobaActionTargetSchemaReader.Read(namedArgs, ctx, in parseContext);
            return new AddBuffArgs(buffIds, in targetRequest);
        }

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            return RequireAny(args, "buffIds", out error, "buffids", "buff_ids", "buffid", "buff_id");
        }
    }
}
