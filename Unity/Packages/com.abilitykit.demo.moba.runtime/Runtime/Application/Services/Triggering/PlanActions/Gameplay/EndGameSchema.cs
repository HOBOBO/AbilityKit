using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Demo.Moba.Systems;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public sealed class EndGameSchema : MobaPlanActionSchemaBase<EndGameArgs>
    {
        public static readonly EndGameSchema Instance = new EndGameSchema();

        protected override string ActionName => TriggeringConstants.Actions.EndGame;

        public override EndGameArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            var reasonId = ReadInt(namedArgs, ctx, 0, "reason_id", "reasonid", "id");
            var winTeamId = ReadInt(namedArgs, ctx, 0, "win_team_id", "team_id", "win_team");
            var winTeamFromEvent = ReadBool(namedArgs, ctx, false, "win_team_from_event");

            return new EndGameArgs(reasonId, winTeamId, winTeamFromEvent);
        }

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            error = null;
            return true;
        }
    }
}
