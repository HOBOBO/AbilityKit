using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public sealed class RemoveBuffSchema : MobaPlanActionSchemaBase<RemoveBuffArgs>
    {
        public static readonly RemoveBuffSchema Instance = new RemoveBuffSchema();

        protected override string ActionName => TriggeringConstants.Actions.RemoveBuff;

        public override RemoveBuffArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            var buffId = ReadInt(namedArgs, ctx, 0, "buff_id", "buffid", "id");
            var sourceActorId = ReadInt(namedArgs, ctx, 0, "source_actor_id", "sourceactorid", "source_id", "sourceid");
            var removeAll = ReadBool(namedArgs, ctx, true, "remove_all", "removeall", "all", "clear");
            var removeSlow = ReadBoolNonZero(namedArgs, ctx, false, "remove_slow", "removeslow");
            var reason = ReadEnum(namedArgs, ctx, MobaExecutionEndReason.Dispelled, "reason", "remove_reason", "removereason");
            var targetRequest = MobaActionTargetSchemaReader.Read(namedArgs, ctx);
            return new RemoveBuffArgs(buffId, sourceActorId, removeAll, removeSlow, reason, in targetRequest);
        }

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            error = null;
            return true;
        }
    }
}
