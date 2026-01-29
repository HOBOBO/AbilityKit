#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Triggering.CodeGen;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Registry;

namespace AbilityKit.Ability.Editor.Utilities
{
    // Fallback implementation when codegen output is missing.
    // Uses reflection to discover [TriggerAction]/[TriggerCondition]/[TriggerParam] schema at editor time.
    // This is intended for export-time compilation only.
    internal static partial class GeneratedTriggerPlanCompiler
    {
        private static bool _fallbackInitialized;
        private static readonly Dictionary<int, TriggerParamAttribute[]> _actionParamsById = new Dictionary<int, TriggerParamAttribute[]>();
        private static readonly Dictionary<string, TriggerParamAttribute[]> _conditionParamsByType = new Dictionary<string, TriggerParamAttribute[]>(StringComparer.Ordinal);

        private static void EnsureFallbackInitialized()
        {
            if (_fallbackInitialized) return;
            _fallbackInitialized = true;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                if (types == null) continue;

                for (int i = 0; i < types.Length; i++)
                {
                    var t = types[i];
                    if (t == null) continue;

                    // Condition configs: class-level attribute.
                    var condAttr = t.GetCustomAttribute<TriggerConditionAttribute>(false);
                    if (condAttr != null && !string.IsNullOrEmpty(condAttr.Type) && !_conditionParamsByType.ContainsKey(condAttr.Type))
                    {
                        _conditionParamsByType[condAttr.Type] = t.GetCustomAttributes<TriggerParamAttribute>(false)
                            .OrderBy(p => p.Index)
                            .ToArray();
                    }

                    // Action: class-level attribute.
                    var actionClassAttr = t.GetCustomAttribute<TriggerActionAttribute>(false);
                    if (actionClassAttr != null && !string.IsNullOrEmpty(actionClassAttr.Name))
                    {
                        var id = StableStringId.Get("action:" + actionClassAttr.Name);
                        if (!_actionParamsById.ContainsKey(id))
                        {
                            _actionParamsById[id] = t.GetCustomAttributes<TriggerParamAttribute>(false)
                                .OrderBy(p => p.Index)
                                .ToArray();
                        }
                    }

                    // Action: method-level attribute.
                    var methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    for (int m = 0; m < methods.Length; m++)
                    {
                        var mi = methods[m];
                        var actionAttr = mi.GetCustomAttribute<TriggerActionAttribute>(false);
                        if (actionAttr == null || string.IsNullOrEmpty(actionAttr.Name)) continue;

                        var id = StableStringId.Get("action:" + actionAttr.Name);
                        if (!_actionParamsById.ContainsKey(id))
                        {
                            _actionParamsById[id] = mi.GetCustomAttributes<TriggerParamAttribute>(false)
                                .OrderBy(p => p.Index)
                                .ToArray();
                        }
                    }
                }
            }
        }

        public static bool TryCompileAction(ActionId id, Dictionary<string, object> args, Func<string, int> payloadFieldIdResolver, out ActionCallPlan[] plans)
        {
            plans = null;
            EnsureFallbackInitialized();

            if (id.Value == 0) return false;

            _actionParamsById.TryGetValue(id.Value, out var ps);
            ps ??= Array.Empty<TriggerParamAttribute>();

            // Current strong plan compiler only supports <=2 int params.
            if (ps.Length > 2 || ps.Any(p => p.Type != ETriggerParamType.Int))
            {
                return false;
            }

            if (ps.Length == 0)
            {
                plans = new[] { new ActionCallPlan(id) };
                return true;
            }

            var a0 = TryReadSchemaIntValueRef(args, ps[0].Name, payloadFieldIdResolver);
            if (ps.Length == 2)
            {
                var a1 = TryReadSchemaIntValueRef(args, ps[1].Name, payloadFieldIdResolver);
                if (a0.HasValue && a1.HasValue) plans = new[] { new ActionCallPlan(id, a0.Value, a1.Value) };
                else if (a0.HasValue) plans = new[] { new ActionCallPlan(id, a0.Value) };
                else plans = new[] { new ActionCallPlan(id) };
            }
            else
            {
                if (a0.HasValue) plans = new[] { new ActionCallPlan(id, a0.Value) };
                else plans = new[] { new ActionCallPlan(id) };
            }

            return true;
        }

        public static bool TryCompileCondition(string type, Dictionary<string, object> args, Func<string, int> payloadFieldIdResolver, out PredicateExprPlan plan)
        {
            plan = default;
            EnsureFallbackInitialized();

            if (string.IsNullOrEmpty(type)) return false;

            // Fallback currently supports compare-int style conditions only.
            // We keep behavior aligned with TriggerPlanCompilerCodegenMenu's current generation scope.
            if (!string.Equals(type, TriggerConditionTypes.ArgEq, StringComparison.Ordinal) &&
                !string.Equals(type, TriggerConditionTypes.ArgGt, StringComparison.Ordinal))
            {
                return false;
            }

            _conditionParamsByType.TryGetValue(type, out var ps);
            ps ??= Array.Empty<TriggerParamAttribute>();

            var leftName = ps.Length > 0 ? ps[0].Name : "key";
            var rightName = ps.Length > 1 ? ps[1].Name : "value";

            var left = TryReadSchemaIntValueRef(args, leftName, payloadFieldIdResolver);
            var right = TryReadSchemaIntValueRef(args, rightName, payloadFieldIdResolver);
            if (!left.HasValue || !right.HasValue) return false;

            var op = string.Equals(type, TriggerConditionTypes.ArgEq, StringComparison.Ordinal) ? ECompareOp.Eq : ECompareOp.Gt;
            plan = new PredicateExprPlan(new[] { BoolExprNode.Compare(op, left.Value, right.Value) });
            return true;
        }

        private static IntValueRef? TryReadSchemaIntValueRef(Dictionary<string, object> args, string name, Func<string, int> payloadFieldIdResolver)
        {
            if (args == null || string.IsNullOrEmpty(name)) return null;

            if (args.TryGetValue(name + "_field", out var fObj) && fObj is string f && !string.IsNullOrEmpty(f))
            {
                return IntValueRef.PayloadField(ResolveFieldId(payloadFieldIdResolver, f));
            }

            if (args.TryGetValue(name + "_const", out var cObj) && cObj != null)
            {
                return IntValueRef.Const(Convert.ToInt32(cObj));
            }

            if (args.TryGetValue(name, out var vObj) && vObj != null)
            {
                if (vObj is string s && !string.IsNullOrEmpty(s)) return IntValueRef.PayloadField(ResolveFieldId(payloadFieldIdResolver, s));
                return IntValueRef.Const(Convert.ToInt32(vObj));
            }

            return null;
        }

        private static int ResolveFieldId(Func<string, int> payloadFieldIdResolver, string fieldName)
        {
            if (payloadFieldIdResolver == null) throw new InvalidOperationException("payloadFieldIdResolver is null");
            var id = payloadFieldIdResolver(fieldName);
            if (id == 0) throw new InvalidOperationException($"FieldId resolve failed for '{fieldName}'");
            return id;
        }
    }
}
#endif
