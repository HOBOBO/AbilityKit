using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public sealed class ScheduleHeroRespawnSchema : MobaPlanActionSchemaBase<ScheduleHeroRespawnArgs>
    {
        public static readonly ScheduleHeroRespawnSchema Instance = new ScheduleHeroRespawnSchema();
        protected override string ActionName => TriggeringConstants.Actions.ScheduleHeroRespawn;

        public override ScheduleHeroRespawnArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs,
            ExecCtx<IWorldResolver> ctx) => new ScheduleHeroRespawnArgs(
            ReadInt(namedArgs, ctx, 5000, "delay_ms"),
            ReadInt(namedArgs, ctx, 0, "spawn_index"),
            ReadFloat(namedArgs, ctx, 1f, "health_ratio"),
            ReadInt(namedArgs, ctx, 0, "max_respawns"));

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args,
            out string error) => RequireNumericValue(args, "delay_ms", out error, "delay_ms");
    }
}
