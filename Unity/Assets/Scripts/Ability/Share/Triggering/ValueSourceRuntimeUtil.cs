using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Triggering.Runtime
{
    public readonly struct ValueSourceRuntime
    {
        public ValueSourceRuntime(ValueSourceKind kind, object constValue, VarScope fromScope, string fromKey)
        {
            Kind = kind;
            ConstValue = constValue;
            FromScope = fromScope;
            FromKey = fromKey;
        }

        public ValueSourceKind Kind { get; }
        public object ConstValue { get; }
        public VarScope FromScope { get; }
        public string FromKey { get; }
    }

    internal static class ValueSourceRuntimeUtil
    {
        public static ValueSourceRuntime Parse(IReadOnlyDictionary<string, object> args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));

            var kind = ValueSourceKind.Const;
            if (args.TryGetValue("value_source", out var valueSourceObj) && valueSourceObj is string valueSourceStr && !string.IsNullOrEmpty(valueSourceStr))
            {
                if (string.Equals(valueSourceStr, "var", StringComparison.OrdinalIgnoreCase)) kind = ValueSourceKind.Var;
                else if (string.Equals(valueSourceStr, "const", StringComparison.OrdinalIgnoreCase)) kind = ValueSourceKind.Const;
            }

            args.TryGetValue("value", out var constValueObj);

            var fromScope = VarScope.Local;
            if (args.TryGetValue("value_scope", out var valueScopeObj) && valueScopeObj is string valueScopeStr && !string.IsNullOrEmpty(valueScopeStr))
            {
                if (string.Equals(valueScopeStr, "global", StringComparison.OrdinalIgnoreCase)) fromScope = VarScope.Global;
                else if (string.Equals(valueScopeStr, "local", StringComparison.OrdinalIgnoreCase)) fromScope = VarScope.Local;
            }

            string fromKey = null;
            if (args.TryGetValue("value_key", out var valueKeyObj) && valueKeyObj is string valueKeyStr && !string.IsNullOrEmpty(valueKeyStr))
            {
                fromKey = valueKeyStr;
            }

            return new ValueSourceRuntime(kind, constValueObj, fromScope, fromKey);
        }

        public static object Resolve(TriggerContext context, in ValueSourceRuntime source)
        {
            if (source.Kind == ValueSourceKind.Var)
            {
                if (!string.IsNullOrEmpty(source.FromKey) && context.TryGetVar(source.FromScope, source.FromKey, out var obj))
                {
                    return obj;
                }

                return null;
            }

            return source.ConstValue;
        }

        public static bool TryToDouble(object obj, out double value)
        {
            value = 0d;
            if (obj == null) return false;
            try
            {
                value = Convert.ToDouble(obj);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
