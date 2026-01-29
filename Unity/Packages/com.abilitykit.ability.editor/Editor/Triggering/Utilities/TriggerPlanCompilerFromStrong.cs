//#if UNITY_EDITOR
//using System;
//using System.Collections.Generic;
//using System.Reflection;
//using AbilityKit.Ability.Triggering.Runtime;
//using AbilityKit.Triggering.Runtime.Plan;
//using AbilityKit.Triggering.Registry;
//using AbilityKit.Triggering.Runtime;
//using AbilityKit.Triggering.CodeGen;

//namespace AbilityKit.Ability.Editor.Utilities
//{
//    internal static class TriggerPlanCompilerFromStrong
//    {
//        public static TriggerPlan<TArgs> Compile<TArgs>(
//            int phase,
//            int priority,
//            JsonConditionEditorConfig condition,
//            JsonActionEditorConfig action)
//        {
//            return Compile<TArgs>(
//                phase,
//                priority,
//                condition,
//                action,
//                TriggerPlanCompilerResolvers.ResolvePayloadFieldId,
//                TriggerPlanCompilerResolvers.ResolveFunctionId,
//                TriggerPlanCompilerResolvers.ResolveActionId);
//        }

//        public static TriggerPlan<TArgs> Compile<TArgs>(
//            int phase,
//            int priority,
//            JsonConditionEditorConfig condition,
//            JsonActionEditorConfig action,
//            Func<string, int> payloadFieldIdResolver,
//            Func<string, FunctionId> functionIdResolver,
//            Func<string, ActionId> actionIdResolver)
//        {
//            var actions = CompileActions(action, payloadFieldIdResolver, actionIdResolver, out var legacyActions);

//            if (condition == null)
//            {
//                if (legacyActions != null) return new TriggerPlan<TArgs>(phase, priority, actions, legacyActions);
//                return new TriggerPlan<TArgs>(phase, priority, actions);
//            }

//            if (TryCompileConditionToExpr(condition, payloadFieldIdResolver, out var expr))
//            {
//                if (legacyActions != null) return new TriggerPlan<TArgs>(phase, priority, expr, actions, legacyActions);
//                return new TriggerPlan<TArgs>(phase, priority, expr, actions);
//            }

//            // Legacy predicate fallback (leaf only).
//            if (!string.IsNullOrEmpty(condition.TypeValue) &&
//                !string.Equals(condition.TypeValue, TriggerConditionTypes.All, StringComparison.Ordinal) &&
//                !string.Equals(condition.TypeValue, TriggerConditionTypes.Any, StringComparison.Ordinal) &&
//                !string.Equals(condition.TypeValue, TriggerConditionTypes.Not, StringComparison.Ordinal))
//            {
//                var legacyPred = new AbilityKit.Triggering.Runtime.Plan.LegacyPredicatePlan(condition.TypeValue, condition.Args);
//                return new TriggerPlan<TArgs>(phase, priority, legacyPred, actions, legacyActions);
//            }

//            throw new NotSupportedException($"Condition type not supported by compiler yet: {condition.TypeValue}");
//        }

//        private static ActionCallPlan[] CompileActions(
//            JsonActionEditorConfig action,
//            Func<string, int> payloadFieldIdResolver,
//            Func<string, ActionId> actionIdResolver,
//            out AbilityKit.Triggering.Runtime.Plan.LegacyActionPlan[] legacyActions)
//        {
//            legacyActions = null;
//            if (action == null) return Array.Empty<ActionCallPlan>();

//            if (GeneratedTriggerPlanCompiler.TryCompileActionTree(action, payloadFieldIdResolver, actionIdResolver, out var tree))
//            {
//                return tree ?? Array.Empty<ActionCallPlan>();
//            }

//            if (string.Equals(action.TypeValue, TriggerActionTypes.Seq, StringComparison.Ordinal))
//            {
//                if (action.Items == null || action.Items.Count == 0) return Array.Empty<ActionCallPlan>();

//                var list = new List<ActionCallPlan>(action.Items.Count);
//                var legacy = new List<AbilityKit.Triggering.Runtime.Plan.LegacyActionPlan>();
//                for (int i = 0; i < action.Items.Count; i++)
//                {
//                    if (!(action.Items[i] is JsonActionEditorConfig child)) continue;
//                    var compiled = CompileActions(child, payloadFieldIdResolver, actionIdResolver, out var childLegacy);
//                    if (compiled == null || compiled.Length == 0) continue;
//                    for (int j = 0; j < compiled.Length; j++) list.Add(compiled[j]);
//                    if (childLegacy != null && childLegacy.Length > 0) legacy.AddRange(childLegacy);
//                }
//                legacyActions = legacy.Count > 0 ? legacy.ToArray() : null;
//                return list.ToArray();
//            }

//            // Minimal example mapping: treat action.TypeValue as actionName.
//            // Params: currently supports up to 2 int params: p0/p1.
//            var id = actionIdResolver != null ? actionIdResolver(action.TypeValue) : default;
//            if (id.Value == 0)
//            {
//                legacyActions = new[] { new AbilityKit.Triggering.Runtime.Plan.LegacyActionPlan(action.TypeValue, action.Args) };
//                return Array.Empty<ActionCallPlan>();
//            }

//            if (action.Args == null || action.Args.Count == 0)
//            {
//                return new[] { new ActionCallPlan(id) };
//            }

//            if (GeneratedTriggerPlanCompiler.TryCompileAction(id, action.Args, payloadFieldIdResolver, out var generated))
//            {
//                return generated ?? Array.Empty<ActionCallPlan>();
//            }

//            // Prefer codegen schema if available.
//            if (TryGetActionParamSchema(id, out var schema) && schema != null)
//            {
//                return CompileActionWithSchema(id, action.Args, schema, payloadFieldIdResolver);
//            }

//            // Allow:
//            // - p0_field: payload field name -> IntValueRef.PayloadField
//            // - p0_const: int
//            // - p1_field, p1_const
//            var a0 = TryReadIntValueRef(action.Args, "p0", payloadFieldIdResolver);
//            var a1 = TryReadIntValueRef(action.Args, "p1", payloadFieldIdResolver);

//            if (a0.HasValue && a1.HasValue) return new[] { new ActionCallPlan(id, a0.Value, a1.Value) };
//            if (a0.HasValue) return new[] { new ActionCallPlan(id, a0.Value) };
//            // Fallback to legacy if we couldn't parse any args.
//            legacyActions = new[] { new AbilityKit.Triggering.Runtime.Plan.LegacyActionPlan(action.TypeValue, action.Args) };
//            return new[] { new ActionCallPlan(id) };
//        }

//        private static bool TryCompileConditionToExpr(JsonConditionEditorConfig cond, Func<string, int> payloadFieldIdResolver, out PredicateExprPlan plan)
//        {
//            plan = default;
//            try
//            {
//                plan = CompileCondition(cond, payloadFieldIdResolver);
//                return true;
//            }
//            catch (NotSupportedException)
//            {
//                return false;
//            }
//        }

//        private static ActionCallPlan[] CompileActionWithSchema(
//            ActionId id,
//            Dictionary<string, object> args,
//            TriggerParamDesc[] schema,
//            Func<string, int> payloadFieldIdResolver)
//        {
//            if (schema.Length > 2)
//            {
//                throw new NotSupportedException($"Action params > 2 are not supported by current ActionCallPlan. id={id.Value} paramCount={schema.Length}");
//            }

//            IntValueRef? p0 = null;
//            IntValueRef? p1 = null;

//            for (int i = 0; i < schema.Length; i++)
//            {
//                var p = schema[i];
//                if (p.Type != ETriggerParamType.Int)
//                {
//                    throw new NotSupportedException($"Only int params are supported by current Plan. id={id.Value} param='{p.Name}' type={p.Type}");
//                }

//                var v = TryReadSchemaIntValueRef(args, p.Name, payloadFieldIdResolver);
//                if (!v.HasValue) continue;

//                if (p.Index == 0) p0 = v;
//                else if (p.Index == 1) p1 = v;
//                else
//                {
//                    throw new NotSupportedException($"Action param index > 1 is not supported by current ActionCallPlan. id={id.Value} index={p.Index}");
//                }
//            }

//            if (p0.HasValue && p1.HasValue) return new[] { new ActionCallPlan(id, p0.Value, p1.Value) };
//            if (p0.HasValue) return new[] { new ActionCallPlan(id, p0.Value) };
//            return new[] { new ActionCallPlan(id) };
//        }

//        private static IntValueRef? TryReadSchemaIntValueRef(Dictionary<string, object> args, string name, Func<string, int> payloadFieldIdResolver)
//        {
//            if (args == null || string.IsNullOrEmpty(name)) return null;

//            // Prefer explicit suffixes.
//            if (args.TryGetValue(name + "_field", out var fObj) && fObj is string f && !string.IsNullOrEmpty(f))
//            {
//                return IntValueRef.PayloadField(ResolveFieldId(payloadFieldIdResolver, f));
//            }

//            if (args.TryGetValue(name + "_const", out var cObj) && cObj != null)
//            {
//                return IntValueRef.Const(Convert.ToInt32(cObj));
//            }

//            // Fallback: direct value means const.
//            if (args.TryGetValue(name, out var vObj) && vObj != null)
//            {
//                if (vObj is string s && !string.IsNullOrEmpty(s))
//                {
//                    return IntValueRef.PayloadField(ResolveFieldId(payloadFieldIdResolver, s));
//                }
//                return IntValueRef.Const(Convert.ToInt32(vObj));
//            }

//            return null;
//        }

//        private static bool TryGetActionParamSchema(ActionId id, out TriggerParamDesc[] schema)
//        {
//            schema = null;

//            // GeneratedSchemas lives in AbilityKit.Triggering runtime assembly and is optional.
//            var t = FindType("AbilityKit.Triggering.Generated.GeneratedSchemas");
//            if (t == null) return false;

//            var m = t.GetMethod("TryGetActionParams", BindingFlags.Public | BindingFlags.Static);
//            if (m == null) return false;

//            object[] parameters = { id, null };
//            var ok = (bool)m.Invoke(null, parameters);
//            if (!ok) return false;
//            schema = parameters[1] as TriggerParamDesc[];
//            return schema != null;
//        }

//        private static Type FindType(string fullName)
//        {
//            var t = Type.GetType(fullName);
//            if (t != null) return t;

//            var asms = AppDomain.CurrentDomain.GetAssemblies();
//            for (int i = 0; i < asms.Length; i++)
//            {
//                try
//                {
//                    t = asms[i].GetType(fullName);
//                    if (t != null) return t;
//                }
//                catch
//                {
//                }
//            }

//            return null;
//        }

//        private static PredicateExprPlan CompileCondition(JsonConditionEditorConfig cond, Func<string, int> payloadFieldIdResolver)
//        {
//            if (cond == null) return default;

//            if (GeneratedTriggerPlanCompiler.TryCompileConditionTree(cond, payloadFieldIdResolver, out var tree))
//            {
//                return tree;
//            }

//            if (string.Equals(cond.TypeValue, TriggerConditionTypes.All, StringComparison.Ordinal))
//            {
//                return CompileAll(cond.Items, payloadFieldIdResolver);
//            }

//            if (string.Equals(cond.TypeValue, TriggerConditionTypes.Any, StringComparison.Ordinal))
//            {
//                return CompileAny(cond.Items, payloadFieldIdResolver);
//            }

//            if (string.Equals(cond.TypeValue, TriggerConditionTypes.Not, StringComparison.Ordinal))
//            {
//                var inner = cond.Item as JsonConditionEditorConfig;
//                var nodes = new List<BoolExprNode>();
//                AppendExpr(nodes, inner, payloadFieldIdResolver);
//                nodes.Add(BoolExprNode.Not());
//                return new PredicateExprPlan(nodes.ToArray());
//            }

//            // Minimal mapping for:
//            // - arg_eq
//            // - arg_gt
//            if (string.Equals(cond.TypeValue, TriggerConditionTypes.ArgEq, StringComparison.Ordinal))
//            {
//                if (GeneratedTriggerPlanCompiler.TryCompileCondition(cond.TypeValue, cond.Args, payloadFieldIdResolver, out var gen))
//                {
//                    return gen;
//                }
//                return CompileCompare(cond.TypeValue, cond.Args, payloadFieldIdResolver, ECompareOp.Eq);
//            }

//            if (string.Equals(cond.TypeValue, TriggerConditionTypes.ArgGt, StringComparison.Ordinal))
//            {
//                if (GeneratedTriggerPlanCompiler.TryCompileCondition(cond.TypeValue, cond.Args, payloadFieldIdResolver, out var gen))
//                {
//                    return gen;
//                }
//                return CompileCompare(cond.TypeValue, cond.Args, payloadFieldIdResolver, ECompareOp.Gt);
//            }

//            throw new NotSupportedException($"Condition type not supported by compiler yet: {cond.TypeValue}");
//        }

//        private static PredicateExprPlan CompileAll(List<ConditionEditorConfigBase> items, Func<string, int> payloadFieldIdResolver)
//        {
//            if (items == null || items.Count == 0) return new PredicateExprPlan(new[] { BoolExprNode.Const(true) });

//            var nodes = new List<BoolExprNode>();
//            var first = true;
//            for (int i = 0; i < items.Count; i++)
//            {
//                var c = items[i] as JsonConditionEditorConfig;
//                if (c == null) continue;
//                AppendExpr(nodes, c, payloadFieldIdResolver);
//                if (!first) nodes.Add(BoolExprNode.And());
//                first = false;
//            }

//            if (first) nodes.Add(BoolExprNode.Const(true));
//            return new PredicateExprPlan(nodes.ToArray());
//        }

//        private static PredicateExprPlan CompileAny(List<ConditionEditorConfigBase> items, Func<string, int> payloadFieldIdResolver)
//        {
//            if (items == null || items.Count == 0) return new PredicateExprPlan(new[] { BoolExprNode.Const(false) });

//            var nodes = new List<BoolExprNode>();
//            var first = true;
//            for (int i = 0; i < items.Count; i++)
//            {
//                var c = items[i] as JsonConditionEditorConfig;
//                if (c == null) continue;
//                AppendExpr(nodes, c, payloadFieldIdResolver);
//                if (!first) nodes.Add(BoolExprNode.Or());
//                first = false;
//            }

//            if (first) nodes.Add(BoolExprNode.Const(false));
//            return new PredicateExprPlan(nodes.ToArray());
//        }

//        private static void AppendExpr(List<BoolExprNode> nodes, JsonConditionEditorConfig cond, Func<string, int> payloadFieldIdResolver)
//        {
//            if (cond == null)
//            {
//                nodes.Add(BoolExprNode.Const(true));
//                return;
//            }

//            var plan = CompileCondition(cond, payloadFieldIdResolver);
//            if (plan.Nodes != null)
//            {
//                for (int i = 0; i < plan.Nodes.Length; i++) nodes.Add(plan.Nodes[i]);
//            }
//        }

//        private static PredicateExprPlan CompileCompare(
//            string conditionType,
//            Dictionary<string, object> args,
//            Func<string, int> payloadFieldIdResolver,
//            ECompareOp op)
//        {
//            if (args == null) throw new InvalidOperationException("compare condition requires args");

//            if (TryGetConditionParamSchema(conditionType, out var schema) && schema != null && schema.Length == 2)
//            {
//                var left = TryReadSchemaIntValueRef(args, schema[0].Name, payloadFieldIdResolver);
//                var right = TryReadSchemaIntValueRef(args, schema[1].Name, payloadFieldIdResolver);
//                if (left.HasValue && right.HasValue)
//                {
//                    return new PredicateExprPlan(new[] { BoolExprNode.Compare(op, left.Value, right.Value) });
//                }
//            }

//            // Old configs use: key + value_source/value.
//            // Here we treat:
//            // - key: payload field name (string)
//            // - value: const int
//            // OR:
//            // - value_field: payload field name
//            if (!args.TryGetValue("key", out var keyObj) || !(keyObj is string key) || string.IsNullOrEmpty(key))
//            {
//                throw new InvalidOperationException("compare condition requires args['key'] as string");
//            }

//            var left = IntValueRef.PayloadField(ResolveFieldId(payloadFieldIdResolver, key));

//            IntValueRef right;
//            if (args.TryGetValue("value_field", out var vfObj) && vfObj is string vf && !string.IsNullOrEmpty(vf))
//            {
//                right = IntValueRef.PayloadField(ResolveFieldId(payloadFieldIdResolver, vf));
//            }
//            else if (args.TryGetValue("value", out var vObj) && vObj != null)
//            {
//                right = IntValueRef.Const(Convert.ToInt32(vObj));
//            }
//            else
//            {
//                throw new InvalidOperationException("compare condition requires args['value'] (const) or args['value_field']");
//            }

//            return new PredicateExprPlan(new[] { BoolExprNode.Compare(op, left, right) });
//        }

//        private static bool TryGetConditionParamSchema(string conditionType, out TriggerParamDesc[] schema)
//        {
//            schema = null;
//            if (string.IsNullOrEmpty(conditionType)) return false;

//            var t = FindType("AbilityKit.Triggering.Generated.GeneratedSchemas");
//            if (t == null) return false;

//            var m = t.GetMethod("TryGetConditionParams", BindingFlags.Public | BindingFlags.Static);
//            if (m == null) return false;

//            object[] parameters = { conditionType, null };
//            var ok = (bool)m.Invoke(null, parameters);
//            if (!ok) return false;
//            schema = parameters[1] as TriggerParamDesc[];
//            return schema != null;
//        }

//        private static IntValueRef? TryReadIntValueRef(Dictionary<string, object> args, string prefix, Func<string, int> payloadFieldIdResolver)
//        {
//            if (args == null) return null;

//            if (args.TryGetValue(prefix + "_field", out var fObj) && fObj is string f && !string.IsNullOrEmpty(f))
//            {
//                return IntValueRef.PayloadField(ResolveFieldId(payloadFieldIdResolver, f));
//            }

//            if (args.TryGetValue(prefix + "_const", out var cObj) && cObj != null)
//            {
//                return IntValueRef.Const(Convert.ToInt32(cObj));
//            }

//            return null;
//        }

//        private static int ResolveFieldId(Func<string, int> payloadFieldIdResolver, string fieldName)
//        {
//            if (payloadFieldIdResolver == null) throw new InvalidOperationException("payloadFieldIdResolver is null");
//            var id = payloadFieldIdResolver(fieldName);
//            if (id == 0) throw new InvalidOperationException($"FieldId resolve failed for '{fieldName}'");
//            return id;
//        }
//    }
//}
//#endif
