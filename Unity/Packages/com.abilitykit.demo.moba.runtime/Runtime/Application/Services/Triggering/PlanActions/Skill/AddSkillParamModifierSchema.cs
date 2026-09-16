using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Modifiers;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public sealed class AddSkillParamModifierSchema : MobaPlanActionSchemaBase<AddSkillParamModifierArgs>
    {
        public static readonly AddSkillParamModifierSchema Instance = new AddSkillParamModifierSchema();

        protected override string ActionName => TriggeringConstants.Actions.AddSkillParamModifier;

        public override AddSkillParamModifierArgs ParseArgs(
            Dictionary<string, ActionArgValue> namedArgs,
            ExecCtx<IWorldResolver> ctx)
        {
            return ParseArgs(namedArgs, ctx, default);
        }

        public override AddSkillParamModifierArgs ParseArgs(
            Dictionary<string, ActionArgValue> namedArgs,
            ExecCtx<IWorldResolver> ctx,
            in TriggerActionParseContext parseContext)
        {
            var parameterId = ReadInt(namedArgs, ctx, 0, "parameter_id", "parameter", "param_id");
            var operation = ReadEnum(namedArgs, ctx, ModifierOp.Add, "operation", "op");
            var value = ReadFloat(namedArgs, ctx, 0f, "value", "amount");
            var sourceId = ReadInt(namedArgs, ctx, 0, "source_id", "source");
            var priority = ReadInt(namedArgs, ctx, 10, "priority");
            var target = MobaActionTargetSchemaReader.Read(namedArgs, ctx, in parseContext);
            return new AddSkillParamModifierArgs(parameterId, operation, value, sourceId, priority, in target);
        }

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            if (!RequireAny(args, "parameter_id", out error, "parameter_id", "parameter", "param_id")) return false;
            if (!RequireAny(args, "value", out error, "value", "amount")) return false;
            return RequireAny(args, "source_id", out error, "source_id", "source");
        }
    }
}
