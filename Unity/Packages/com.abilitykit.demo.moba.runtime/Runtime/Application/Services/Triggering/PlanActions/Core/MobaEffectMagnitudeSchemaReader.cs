using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Services.Combat.Magnitude;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Modifiers;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Triggering.Variables.Numeric;
using Newtonsoft.Json.Linq;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    internal enum MobaMagnitudeLevelSource { Skill = 0, CurrentHero = 1 }

    internal static class MobaEffectMagnitudeSchemaReader
    {
        public static MobaEffectMagnitudeSpec Read(
            Dictionary<string, ActionArgValue> args,
            in ExecCtx<IWorldResolver> ctx,
            float fallbackValue)
        {
            if (!HasMagnitudeArgs(args)) return default;

            if (HasName(args, "magnitude_terms"))
            {
                foreach (var pair in args)
                    if (pair.Key.StartsWith("magnitude_", StringComparison.OrdinalIgnoreCase) &&
                        pair.Key != "magnitude_terms" && pair.Key != "magnitude_evaluation" &&
                        pair.Key != "magnitude_capture")
                        throw new InvalidOperationException("magnitude_terms cannot be mixed with legacy magnitude sources.");
                var terms = ReadTerms(ReadString(args, in ctx, "magnitude_terms"));
                var termsPolicy = ReadEnum(args, in ctx, MobaEffectEvaluationPolicy.Realtime, "magnitude_evaluation");
                TryReadBlackboardTarget(args, out var capture, "magnitude_capture");
                return new MobaEffectMagnitudeSpec(default, default, evaluationPolicy: termsPolicy,
                    captureTarget: capture, terms: terms);
            }

            var baseSource = ReadSource(args, in ctx, "magnitude", fallbackValue, out var baseSkillParameter);
            string secondarySkillParameter = null;
            string secondaryCoefficientSkillParameter = null;
            var secondarySource = HasPrefix(args, "magnitude_secondary_")
                ? ReadSource(args, in ctx, "magnitude_secondary", 0f, out secondarySkillParameter)
                : default;
            if (HasName(args, "magnitude_secondary_coefficient_parameter"))
                secondaryCoefficientSkillParameter = ReadString(args, in ctx, "magnitude_secondary_coefficient_parameter");
            var combine = ReadEnum(args, in ctx, MobaEffectMagnitudeCombine.Add,
                "magnitude_combine");
            var sourceRole = ReadEnum(args, in ctx, MobaEffectSourceRole.AttributionActor,
                "magnitude_source_role", "magnitude_role");
            var policy = ReadEnum(args, in ctx, MobaEffectEvaluationPolicy.Realtime,
                "magnitude_evaluation", "magnitude_evaluation_policy");
            TryReadBlackboardTarget(args, out var captureTarget,
                "magnitude_capture", "magnitude_capture_target");
            return new MobaEffectMagnitudeSpec(
                in baseSource,
                in secondarySource,
                combine,
                sourceRole,
                policy,
                captureTarget,
                enabled: true,
                baseSkillLevelParameter: baseSkillParameter,
                secondarySkillLevelParameter: secondarySkillParameter,
                secondaryCoefficientSkillLevelParameter: secondaryCoefficientSkillParameter,
                useCurrentHeroLevel: ReadEnum(args, in ctx, MobaMagnitudeLevelSource.Skill, "magnitude_level_source") == MobaMagnitudeLevelSource.CurrentHero);
        }

        public static bool HasMagnitudeArgs(Dictionary<string, ActionArgValue> args)
        {
            return HasPrefix(args, "magnitude_");
        }

        internal static bool TryValidateTerms(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            error = null;
            Dictionary<string, ActionArgValue> dictionary = null;
            foreach (var pair in args)
                if (string.Equals(pair.Key, "magnitude_terms", StringComparison.OrdinalIgnoreCase))
                {
                    if (pair.Value.Kind != ActionArgKind.StringValue || string.IsNullOrWhiteSpace(pair.Value.StringValue))
                    { error = "magnitude_terms requires constant JSON"; return false; }
                    dictionary = new Dictionary<string, ActionArgValue>(StringComparer.OrdinalIgnoreCase);
                    break;
                }
            if (dictionary == null) return true;
            foreach (var pair in args) dictionary.Add(pair.Key, pair.Value);
            try { Read(dictionary, default, 0); return true; }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        /// <summary>
        /// 标量 Trigger 参数使用 JSON 字符串承载有序数值项。
        /// 各项只描述来源，冻结与快照由外层公式统一管理，禁止嵌套公式。
        /// </summary>
        internal static MobaEffectMagnitudeSpec[] ReadTerms(string json)
        {
            JArray array;
            try { array = JArray.Parse(json); }
            catch (Exception ex) { throw new InvalidOperationException("Invalid magnitude_terms JSON array.", ex); }
            if (array.Count == 0) throw new InvalidOperationException("magnitude_terms must not be empty.");
            var terms = new MobaEffectMagnitudeSpec[array.Count];
            for (var i = 0; i < array.Count; i++)
            {
                if (!(array[i] is JObject item)) throw new InvalidOperationException("Magnitude term must be an object.");
                var args = new Dictionary<string, ActionArgValue>(StringComparer.OrdinalIgnoreCase);
                foreach (var field in item.Properties())
                {
                    var name = "magnitude_" + field.Name;
                    switch (field.Name)
                    {
                        case "parameter":
                        case "coefficient_parameter":
                            if (field.Value.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)field.Value))
                                throw new InvalidOperationException("Magnitude parameter must be a non-empty string.");
                            args.Add(name, ActionArgValue.OfString((string)field.Value, name));
                            break;
                        case "type": case "value": case "attribute": case "coefficient": case "source_role":
                            if ((field.Value.Type != JTokenType.Integer && field.Value.Type != JTokenType.Float) ||
                                double.IsNaN((double)field.Value) || double.IsInfinity((double)field.Value) ||
                                Math.Abs((double)field.Value) > float.MaxValue)
                                throw new InvalidOperationException("Magnitude term requires finite numeric fields.");
                            if ((field.Name == "type" || field.Name == "attribute" || field.Name == "source_role") &&
                                ((double)field.Value != Math.Truncate((double)field.Value) ||
                                 (double)field.Value < 0 || (double)field.Value > int.MaxValue))
                                throw new InvalidOperationException("Magnitude enum and attribute fields require non-negative integers.");
                            if (field.Name == "type" && (double)field.Value != 0 && (double)field.Value != 2 &&
                                (double)field.Value != 6 && (double)field.Value != 7)
                                throw new InvalidOperationException("Unsupported magnitude term type.");
                            if (field.Name == "source_role" &&
                                ((double)field.Value > byte.MaxValue ||
                                 !Enum.IsDefined(typeof(MobaEffectSourceRole), Enum.ToObject(typeof(MobaEffectSourceRole), (int)field.Value))))
                                throw new InvalidOperationException("Invalid magnitude source role.");
                            args.Add(name, ActionArgValue.OfConst((double)field.Value, name));
                            break;
                        default: throw new InvalidOperationException("Unknown magnitude term field: " + field.Name);
                    }
                }
                var type = ReadEnum(args, default, MagnitudeSourceType.Fixed, "magnitude_type");
                if (type != MagnitudeSourceType.Fixed && type != MagnitudeSourceType.Attribute &&
                    type != MagnitudeSourceType.SkillLevelParameter && type != MagnitudeSourceType.HeroLevelSkillParameter)
                    throw new InvalidOperationException("Magnitude term requires Fixed, Attribute, SkillLevelParameter or HeroLevelSkillParameter.");
                var role = ReadEnum(args, default, MobaEffectSourceRole.AttributionActor, "magnitude_source_role");
                if (!Enum.IsDefined(typeof(MobaEffectSourceRole), role)) throw new InvalidOperationException("Invalid magnitude source role.");
                var attribute = ReadInt(args, default, 0, "magnitude_attribute");
                if (type == MagnitudeSourceType.Attribute &&
                    (attribute <= 0 || !Enum.IsDefined(typeof(BattleAttributeType), attribute)))
                    throw new InvalidOperationException("Attribute magnitude term requires a valid battle attribute.");
                var source = ReadSource(args, default, "magnitude", 0, out var parameter);
                if (type == MagnitudeSourceType.Fixed)
                    source = MagnitudeSource.Fixed(source.Data0 * ReadFloat(args, default, 1f, "magnitude_coefficient"));
                var coefficientParameter = ReadString(args, default, "magnitude_coefficient_parameter");
                if (!string.IsNullOrEmpty(coefficientParameter) && type != MagnitudeSourceType.Attribute)
                    throw new InvalidOperationException("coefficient_parameter requires Attribute.");
                /*
                 * 系数参数沿用既有 Attribute 次项求值，避免添加另一份等级参数读取逻辑。
                 */
                terms[i] = type == MagnitudeSourceType.Attribute
                    ? new MobaEffectMagnitudeSpec(default, in source, sourceRole: role,
                        secondaryCoefficientSkillLevelParameter: coefficientParameter)
                    : new MobaEffectMagnitudeSpec(in source, default, sourceRole: role, baseSkillLevelParameter: parameter);
            }
            return terms;
        }

        public static bool HasMagnitudeArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i].Key != null && args[i].Key.StartsWith("magnitude_", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static MagnitudeSource ReadSource(
            Dictionary<string, ActionArgValue> args,
            in ExecCtx<IWorldResolver> ctx,
            string prefix,
            float fallbackValue,
            out string skillLevelParameter)
        {
            skillLevelParameter = null;
            var type = ReadEnum(args, in ctx, MagnitudeSourceType.Fixed,
                prefix + "_type", prefix + "_source_type");
            var hasSkillParameter = HasName(args, prefix + "_parameter");
            if (type == MagnitudeSourceType.SkillLevelParameter || type == MagnitudeSourceType.HeroLevelSkillParameter)
            {
                skillLevelParameter = ReadString(args, in ctx, prefix + "_parameter");
                if (string.IsNullOrWhiteSpace(skillLevelParameter))
                    throw new InvalidOperationException($"Magnitude source '{prefix}' requires a non-empty skill level parameter name.");
            }
            else if (hasSkillParameter)
            {
                throw new InvalidOperationException($"Magnitude source '{prefix}' supplies a skill level parameter but selects {type}.");
            }
            var value = ReadFloat(args, in ctx, fallbackValue,
                prefix + "_value", prefix + "_base_value");
            var coefficient = ReadFloat(args, in ctx, 1f, prefix + "_coefficient");
            switch (type)
            {
                case MagnitudeSourceType.Scalable:
                    return MagnitudeSource.LevelCurve(value, ReadCurve(args, in ctx, prefix + "_curve"), coefficient);
                case MagnitudeSourceType.Attribute:
                    var attribute = ReadInt(args, in ctx, 0, prefix + "_attribute", prefix + "_attribute_type");
                    return MagnitudeSource.Attribute(ModifierKey.FromPacked((uint)Math.Max(0, attribute)), coefficient);
                case MagnitudeSourceType.TimeDecay:
                    var duration = ReadFloat(args, in ctx, 0f, prefix + "_duration");
                    var decay = ReadEnum(args, in ctx, DecayType.Linear, prefix + "_decay", prefix + "_decay_type");
                    return MagnitudeSource.TimeDecay(value, duration, coefficient, decay);
                case MagnitudeSourceType.ContextFloat:
                    var key = ReadString(args, in ctx, prefix + "_context_key");
                    return MagnitudeSource.ContextFloat(key, coefficient, value);
                case MagnitudeSourceType.SkillLevelParameter:
                    return new MagnitudeSource { Type = MagnitudeSourceType.SkillLevelParameter, Data1 = coefficient };
                case MagnitudeSourceType.HeroLevelSkillParameter:
                    return new MagnitudeSource { Type = MagnitudeSourceType.HeroLevelSkillParameter, Data1 = coefficient };
                case MagnitudeSourceType.Pipeline:
                    throw new InvalidOperationException("Magnitude Pipeline must be supplied by a registered code or config template.");
                default:
                    return MagnitudeSource.Fixed(value);
            }
        }

        private static float[] ReadCurve(Dictionary<string, ActionArgValue> args, in ExecCtx<IWorldResolver> ctx, string prefix)
        {
            List<float> values = null;
            for (var i = 0; i < 32; i++)
            {
                if (!TryReadNumber(args, in ctx, out var value, prefix + "_" + i, prefix + i)) break;
                if (values == null) values = new List<float>(8);
                values.Add((float)value);
            }
            return values == null || values.Count == 0 ? null : values.ToArray();
        }

        private static bool HasPrefix(Dictionary<string, ActionArgValue> args, string prefix)
        {
            if (args == null) return false;
            foreach (var pair in args)
            {
                if (pair.Key != null && pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool HasName(Dictionary<string, ActionArgValue> args, string name)
        {
            if (args == null) return false;
            foreach (var pair in args)
            {
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static float ReadFloat(Dictionary<string, ActionArgValue> args, in ExecCtx<IWorldResolver> ctx, float fallback, params string[] names)
            => TryReadNumber(args, in ctx, out var value, names) ? (float)value : fallback;

        private static int ReadInt(Dictionary<string, ActionArgValue> args, in ExecCtx<IWorldResolver> ctx, int fallback, params string[] names)
            => TryReadNumber(args, in ctx, out var value, names) ? (int)Math.Round(value) : fallback;

        private static TEnum ReadEnum<TEnum>(Dictionary<string, ActionArgValue> args, in ExecCtx<IWorldResolver> ctx, TEnum fallback, params string[] names)
            where TEnum : struct, Enum
            => TryReadNumber(args, in ctx, out var value, names)
                ? (TEnum)Enum.ToObject(typeof(TEnum), (int)Math.Round(value))
                : fallback;

        internal static string ReadString(
            Dictionary<string, ActionArgValue> args,
            in ExecCtx<IWorldResolver> ctx,
            params string[] names)
        {
            if (args == null) return string.Empty;
            foreach (var pair in args)
            {
                if (!Matches(pair.Key, names)) continue;
                if (pair.Value.Kind == ActionArgKind.StringValue)
                    return pair.Value.StringValue ?? string.Empty;

                if (pair.Value.Kind == ActionArgKind.NumericValue &&
                    pair.Value.Ref.Kind == ENumericValueRefKind.Const &&
                    ctx.Context != null &&
                    ctx.Context.TryResolve<TriggerPlanJsonDatabase>(out var plans) &&
                    plans != null &&
                    plans.TryGetString((int)Math.Round(pair.Value.Ref.ConstValue), out var value))
                    return value ?? string.Empty;
            }
            return string.Empty;
        }

        private static bool TryReadBlackboardTarget(Dictionary<string, ActionArgValue> args, out BlackboardWriteTarget target, params string[] names)
        {
            target = default;
            if (args == null) return false;
            foreach (var pair in args)
            {
                if (!Matches(pair.Key, names) || pair.Value.Kind != ActionArgKind.BlackboardTarget) continue;
                target = pair.Value.BlackboardTarget;
                return true;
            }
            return false;
        }

        private static bool TryReadNumber(Dictionary<string, ActionArgValue> args, in ExecCtx<IWorldResolver> ctx, out double value, params string[] names)
        {
            value = default;
            if (args == null) return false;
            foreach (var pair in args)
            {
                if (!Matches(pair.Key, names) || pair.Value.Kind != ActionArgKind.NumericValue) continue;
                if (pair.Value.Ref.Kind == ENumericValueRefKind.Const)
                {
                    value = pair.Value.Ref.ConstValue;
                    return true;
                }
                return NumericValueRefResolver.TryResolve(pair.Value.Ref, default(object), ctx, out value);
            }
            return false;
        }

        private static bool Matches(string key, string[] names)
        {
            if (key == null) return false;
            for (var i = 0; i < names.Length; i++)
            {
                if (string.Equals(key, names[i], StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
