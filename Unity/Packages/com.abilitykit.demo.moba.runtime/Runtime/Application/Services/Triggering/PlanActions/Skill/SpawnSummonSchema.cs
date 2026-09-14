using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Demo.Moba.Systems;


namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public sealed class SpawnSummonSchema : MobaPlanActionSchemaBase<SpawnSummonArgs>
    {
        public static readonly SpawnSummonSchema Instance = new SpawnSummonSchema();

        protected override string ActionName => TriggeringConstants.Actions.SpawnSummon;

        public override SpawnSummonArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            var summonId = ReadInt(namedArgs, ctx, 0, "summon_id", "summonid", "id");
            var args = new SpawnSummonArgs(summonId)
            {
                PositionMode = ReadInt(namedArgs, ctx, 0, "position_mode", "positionmode", "position"),
                RotationMode = ReadInt(namedArgs, ctx, 0, "rotation_mode", "rotationmode", "rotation"),
                IntervalMs = ReadFloat(namedArgs, ctx, 0f, "interval_ms", "intervalms"),
                DurationMs = ReadFloat(namedArgs, ctx, 0f, "duration_ms", "durationms"),
                TotalCount = ReadInt(namedArgs, ctx, 0, "total_count", "totalcount", "count"),
                QueryTemplateId = ReadInt(namedArgs, ctx, 0, "query_template_id", "querytemplateid", "query_id"),
                TargetMode = ReadInt(namedArgs, ctx, 0, "target_mode", "targetmode", "target")
            };
            TryReadBlackboardTarget(namedArgs, out args.ResultTarget, "result", "result_id", "summon_result");
            TryReadBlackboardTarget(namedArgs, out args.ResultCountTarget, "result_count", "summon_count");

            return args;
        }

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            return RequireAny(args, "summon_id", out error, "summon_id", "summonid", "id");
        }
    }
}
