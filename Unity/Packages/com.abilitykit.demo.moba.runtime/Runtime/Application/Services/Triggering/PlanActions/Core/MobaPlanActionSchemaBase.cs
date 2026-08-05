using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Variables.Numeric;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
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

        private static bool TryResolveNumber(ActionArgValue arg, ExecCtx<IWorldResolver> ctx, out double value)
        {
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

        private static double ResolveNumber(ActionArgValue arg, ExecCtx<IWorldResolver> ctx)
        {
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
