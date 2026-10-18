using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public sealed class JoinSharedShieldSchema : MobaPlanActionSchemaBase<JoinSharedShieldArgs>
    {
        public static readonly JoinSharedShieldSchema Instance = new JoinSharedShieldSchema();
        protected override string ActionName => TriggeringConstants.Actions.JoinSharedShield;

        public override JoinSharedShieldArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            var poolId = ReadInt(namedArgs, ctx, 0, "pool_id", "poolid");
            var weight = ReadFloat(namedArgs, ctx, 1f, "weight");
            var max = ReadFloat(namedArgs, ctx, 0f, "max_consume", "maxconsume");
            var target = MobaActionTargetSchemaReader.Read(namedArgs, ctx);
            return new JoinSharedShieldArgs(poolId, MobaResourceFixedConvert.ToFixed(weight), MobaResourceFixedConvert.ToFixed(max), in target);
        }

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            return RequireAny(args, "pool_id", out error, "pool_id", "poolid");
        }
    }
}
