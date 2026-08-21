using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public sealed class SetNumericBlackboardSchema : MobaPlanActionSchemaBase<SetNumericBlackboardArgs>
    {
        public static readonly SetNumericBlackboardSchema Instance = new SetNumericBlackboardSchema();
        protected override string ActionName => "set_num_var";

        public override SetNumericBlackboardArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            if (!TryReadBlackboardTarget(namedArgs, out var target, "target"))
                throw new InvalidOperationException("set_num_var requires a BlackboardTarget parameter named 'target'.");
            if (!TryReadNumber(namedArgs, ctx, out var value, "value"))
                throw new InvalidOperationException("set_num_var requires a numeric parameter named 'value'.");
            return new SetNumericBlackboardArgs(in target, value);
        }

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            if (!RequireBlackboardTarget(args, "target", out error, "target")) return false;
            return RequireNumericValue(args, "value", out error, "value");
        }
    }
}
