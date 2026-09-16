using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public sealed class RemoveSkillParamModifiersSchema : MobaPlanActionSchemaBase<RemoveSkillParamModifiersArgs>
    {
        public static readonly RemoveSkillParamModifiersSchema Instance = new RemoveSkillParamModifiersSchema();

        protected override string ActionName => TriggeringConstants.Actions.RemoveSkillParamModifiers;

        public override RemoveSkillParamModifiersArgs ParseArgs(
            Dictionary<string, ActionArgValue> namedArgs,
            ExecCtx<IWorldResolver> ctx)
        {
            return ParseArgs(namedArgs, ctx, default);
        }

        public override RemoveSkillParamModifiersArgs ParseArgs(
            Dictionary<string, ActionArgValue> namedArgs,
            ExecCtx<IWorldResolver> ctx,
            in TriggerActionParseContext parseContext)
        {
            var sourceId = ReadInt(namedArgs, ctx, 0, "source_id", "source");
            var target = MobaActionTargetSchemaReader.Read(namedArgs, ctx, in parseContext);
            return new RemoveSkillParamModifiersArgs(sourceId, in target);
        }

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            return RequireAny(args, "source_id", out error, "source_id", "source");
        }
    }
}
