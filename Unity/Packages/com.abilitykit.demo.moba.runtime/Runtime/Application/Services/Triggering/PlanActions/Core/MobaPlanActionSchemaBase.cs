using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Variables.Numeric;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public enum MobaPlanActionArgKind
    {
        Int = 0,
        Float = 1,
        Bool = 2,
        BoolNonZero = 3,
        Enum = 4,
    }

    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class GenerateMobaPlanActionSchemaAttribute : Attribute
    {
        public string ActionName { get; }

        public GenerateMobaPlanActionSchemaAttribute(string actionName)
        {
            ActionName = actionName;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class MobaPlanActionArgAttribute : Attribute
    {
        public MobaPlanActionArgKind Kind { get; }
        public double DefaultValue { get; }
        public bool Required { get; }
        public string[] Aliases { get; }
        public string DisplayName { get; set; }
        public double Min { get; set; } = double.NaN;
        public double Max { get; set; } = double.NaN;
        public bool ValidateEnum { get; set; } = true;

        public MobaPlanActionArgAttribute(
            MobaPlanActionArgKind kind,
            double defaultValue,
            bool required,
            params string[] aliases)
        {
            Kind = kind;
            DefaultValue = defaultValue;
            Required = required;
            Aliases = aliases ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Demo MOBA 强类型动作结构描述基类。
    /// 新结构描述只需要提供配置中的动作名称以及参数解析规则。
    /// </summary>
    public abstract class MobaPlanActionSchemaBase<TActionArgs> : ITriggerActionParseContextAwareSchema<TActionArgs, IWorldResolver>
    {
        protected abstract string ActionName { get; }

        public string ConfigActionName => ActionName;

        public ActionId ActionId => PlanActionRegisterUtil.GetActionId(ActionName);

        public Type ArgsType => typeof(TActionArgs);

        public abstract TActionArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx);

        public virtual TActionArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx, in TriggerActionParseContext parseContext)
        {
            return ParseArgs(namedArgs, ctx);
        }

        public abstract bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error);

        protected static float ReadFloat(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx, float defaultValue, params string[] aliases)
        {
            return TryReadNumber(namedArgs, ctx, out var value, aliases) ? (float)value : defaultValue;
        }

        protected static int ReadInt(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx, int defaultValue, params string[] aliases)
        {
            return TryReadNumber(namedArgs, ctx, out var value, aliases) ? (int)Math.Round(value) : defaultValue;
        }

        protected static bool ReadBool(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx, bool defaultValue, params string[] aliases)
        {
            return TryReadNumber(namedArgs, ctx, out var value, aliases) ? value >= 0.5 : defaultValue;
        }

        protected static bool ReadBoolNonZero(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx, bool defaultValue, params string[] aliases)
        {
            return TryReadNumber(namedArgs, ctx, out var value, aliases) ? value != 0 : defaultValue;
        }

        protected static TEnum ReadEnum<TEnum>(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx, TEnum defaultValue, params string[] aliases)
            where TEnum : struct, Enum
        {
            return TryReadNumber(namedArgs, ctx, out var value, aliases) ? (TEnum)Enum.ToObject(typeof(TEnum), (int)Math.Round(value)) : defaultValue;
        }

        protected static bool TryReadNumber(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx, out double value, params string[] aliases)
        {
            value = default;
            if (namedArgs == null || namedArgs.Count == 0 || aliases == null || aliases.Length == 0)
                return false;

            foreach (var kv in namedArgs)
            {
                if (!IsAlias(kv.Key, aliases))
                    continue;

                if (!TryResolveNumber(kv.Value, ctx, out value))
                {
                    value = default;
                    return false;
                }

                return true;
            }

            return false;
        }

        protected static bool TryReadBlackboardTarget(
            Dictionary<string, ActionArgValue> namedArgs,
            out BlackboardWriteTarget target,
            params string[] aliases)
        {
            target = default;
            if (namedArgs == null || aliases == null || aliases.Length == 0) return false;
            foreach (var pair in namedArgs)
            {
                if (!IsAlias(pair.Key, aliases)) continue;
                if (pair.Value.Kind != ActionArgKind.BlackboardTarget) return false;
                target = pair.Value.BlackboardTarget;
                return true;
            }
            return false;
        }

        protected bool RequireBlackboardTarget(
            ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args,
            string displayName,
            out string error,
            params string[] aliases)
        {
            foreach (var pair in args)
            {
                if (!IsAlias(pair.Key, aliases)) continue;
                if (pair.Value.Kind == ActionArgKind.BlackboardTarget)
                {
                    error = null;
                    return true;
                }
                error = $"{ActionName} parameter '{displayName}' must be a BlackboardTarget";
                return false;
            }
            error = $"{ActionName} is missing required parameter '{displayName}'";
            return false;
        }

        protected bool RequireNumericValue(
            ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args,
            string displayName,
            out string error,
            params string[] aliases)
        {
            foreach (var pair in args)
            {
                if (!IsAlias(pair.Key, aliases)) continue;
                if (pair.Value.Kind == ActionArgKind.NumericValue)
                {
                    error = null;
                    return true;
                }
                error = $"{ActionName} parameter '{displayName}' must be numeric";
                return false;
            }
            error = $"{ActionName} is missing required parameter '{displayName}'";
            return false;
        }

        private static bool TryResolveNumber(ActionArgValue arg, ExecCtx<IWorldResolver> ctx, out double value)
        {
            if (arg.Kind != ActionArgKind.NumericValue)
            {
                value = default;
                return false;
            }
            if (arg.Ref.Kind == ENumericValueRefKind.Const)
            {
                value = arg.Ref.ConstValue;
                return true;
            }

            return NumericValueRefResolver.TryResolve(arg.Ref, default(object), ctx, out value);
        }

        protected static int[] ReadPositiveInts(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx, params string[] aliases)
        {
            if (namedArgs == null || namedArgs.Count == 0 || aliases == null || aliases.Length == 0)
                return null;

            List<int> values = null;
            foreach (var kv in namedArgs)
            {
                if (!IsAlias(kv.Key, aliases))
                    continue;

                var value = (int)Math.Round(ResolveNumber(kv.Value, ctx));
                if (value <= 0)
                    continue;

                if (values == null)
                    values = new List<int>();
                values.Add(value);
            }

            return values == null || values.Count == 0 ? null : values.ToArray();
        }

        protected static bool HasAny(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, params string[] aliases)
        {
            if (aliases == null || aliases.Length == 0)
                return false;

            foreach (var kv in args)
            {
                if (IsAlias(kv.Key, aliases))
                    return true;
            }

            return false;
        }

        protected bool RequireAny(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, string displayName, out string error, params string[] aliases)
        {
            if (HasAny(args, aliases))
            {
                error = null;
                return true;
            }

            error = $"{ActionName} is missing required parameter '{displayName}'";
            return false;
        }

        protected bool ValidateNumericRange(
            ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args,
            string displayName,
            double min,
            double max,
            out string error,
            params string[] aliases)
        {
            foreach (var pair in args)
            {
                if (!IsAlias(pair.Key, aliases)) continue;
                if (pair.Value.Kind != ActionArgKind.NumericValue)
                {
                    error = $"{ActionName} parameter '{displayName}' must be numeric";
                    return false;
                }

                if (pair.Value.Ref.Kind != ENumericValueRefKind.Const) continue;
                var value = pair.Value.Ref.ConstValue;
                if ((!double.IsNaN(min) && value < min) || (!double.IsNaN(max) && value > max))
                {
                    error = $"{ActionName} parameter '{displayName}' is outside range [{min}, {max}]";
                    return false;
                }
            }

            error = null;
            return true;
        }

        protected bool ValidateEnumValue<TEnum>(
            ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args,
            string displayName,
            out string error,
            params string[] aliases)
            where TEnum : struct, Enum
        {
            foreach (var pair in args)
            {
                if (!IsAlias(pair.Key, aliases)) continue;
                if (pair.Value.Kind != ActionArgKind.NumericValue)
                {
                    error = $"{ActionName} parameter '{displayName}' must be numeric";
                    return false;
                }

                if (pair.Value.Ref.Kind != ENumericValueRefKind.Const) continue;
                var value = (int)Math.Round(pair.Value.Ref.ConstValue);
                if (!Enum.IsDefined(typeof(TEnum), value))
                {
                    error = $"{ActionName} parameter '{displayName}' has undefined {typeof(TEnum).Name} value {value}";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static double ResolveNumber(ActionArgValue arg, ExecCtx<IWorldResolver> ctx)
        {
            if (arg.Kind != ActionArgKind.NumericValue)
                throw new InvalidOperationException($"Action argument '{arg.Name}' is not numeric.");
            if (arg.Ref.Kind == ENumericValueRefKind.Const)
            {
                return arg.Ref.ConstValue;
            }

            return NumericValueRefResolver.Resolve(arg.Ref, default(object), ctx);
        }

        protected static bool TryReadCurrentPayloadNumber(ExecCtx<IWorldResolver> ctx, in TriggerActionParseContext parseContext, int fieldId, out double value)
        {
            return TryResolvePayloadField(fieldId, ctx, in parseContext, out value);
        }

        protected static int ReadCurrentPayloadInt(ExecCtx<IWorldResolver> ctx, in TriggerActionParseContext parseContext, int fieldId, int defaultValue = 0)
        {
            return TryResolvePayloadField(fieldId, ctx, in parseContext, out var value) ? (int)Math.Round(value) : defaultValue;
        }

        protected static int ReadCurrentPayloadInt(ExecCtx<IWorldResolver> ctx, in TriggerActionParseContext parseContext, string fieldName, Func<string, int> resolveFieldId, int defaultValue = 0)
        {
            if (string.IsNullOrWhiteSpace(fieldName) || resolveFieldId == null)
            {
                return defaultValue;
            }

            return ReadCurrentPayloadInt(ctx, in parseContext, resolveFieldId(fieldName), defaultValue);
        }

        [Obsolete("Use overloads with TriggerActionParseContext to avoid hidden trigger payload state.")]
        protected static bool TryReadCurrentPayloadNumber(ExecCtx<IWorldResolver> ctx, int fieldId, out double value)
        {
            value = default;
            return false;
        }

        [Obsolete("Use overloads with TriggerActionParseContext to avoid hidden trigger payload state.")]
        protected static int ReadCurrentPayloadInt(ExecCtx<IWorldResolver> ctx, int fieldId, int defaultValue = 0)
        {
            return defaultValue;
        }

        [Obsolete("Use overloads with TriggerActionParseContext to avoid hidden trigger payload state.")]
        protected static int ReadCurrentPayloadInt(ExecCtx<IWorldResolver> ctx, string fieldName, Func<string, int> resolveFieldId, int defaultValue = 0)
        {
            if (string.IsNullOrWhiteSpace(fieldName) || resolveFieldId == null)
            {
                return defaultValue;
            }

            return ReadCurrentPayloadInt(ctx, resolveFieldId(fieldName), defaultValue);
        }

        private static bool TryResolvePayloadField(int fieldId, ExecCtx<IWorldResolver> ctx, in TriggerActionParseContext parseContext, out double value)
        {
            var triggerArgs = parseContext.TriggerArgs;
            if (triggerArgs is MobaTriggerConditionContext conditionContext)
            {
                triggerArgs = conditionContext.Payload;
            }

            if (ctx.Payloads != null && triggerArgs != null)
            {
                if (ctx.Payloads.TryGetDouble(in triggerArgs, fieldId, out value))
                {
                    return true;
                }
            }

            value = default;
            return false;
        }

        private static bool IsAlias(string key, string[] aliases)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            for (int i = 0; i < aliases.Length; i++)
            {
                var alias = aliases[i];
                if (string.Equals(key, alias, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (HasIndexedAliasSuffix(key, alias))
                    return true;
            }

            return false;
        }

        private static bool HasIndexedAliasSuffix(string key, string alias)
        {
            if (string.IsNullOrEmpty(alias) || key.Length <= alias.Length)
            {
                return false;
            }

            if (!key.StartsWith(alias, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var start = alias.Length;
            if (key[start] == '_')
            {
                start++;
            }

            if (start >= key.Length)
            {
                return false;
            }

            for (int i = start; i < key.Length; i++)
            {
                if (!char.IsDigit(key[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
