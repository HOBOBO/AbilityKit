using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public sealed class RemoveSharedShieldSchema : MobaPlanActionSchemaBase<RemoveSharedShieldArgs>
    {
        public static readonly RemoveSharedShieldSchema Instance = new RemoveSharedShieldSchema();
        protected override string ActionName => TriggeringConstants.Actions.RemoveSharedShield;
        public override RemoveSharedShieldArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
            => new RemoveSharedShieldArgs(ReadInt(namedArgs, ctx, 0, "pool_id", "poolid"));
        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
            => RequireAny(args, "pool_id", out error, "pool_id", "poolid");
    }
}
