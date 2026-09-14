using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Services.Combat.Magnitude;
using AbilityKit.Modifiers;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Variables.Numeric;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    internal static class MobaEffectMagnitudeSchemaReader
    {
        public static MobaEffectMagnitudeSpec Read(
            Dictionary<string, ActionArgValue> args,
            in ExecCtx<IWorldResolver> ctx,
            float fallbackValue)
        {
            if (!HasMagnitudeArgs(args)) return default;

            var baseSource = ReadSource(args, in ctx, "magnitude", fallbackValue);
            var secondarySource = HasPrefix(args, "magnitude_secondary_")
                ? ReadSource(args, in ctx, "magnitude_secondary", 0f)
                : default;
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
                enabled: true);
        }

        public static bool HasMagnitudeArgs(Dictionary<string, ActionArgValue> args)
        {
            return HasPrefix(args, "magnitude_");
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
            float fallbackValue)
        {
            var type = ReadEnum(args, in ctx, MagnitudeSourceType.Fixed,
                prefix + "_type", prefix + "_source_type");
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
                    var key = ReadString(args, prefix + "_context_key");
                    return MagnitudeSource.ContextFloat(key, coefficient, value);
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

        private static float ReadFloat(Dictionary<string, ActionArgValue> args, in ExecCtx<IWorldResolver> ctx, float fallback, params string[] names)
            => TryReadNumber(args, in ctx, out var value, names) ? (float)value : fallback;

        private static int ReadInt(Dictionary<string, ActionArgValue> args, in ExecCtx<IWorldResolver> ctx, int fallback, params string[] names)
            => TryReadNumber(args, in ctx, out var value, names) ? (int)Math.Round(value) : fallback;

        private static TEnum ReadEnum<TEnum>(Dictionary<string, ActionArgValue> args, in ExecCtx<IWorldResolver> ctx, TEnum fallback, params string[] names)
            where TEnum : struct, Enum
            => TryReadNumber(args, in ctx, out var value, names)
                ? (TEnum)Enum.ToObject(typeof(TEnum), (int)Math.Round(value))
                : fallback;

        private static string ReadString(Dictionary<string, ActionArgValue> args, params string[] names)
        {
            if (args == null) return string.Empty;
            foreach (var pair in args)
            {
                if (Matches(pair.Key, names) && pair.Value.Kind == ActionArgKind.StringValue)
                    return pair.Value.StringValue ?? string.Empty;
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
