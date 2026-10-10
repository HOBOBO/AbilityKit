using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public sealed class AwardTeamKillScoreSchema : MobaPlanActionSchemaBase<AwardTeamKillScoreArgs>
    {
        public static readonly AwardTeamKillScoreSchema Instance = new AwardTeamKillScoreSchema();
        protected override string ActionName => TriggeringConstants.Actions.AwardTeamKillScore;

        public override AwardTeamKillScoreArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs,
            ExecCtx<IWorldResolver> ctx)
        {
            return new AwardTeamKillScoreArgs(
                ReadInt(namedArgs, ctx, 0, "key_base_id"),
                ReadFloat(namedArgs, ctx, 1f, "points"));
        }

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args,
            out string error)
        {
            return RequireNumericValue(args, "key_base_id", out error, "key_base_id");
        }
    }
}
