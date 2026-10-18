using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public sealed class TransferShieldSchema : MobaPlanActionSchemaBase<TransferShieldArgs>
    {
        public static readonly TransferShieldSchema Instance = new TransferShieldSchema();
        protected override string ActionName => TriggeringConstants.Actions.TransferShield;
        public override TransferShieldArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            var instanceId = ReadInt(namedArgs, ctx, 0, "instance_id", "instanceid", "shield_instance");
            var source = ReadInt(namedArgs, ctx, 0, "source_actor_id", "source", "from_actor");
            var policy = ReadEnum(namedArgs, ctx, ShieldTransferPolicy.MoveRemaining, "transfer_policy", "policy");
            var ratio = ReadFloat(namedArgs, ctx, 1f, "transfer_ratio", "ratio");
            var target = MobaActionTargetSchemaReader.Read(namedArgs, ctx);
            return new TransferShieldArgs(instanceId, source, policy, MobaResourceFixedConvert.ToFixed(ratio), in target);
        }
        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
            => RequireAny(args, "instance_id", out error, "instance_id", "instanceid", "shield_instance");
    }
}
