using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public sealed class LeaveSharedShieldSchema : MobaPlanActionSchemaBase<LeaveSharedShieldArgs>
    {
        public static readonly LeaveSharedShieldSchema Instance = new LeaveSharedShieldSchema();
        protected override string ActionName => TriggeringConstants.Actions.LeaveSharedShield;
        public override LeaveSharedShieldArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            var poolId = ReadInt(namedArgs, ctx, 0, "pool_id", "poolid");
            var target = MobaActionTargetSchemaReader.Read(namedArgs, ctx);
            return new LeaveSharedShieldArgs(poolId, in target);
        }
        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error) => RequireAny(args, "pool_id", out error, "pool_id", "poolid");
    }
}
