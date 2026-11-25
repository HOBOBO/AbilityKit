using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Demo.Moba.Systems;
using Newtonsoft.Json.Linq;
using AbilityKit.Demo.Moba.Services.Combat.Magnitude;


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
                DurationSkillLevelParameter = MobaEffectMagnitudeSchemaReader.ReadString(namedArgs, in ctx, "duration_skill_level_parameter"),
                BirthAttributes = ReadBirthAttributes(MobaEffectMagnitudeSchemaReader.ReadString(namedArgs, in ctx, "birth_attributes")),
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
            var hasDuration = false;
            var hasParameter = false;
            foreach (var pair in args)
            {
                if (pair.Key == "duration_ms" || pair.Key == "durationms") hasDuration = true;
                if (pair.Key == "duration_skill_level_parameter")
                {
                    hasParameter = true;
                    if (pair.Value.Kind != ActionArgKind.StringValue || string.IsNullOrWhiteSpace(pair.Value.StringValue))
                    { error = "duration_skill_level_parameter requires a non-empty constant name"; return false; }
                }
                if (pair.Key == "birth_attributes")
                {
                    if (pair.Value.Kind != ActionArgKind.StringValue || string.IsNullOrWhiteSpace(pair.Value.StringValue))
                    { error = "birth_attributes requires constant JSON"; return false; }
                    try { ReadBirthAttributes(pair.Value.StringValue); }
                    catch (Exception ex) { error = ex.Message; return false; }
                }
            }
            if (hasDuration && hasParameter)
            { error = "summon duration must select a fixed value or skill level parameter"; return false; }
            return RequireAny(args, "summon_id", out error, "summon_id", "summonid", "id");
        }

        private static MobaSummonBirthAttributeSpec[] ReadBirthAttributes(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var array = JArray.Parse(json);
            var result = new MobaSummonBirthAttributeSpec[array.Count];
            var seen = new HashSet<int>();
            for (var i = 0; i < array.Count; i++)
            {
                if (!(array[i] is JObject item) || item.Count != 2 ||
                    item["attribute"]?.Type != JTokenType.Integer || !(item["magnitude_terms"] is JArray terms))
                    throw new InvalidOperationException("birth_attributes entries require attribute and magnitude_terms.");
                var attribute = (int)item["attribute"];
                if (!Enum.IsDefined(typeof(BattleAttributeType), attribute) || attribute <= 0 || !seen.Add(attribute))
                    throw new InvalidOperationException("Invalid or duplicate summon birth attribute.");
                var magnitude = new MobaEffectMagnitudeSpec(default, default,
                    terms: MobaEffectMagnitudeSchemaReader.ReadTerms(terms.ToString()));
                result[i] = new MobaSummonBirthAttributeSpec((BattleAttributeType)attribute, in magnitude);
            }
            return result;
        }
    }
}
