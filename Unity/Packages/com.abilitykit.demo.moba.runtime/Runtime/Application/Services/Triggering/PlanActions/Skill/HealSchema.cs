using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public sealed class HealSchema : MobaPlanActionSchemaBase<HealArgs>
    {
        public static readonly HealSchema Instance = new HealSchema();

        protected override string ActionName => TriggeringConstants.Actions.Heal;

        public override HealArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            var amount = ReadFloat(namedArgs, ctx, 0f, "amount", "heal_amount", "value");
            var healType = ReadEnum(namedArgs, ctx, DamageType.None, "heal_type", "healtype", "type");
            var reasonKind = ReadInt(namedArgs, ctx, (int)DamageReasonKind.Buff, "reason_kind", "reasonkind");
            var reasonParam = ReadInt(namedArgs, ctx, 0, "reason_param", "reasonparam");
            var targetRequest = MobaActionTargetSchemaReader.Read(namedArgs, ctx);
            var magnitude = MobaEffectMagnitudeSchemaReader.Read(namedArgs, in ctx, amount);
            return new HealArgs(amount, healType, reasonKind, reasonParam, in targetRequest, magnitude);
        }

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            if (MobaEffectMagnitudeSchemaReader.HasMagnitudeArgs(args)) { error = null; return true; }
            return RequireAny(args, "amount", out error, "amount", "heal_amount", "value");
        }
    }
}
