using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public sealed class SetBlackboardVariableSchema : MobaPlanActionSchemaBase<SetBlackboardVariableArgs>
    {
        public static readonly SetBlackboardVariableSchema Instance = new SetBlackboardVariableSchema();
        protected override string ActionName => "set_var";

        public override SetBlackboardVariableArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            if (!TryReadBlackboardTarget(namedArgs, out var target, "target"))
                throw new InvalidOperationException("set_var requires a BlackboardTarget parameter named 'target'.");
            if (!TryReadTypedValue(namedArgs, "value", out var value))
                throw new InvalidOperationException("set_var requires a numeric, Boolean, or String value.");
            return new SetBlackboardVariableArgs(in target, in value);
        }

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            if (!RequireBlackboardTarget(args, "target", out error, "target")) return false;
            foreach (var pair in args)
            {
                if (!string.Equals(pair.Key, "value", StringComparison.OrdinalIgnoreCase)) continue;
                if (pair.Value.Kind == ActionArgKind.NumericValue ||
                    pair.Value.Kind == ActionArgKind.BooleanValue ||
                    pair.Value.Kind == ActionArgKind.StringValue)
                {
                    error = null;
                    return true;
                }
                error = "set_var value must be numeric, Boolean, or String.";
                return false;
            }
            error = "set_var is missing required parameter 'value'.";
            return false;
        }

        private static bool TryReadTypedValue(
            Dictionary<string, ActionArgValue> namedArgs,
            string name,
            out ActionArgValue value)
        {
            value = default;
            if (namedArgs == null) return false;
            foreach (var pair in namedArgs)
            {
                if (!string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)) continue;
                if (pair.Value.Kind != ActionArgKind.NumericValue &&
                    pair.Value.Kind != ActionArgKind.BooleanValue &&
                    pair.Value.Kind != ActionArgKind.StringValue) return false;
                value = pair.Value;
                return true;
            }
            return false;
        }
    }
}
