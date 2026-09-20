#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using AbilityKit.Ability.Config.Authoring;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal enum TriggerAuthoringDryRunState
    {
        Passed,
        Failed,
        Unknown,
        WouldExecute,
        Potential,
        Skipped,
        Disabled,
        Error
    }

    internal sealed class TriggerAuthoringDryRunEntry
    {
        public string NodeId;
        public string Path;
        public TriggerNodeKind Kind;
        public TriggerNodeKind WorkspaceKind;
        public string Type;
        public int Depth;
        public TriggerAuthoringDryRunState State;
        public string Message;
    }

    internal sealed class TriggerAuthoringDryRunResult
    {
        public readonly List<TriggerAuthoringDryRunEntry> Entries = new List<TriggerAuthoringDryRunEntry>();
        public TriggerAuthoringDryRunState EntryState;
        public string Error;

        public int Count(TriggerAuthoringDryRunState state)
        {
            var count = 0;
            for (var i = 0; i < Entries.Count; i++)
                if (Entries[i].State == state) count++;
            return count;
        }
    }

    internal sealed class TriggerAuthoringDryRunInput
    {
        private const string DefaultJson =
            "{\n" +
            "  \"payload\": {},\n" +
            "  \"context\": {},\n" +
            "  \"local\": {},\n" +
            "  \"global\": {}\n" +
            "}";

        public JObject Payload = new JObject();
        public JObject Context = new JObject();
        public JObject Local = new JObject();
        public JObject Global = new JObject();

        public static string CreateDefaultJson()
        {
            return DefaultJson;
        }

        public static bool TryParse(string json, out TriggerAuthoringDryRunInput input, out string error)
        {
            input = null;
            error = null;
            try
            {
                var root = string.IsNullOrWhiteSpace(json) ? new JObject() : JObject.Parse(json);
                input = new TriggerAuthoringDryRunInput
                {
                    Payload = ReadObject(root, "payload"),
                    Context = ReadObject(root, "context"),
                    Local = ReadObject(root, "local"),
                    Global = ReadObject(root, "global")
                };
                return true;
            }
            catch (JsonException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static JObject ReadObject(JObject root, string name)
        {
            if (!root.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out var token) ||
                token == null || token.Type == JTokenType.Null)
                return new JObject();
            if (token is JObject value) return value;
            throw new JsonSerializationException("'" + name + "' must be a JSON object.");
        }
    }

    internal static class TriggerAuthoringDryRun
    {
        private enum Truth
        {
            True,
            False,
            Unknown,
            Skipped
        }

        private enum ActionGate
        {
            Active,
            Potential,
            Skipped
        }

        private sealed class EvaluationContext
        {
            public TriggerAuthoringModuleData Module;
            public TriggerDefinitionData Trigger;
            public TriggerAuthoringDryRunInput Input;
            public TriggerAuthoringDryRunResult Result;
        }

        public static TriggerAuthoringDryRunResult Run(
            TriggerAuthoringModuleData module,
            TriggerDefinitionData trigger,
            int triggerIndex,
            TriggerTemplateDescriptorCatalog templates,
            TriggerAuthoringDryRunInput input)
        {
            var result = new TriggerAuthoringDryRunResult();
            if (module == null || trigger == null)
            {
                result.EntryState = TriggerAuthoringDryRunState.Error;
                result.Error = "Trigger module or trigger is missing.";
                return result;
            }

            var effective = trigger;
            var templateMode = false;
            if (trigger.Template != null && templates != null &&
                templates.TryGet(trigger.Template.TemplateId, out var templateAsset) &&
                templateAsset?.Template != null)
            {
                effective = TriggerAuthoringTemplateDefinition.CreateMaterialized(
                    trigger,
                    templateAsset.Template,
                    out var materializeError);
                if (effective == null)
                {
                    result.EntryState = TriggerAuthoringDryRunState.Error;
                    result.Error = materializeError ?? "Template materialization failed.";
                    return result;
                }
                templateMode = true;
            }

            var context = new EvaluationContext
            {
                Module = module,
                Trigger = effective,
                Input = input ?? new TriggerAuthoringDryRunInput(),
                Result = result
            };
            var triggerPath = "module.triggers[" + triggerIndex + "]";
            var condition = effective.Condition;
            var actions = effective.Actions;
            if (!effective.Enabled)
            {
                result.EntryState = TriggerAuthoringDryRunState.Disabled;
                TraceSkippedNode(
                    context,
                    condition,
                    triggerPath + ".condition",
                    TriggerNodeKind.Condition,
                    TriggerNodeKind.Condition,
                    0);
                TraceAction(
                    context,
                    actions,
                    triggerPath + ".actions",
                    0,
                    ActionGate.Skipped,
                    new HashSet<string>(StringComparer.Ordinal));
                return result;
            }
            if (templateMode)
            {
                condition = ExpandTemplateRoot(module, condition, TriggerNodeKind.Condition, result);
                actions = ExpandTemplateRoot(module, actions, TriggerNodeKind.Action, result);
                if (!string.IsNullOrEmpty(result.Error))
                {
                    result.EntryState = TriggerAuthoringDryRunState.Error;
                    return result;
                }
            }

            var truth = condition == null
                ? Truth.True
                : TraceCondition(
                    context,
                    condition,
                    triggerPath + ".condition",
                    TriggerNodeKind.Condition,
                    0,
                    new HashSet<string>(StringComparer.Ordinal));
            if (truth == Truth.Skipped) truth = Truth.True;
            result.EntryState = ToConditionState(truth);

            var actionGate = truth == Truth.True
                ? ActionGate.Active
                : truth == Truth.False ? ActionGate.Skipped : ActionGate.Potential;
            TraceAction(
                context,
                actions,
                triggerPath + ".actions",
                0,
                actionGate,
                new HashSet<string>(StringComparer.Ordinal));
            return result;
        }

        private static TriggerNodeData ExpandTemplateRoot(
            TriggerAuthoringModuleData module,
            TriggerNodeData root,
            TriggerNodeKind kind,
            TriggerAuthoringDryRunResult result)
        {
            if (root == null) return null;
            if (TriggerAuthoringGroupResolver.TryExpand(module, root, kind, out var expanded, out var failure))
                return expanded;
            result.Error = failure?.Message ?? "Reusable group expansion failed.";
            return null;
        }

        private static Truth TraceCondition(
            EvaluationContext context,
            TriggerNodeData node,
            string path,
            TriggerNodeKind workspaceKind,
            int depth,
            ISet<string> resolvingGroups)
        {
            if (node == null) return Truth.Skipped;
            var entry = AddEntry(context, node, path, TriggerNodeKind.Condition, workspaceKind, depth);
            if (!node.Enabled)
            {
                entry.State = TriggerAuthoringDryRunState.Disabled;
                entry.Message = "Disabled condition.";
                TraceSkippedChildren(context, node, path, workspaceKind, depth + 1);
                return Truth.Skipped;
            }

            if (!string.IsNullOrWhiteSpace(node.GroupReference))
            {
                if (!resolvingGroups.Add(node.GroupReference))
                    return SetCondition(entry, Truth.Unknown, "Reusable condition group cycle detected.");
                try
                {
                    if (!TriggerAuthoringGroupResolver.TryExpand(
                            context.Module,
                            node,
                            TriggerNodeKind.Condition,
                            out var expanded,
                            out var failure) || expanded == null)
                        return SetCondition(entry, Truth.Unknown, failure?.Message ?? "Reusable condition group is missing.");
                    var value = TraceCondition(
                        context,
                        expanded,
                        path + ".resolved",
                        workspaceKind,
                        depth + 1,
                        resolvingGroups);
                    return SetCondition(entry, value, "Resolved group: " + node.GroupReference);
                }
                finally
                {
                    resolvingGroups.Remove(node.GroupReference);
                }
            }

            var type = (node.Type ?? string.Empty).Trim().ToLowerInvariant();
            switch (type)
            {
                case "always_true":
                    return SetCondition(entry, Truth.True, "Constant true.");
                case "always_false":
                    return SetCondition(entry, Truth.False, "Constant false.");
                case "all":
                    return TraceAll(context, node, path, workspaceKind, depth, resolvingGroups, entry);
                case "any":
                    return TraceAny(context, node, path, workspaceKind, depth, resolvingGroups, entry);
                case "not":
                    return TraceNot(context, node, path, workspaceKind, depth, resolvingGroups, entry);
                case "arg_eq":
                case "arg_neq":
                case "arg_gt":
                case "arg_gte":
                case "arg_geq":
                case "arg_lt":
                case "arg_lte":
                case "arg_leq":
                    return TraceComparison(context, node, type, "left", "right", entry);
                case "num_var_eq":
                case "num_var_gt":
                case "num_var_lt":
                    return TraceComparison(context, node, type, "variable", "value", entry);
                default:
                    return SetCondition(
                        entry,
                        Truth.Unknown,
                        "Requires runtime world or a registered condition evaluator.");
            }
        }

        private static Truth TraceAll(
            EvaluationContext context,
            TriggerNodeData node,
            string path,
            TriggerNodeKind workspaceKind,
            int depth,
            ISet<string> resolvingGroups,
            TriggerAuthoringDryRunEntry entry)
        {
            var unknown = false;
            var evaluated = false;
            var children = node.Children;
            if (children != null)
                for (var i = 0; i < children.Count; i++)
                {
                    var value = TraceCondition(
                        context,
                        children[i],
                        path + ".children[" + i + "]",
                        workspaceKind,
                        depth + 1,
                        resolvingGroups);
                    if (value == Truth.Skipped) continue;
                    evaluated = true;
                    if (value == Truth.False)
                    {
                        TraceRemainingSkippedConditions(context, children, i + 1, path, workspaceKind, depth + 1);
                        return SetCondition(entry, Truth.False, "Short-circuited after a failed child.");
                    }
                    if (value == Truth.Unknown) unknown = true;
                }
            return SetCondition(entry, unknown ? Truth.Unknown : Truth.True,
                evaluated ? "All enabled children passed." : "No enabled child conditions.");
        }

        private static Truth TraceAny(
            EvaluationContext context,
            TriggerNodeData node,
            string path,
            TriggerNodeKind workspaceKind,
            int depth,
            ISet<string> resolvingGroups,
            TriggerAuthoringDryRunEntry entry)
        {
            var unknown = false;
            var children = node.Children;
            if (children != null)
                for (var i = 0; i < children.Count; i++)
                {
                    var value = TraceCondition(
                        context,
                        children[i],
                        path + ".children[" + i + "]",
                        workspaceKind,
                        depth + 1,
                        resolvingGroups);
                    if (value == Truth.True)
                    {
                        TraceRemainingSkippedConditions(context, children, i + 1, path, workspaceKind, depth + 1);
                        return SetCondition(entry, Truth.True, "Short-circuited after a passed child.");
                    }
                    if (value == Truth.Unknown) unknown = true;
                }
            return SetCondition(entry, unknown ? Truth.Unknown : Truth.False,
                unknown ? "No child passed; at least one result is unknown." : "No enabled child passed.");
        }

        private static Truth TraceNot(
            EvaluationContext context,
            TriggerNodeData node,
            string path,
            TriggerNodeKind workspaceKind,
            int depth,
            ISet<string> resolvingGroups,
            TriggerAuthoringDryRunEntry entry)
        {
            var children = node.Children;
            if (children == null) return SetCondition(entry, Truth.Unknown, "Missing child condition.");
            for (var i = 0; i < children.Count; i++)
            {
                var value = TraceCondition(
                    context,
                    children[i],
                    path + ".children[" + i + "]",
                    workspaceKind,
                    depth + 1,
                    resolvingGroups);
                if (value == Truth.Skipped) continue;
                TraceRemainingSkippedConditions(context, children, i + 1, path, workspaceKind, depth + 1);
                if (value == Truth.True) return SetCondition(entry, Truth.False, "Negated true to false.");
                if (value == Truth.False) return SetCondition(entry, Truth.True, "Negated false to true.");
                return SetCondition(entry, Truth.Unknown, "Child result is unknown.");
            }
            return SetCondition(entry, Truth.Unknown, "Missing enabled child condition.");
        }

        private static Truth TraceComparison(
            EvaluationContext context,
            TriggerNodeData node,
            string type,
            string leftName,
            string rightName,
            TriggerAuthoringDryRunEntry entry)
        {
            var leftArgument = FindArgument(node, leftName);
            var rightArgument = FindArgument(node, rightName);
            if (!TryResolveValue(context, leftArgument?.Value, out var left, out var leftError))
                return SetCondition(entry, Truth.Unknown, leftName + ": " + leftError);
            if (!TryResolveValue(context, rightArgument?.Value, out var right, out var rightError))
                return SetCondition(entry, Truth.Unknown, rightName + ": " + rightError);

            if (!TryCompare(type, left, right, out var passed, out var compareError))
                return SetCondition(entry, Truth.Unknown, compareError);
            return SetCondition(
                entry,
                passed ? Truth.True : Truth.False,
                FormatValue(left) + " " + ComparisonSymbol(type) + " " + FormatValue(right));
        }

        private static void TraceAction(
            EvaluationContext context,
            TriggerNodeData node,
            string path,
            int depth,
            ActionGate gate,
            ISet<string> resolvingGroups)
        {
            if (node == null) return;
            var entry = AddEntry(context, node, path, TriggerNodeKind.Action, TriggerNodeKind.Action, depth);
            if (!node.Enabled)
            {
                entry.State = TriggerAuthoringDryRunState.Disabled;
                entry.Message = "Disabled action.";
                TraceSkippedChildren(context, node, path, TriggerNodeKind.Action, depth + 1);
                return;
            }
            if (gate == ActionGate.Skipped)
            {
                entry.State = TriggerAuthoringDryRunState.Skipped;
                entry.Message = "Entry condition or branch was not satisfied.";
                TraceSkippedChildren(context, node, path, TriggerNodeKind.Action, depth + 1);
                return;
            }

            if (!string.IsNullOrWhiteSpace(node.GroupReference))
            {
                SetActionState(entry, gate, "Resolved group: " + node.GroupReference);
                if (!resolvingGroups.Add(node.GroupReference))
                {
                    entry.State = TriggerAuthoringDryRunState.Error;
                    entry.Message = "Reusable action group cycle detected.";
                    return;
                }
                try
                {
                    if (TriggerAuthoringGroupResolver.TryExpand(
                            context.Module,
                            node,
                            TriggerNodeKind.Action,
                            out var expanded,
                            out var failure) && expanded != null)
                        TraceAction(context, expanded, path + ".resolved", depth + 1, gate, resolvingGroups);
                    else
                    {
                        entry.State = TriggerAuthoringDryRunState.Error;
                        entry.Message = failure?.Message ?? "Reusable action group is missing.";
                    }
                }
                finally
                {
                    resolvingGroups.Remove(node.GroupReference);
                }
                return;
            }

            if (TriggerAuthoringTriggerReuse.TryGetReferencedTriggerId(node, out var referencedTriggerId))
            {
                SetActionState(entry, gate, "Would invoke trigger #" + referencedTriggerId + ".");
                return;
            }

            var type = (node.Type ?? string.Empty).Trim().ToLowerInvariant();
            SetActionState(entry, gate, ActionMessage(type, gate));
            if (string.Equals(type, "conditional", StringComparison.Ordinal))
            {
                var truth = TraceCondition(
                    context,
                    node.Condition,
                    path + ".condition",
                    TriggerNodeKind.Action,
                    depth + 1,
                    new HashSet<string>(StringComparer.Ordinal));
                TraceConditionalBranches(context, node, path, depth, gate, truth, resolvingGroups);
                return;
            }

            if (string.Equals(type, "until", StringComparison.Ordinal))
            {
                TraceCondition(
                    context,
                    node.Condition,
                    path + ".condition",
                    TriggerNodeKind.Action,
                    depth + 1,
                    new HashSet<string>(StringComparer.Ordinal));
                TraceActionChildren(context, node.Children, path + ".children", depth + 1, ActionGate.Potential, resolvingGroups);
                TraceActionChildren(context, node.ElseChildren, path + ".elseChildren", depth + 1, ActionGate.Potential, resolvingGroups);
                return;
            }

            if (string.Equals(type, "random", StringComparison.Ordinal) ||
                string.Equals(type, "selector", StringComparison.Ordinal) ||
                string.Equals(type, "repeat", StringComparison.Ordinal) ||
                string.Equals(type, "for_each", StringComparison.Ordinal) ||
                string.Equals(type, "scheduled", StringComparison.Ordinal))
            {
                TraceActionChildren(context, node.Children, path + ".children", depth + 1, ActionGate.Potential, resolvingGroups);
                TraceActionChildren(context, node.ElseChildren, path + ".elseChildren", depth + 1, ActionGate.Potential, resolvingGroups);
                return;
            }

            TraceActionChildren(context, node.Children, path + ".children", depth + 1, gate, resolvingGroups);
            TraceActionChildren(context, node.ElseChildren, path + ".elseChildren", depth + 1, gate, resolvingGroups);
        }

        private static void TraceConditionalBranches(
            EvaluationContext context,
            TriggerNodeData node,
            string path,
            int depth,
            ActionGate parentGate,
            Truth truth,
            ISet<string> resolvingGroups)
        {
            ActionGate thenGate;
            ActionGate elseGate;
            if (parentGate == ActionGate.Potential || truth == Truth.Unknown || truth == Truth.Skipped)
            {
                thenGate = elseGate = ActionGate.Potential;
            }
            else if (truth == Truth.True)
            {
                thenGate = ActionGate.Active;
                elseGate = ActionGate.Skipped;
            }
            else
            {
                thenGate = ActionGate.Skipped;
                elseGate = ActionGate.Active;
            }
            TraceActionChildren(context, node.Children, path + ".children", depth + 1, thenGate, resolvingGroups);
            TraceActionChildren(context, node.ElseChildren, path + ".elseChildren", depth + 1, elseGate, resolvingGroups);
        }

        private static void TraceActionChildren(
            EvaluationContext context,
            IReadOnlyList<TriggerNodeData> children,
            string path,
            int depth,
            ActionGate gate,
            ISet<string> resolvingGroups)
        {
            if (children == null) return;
            for (var i = 0; i < children.Count; i++)
                TraceAction(context, children[i], path + "[" + i + "]", depth, gate, resolvingGroups);
        }

        private static void TraceSkippedChildren(
            EvaluationContext context,
            TriggerNodeData node,
            string path,
            TriggerNodeKind workspaceKind,
            int depth)
        {
            if (node.Condition != null)
                TraceSkippedNode(context, node.Condition, path + ".condition", TriggerNodeKind.Condition, workspaceKind, depth);
            TraceSkippedNodes(context, node.Children, path + ".children", node.Kind, workspaceKind, depth);
            TraceSkippedNodes(context, node.ElseChildren, path + ".elseChildren", node.Kind, workspaceKind, depth);
        }

        private static void TraceRemainingSkippedConditions(
            EvaluationContext context,
            IReadOnlyList<TriggerNodeData> children,
            int start,
            string parentPath,
            TriggerNodeKind workspaceKind,
            int depth)
        {
            if (children == null) return;
            for (var i = start; i < children.Count; i++)
                TraceSkippedNode(
                    context,
                    children[i],
                    parentPath + ".children[" + i + "]",
                    TriggerNodeKind.Condition,
                    workspaceKind,
                    depth);
        }

        private static void TraceSkippedNodes(
            EvaluationContext context,
            IReadOnlyList<TriggerNodeData> children,
            string path,
            TriggerNodeKind kind,
            TriggerNodeKind workspaceKind,
            int depth)
        {
            if (children == null) return;
            for (var i = 0; i < children.Count; i++)
                TraceSkippedNode(context, children[i], path + "[" + i + "]", kind, workspaceKind, depth);
        }

        private static void TraceSkippedNode(
            EvaluationContext context,
            TriggerNodeData node,
            string path,
            TriggerNodeKind kind,
            TriggerNodeKind workspaceKind,
            int depth)
        {
            if (node == null) return;
            var entry = AddEntry(context, node, path, kind, workspaceKind, depth);
            entry.State = node.Enabled ? TriggerAuthoringDryRunState.Skipped : TriggerAuthoringDryRunState.Disabled;
            entry.Message = node.Enabled ? "Skipped by short-circuit or branch selection." : "Disabled node.";
            TraceSkippedChildren(context, node, path, workspaceKind, depth + 1);
        }

        private static TriggerAuthoringDryRunEntry AddEntry(
            EvaluationContext context,
            TriggerNodeData node,
            string path,
            TriggerNodeKind kind,
            TriggerNodeKind workspaceKind,
            int depth)
        {
            var entry = new TriggerAuthoringDryRunEntry
            {
                NodeId = node.NodeId,
                Path = path,
                Kind = kind,
                WorkspaceKind = workspaceKind,
                Type = string.IsNullOrWhiteSpace(node.GroupReference) ? node.Type : "group:" + node.GroupReference,
                Depth = depth,
                State = TriggerAuthoringDryRunState.Unknown
            };
            context.Result.Entries.Add(entry);
            return entry;
        }

        private static Truth SetCondition(TriggerAuthoringDryRunEntry entry, Truth truth, string message)
        {
            entry.State = ToConditionState(truth);
            entry.Message = message;
            return truth;
        }

        private static TriggerAuthoringDryRunState ToConditionState(Truth truth)
        {
            switch (truth)
            {
                case Truth.True: return TriggerAuthoringDryRunState.Passed;
                case Truth.False: return TriggerAuthoringDryRunState.Failed;
                case Truth.Skipped: return TriggerAuthoringDryRunState.Skipped;
                default: return TriggerAuthoringDryRunState.Unknown;
            }
        }

        private static void SetActionState(
            TriggerAuthoringDryRunEntry entry,
            ActionGate gate,
            string message)
        {
            entry.State = gate == ActionGate.Active
                ? TriggerAuthoringDryRunState.WouldExecute
                : gate == ActionGate.Potential
                    ? TriggerAuthoringDryRunState.Potential
                    : TriggerAuthoringDryRunState.Skipped;
            entry.Message = message;
        }

        private static string ActionMessage(string type, ActionGate gate)
        {
            if (gate == ActionGate.Potential) return "May execute; no side effects were run.";
            switch (type)
            {
                case "seq": return "Would execute children in order.";
                case "selector": return "Would try children until one succeeds.";
                case "random": return "Would select one child at runtime.";
                case "parallel": return "Would execute all children in parallel.";
                case "repeat": return "Would repeat child execution at runtime.";
                case "until": return "Would repeat until the end condition passes or the iteration limit is reached.";
                case "for_each": return "Would iterate at runtime.";
                case "scheduled": return "Would schedule child execution at runtime.";
                case "conditional": return "Would evaluate the branch condition.";
                case "invert": return "Would execute the child and invert its result.";
                case "succeed": return "Would execute the child and return success.";
                case "fail": return "Would execute the child and return failure.";
                default: return "Would execute; no side effects were run.";
            }
        }

        private static bool TryResolveValue(
            EvaluationContext context,
            TriggerValueRefData value,
            out object resolved,
            out string error)
        {
            resolved = null;
            error = null;
            if (value == null)
            {
                error = "value is missing";
                return false;
            }
            switch (value.Source)
            {
                case TriggerValueSource.Constant:
                    resolved = ConstantValue(value);
                    return true;
                case TriggerValueSource.Payload:
                    return TryReadJson(context.Input.Payload, value.Path, out resolved, out error);
                case TriggerValueSource.Context:
                    return TryReadJson(context.Input.Context, value.Path, out resolved, out error);
                case TriggerValueSource.LocalBlackboard:
                    if (TryReadJson(context.Input.Local, value.Path, out resolved, out _)) return true;
                    if (TriggerAuthoringLocalBlackboardPath.TryParse(value.Path, out var scope, out var key) &&
                        TryReadJson(context.Input.Local, key, out resolved, out _)) return true;
                    if (TryResolveLocalDefault(context, scope, key, out resolved)) return true;
                    error = "local value '" + (value.Path ?? string.Empty) + "' was not provided";
                    return false;
                case TriggerValueSource.GlobalBlackboard:
                    if (TryReadJson(context.Input.Global, value.Path, out resolved, out _)) return true;
                    error = "global value '" + (value.Path ?? string.Empty) + "' was not provided";
                    return false;
                case TriggerValueSource.TemplateParameter:
                    error = "template parameter was not materialized";
                    return false;
                case TriggerValueSource.Expression:
                    error = "expressions require the runtime evaluator";
                    return false;
                default:
                    error = "unsupported value source: " + value.Source;
                    return false;
            }
        }

        private static bool TryResolveLocalDefault(
            EvaluationContext context,
            TriggerAuthoringLocalBlackboardScope scope,
            string key,
            out object value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(key)) return false;
            var variables = scope == TriggerAuthoringLocalBlackboardScope.Module
                ? context.Module.Blackboard
                : context.Trigger.Blackboard;
            if (variables == null) return false;
            for (var i = 0; i < variables.Count; i++)
            {
                var variable = variables[i];
                if (variable == null || !string.Equals(variable.Key, key, StringComparison.Ordinal)) continue;
                if (variable.DefaultValue?.Source != TriggerValueSource.Constant) return false;
                value = ConstantValue(variable.DefaultValue);
                return true;
            }
            return false;
        }

        private static object ConstantValue(TriggerValueRefData value)
        {
            switch (value.Type)
            {
                case TriggerValueType.Integer: return value.IntegerValue;
                case TriggerValueType.Number: return value.NumberValue;
                case TriggerValueType.Boolean: return value.BooleanValue;
                case TriggerValueType.String:
                case TriggerValueType.Entity:
                case TriggerValueType.ObjectId: return value.StringValue;
                case TriggerValueType.IntegerList: return value.IntegerListValue;
                case TriggerValueType.Vector3: return value.Vector3Value;
                case TriggerValueType.Object: return value.Fields;
                default: return value.StringValue;
            }
        }

        private static bool TryReadJson(JObject root, string path, out object value, out string error)
        {
            value = null;
            error = null;
            if (root == null || string.IsNullOrWhiteSpace(path))
            {
                error = "path is empty";
                return false;
            }
            if (root.TryGetValue(path, StringComparison.Ordinal, out var direct))
            {
                value = ConvertToken(direct);
                return true;
            }

            JToken current = root;
            var segments = path.Split(new[] { '.', '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < segments.Length; i++)
            {
                if (!(current is JObject currentObject) ||
                    !currentObject.TryGetValue(segments[i], StringComparison.Ordinal, out current))
                {
                    error = "value '" + path + "' was not provided";
                    return false;
                }
            }
            value = ConvertToken(current);
            return true;
        }

        private static object ConvertToken(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            if (token is JValue scalar) return scalar.Value;
            return token.ToObject<object>();
        }

        private static bool TryCompare(
            string type,
            object left,
            object right,
            out bool result,
            out string error)
        {
            result = false;
            error = null;
            var equals = ValuesEqual(left, right);
            if (type.EndsWith("_eq", StringComparison.Ordinal))
            {
                result = equals;
                return true;
            }
            if (type.EndsWith("_neq", StringComparison.Ordinal))
            {
                result = !equals;
                return true;
            }
            if (!TryNumber(left, out var leftNumber) || !TryNumber(right, out var rightNumber))
            {
                error = "numeric comparison requires two numbers";
                return false;
            }
            if (type.EndsWith("_gt", StringComparison.Ordinal)) result = leftNumber > rightNumber;
            else if (type.EndsWith("_gte", StringComparison.Ordinal) || type.EndsWith("_geq", StringComparison.Ordinal))
                result = leftNumber >= rightNumber;
            else if (type.EndsWith("_lt", StringComparison.Ordinal)) result = leftNumber < rightNumber;
            else if (type.EndsWith("_lte", StringComparison.Ordinal) || type.EndsWith("_leq", StringComparison.Ordinal))
                result = leftNumber <= rightNumber;
            else
            {
                error = "unsupported comparison: " + type;
                return false;
            }
            return true;
        }

        private static bool ValuesEqual(object left, object right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null) return false;
            if (TryNumber(left, out var leftNumber) && TryNumber(right, out var rightNumber))
                return Math.Abs(leftNumber - rightNumber) <= 0.000001d;
            return string.Equals(
                Convert.ToString(left, CultureInfo.InvariantCulture),
                Convert.ToString(right, CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
        }

        private static bool TryNumber(object value, out double number)
        {
            if (value is bool)
            {
                number = 0d;
                return false;
            }
            if (value is IConvertible)
                return double.TryParse(
                    Convert.ToString(value, CultureInfo.InvariantCulture),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out number);
            number = 0d;
            return false;
        }

        private static string ComparisonSymbol(string type)
        {
            if (type.EndsWith("_neq", StringComparison.Ordinal)) return "!=";
            if (type.EndsWith("_gte", StringComparison.Ordinal) || type.EndsWith("_geq", StringComparison.Ordinal)) return ">=";
            if (type.EndsWith("_lte", StringComparison.Ordinal) || type.EndsWith("_leq", StringComparison.Ordinal)) return "<=";
            if (type.EndsWith("_gt", StringComparison.Ordinal)) return ">";
            if (type.EndsWith("_lt", StringComparison.Ordinal)) return "<";
            return "==";
        }

        private static string FormatValue(object value)
        {
            if (value == null) return "null";
            if (value is string text) return "\"" + text + "\"";
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static TriggerArgumentData FindArgument(TriggerNodeData node, string name)
        {
            var arguments = node?.Arguments;
            if (arguments == null) return null;
            for (var i = 0; i < arguments.Count; i++)
                if (string.Equals(arguments[i]?.Name, name, StringComparison.Ordinal)) return arguments[i];
            return null;
        }
    }
}
#endif
