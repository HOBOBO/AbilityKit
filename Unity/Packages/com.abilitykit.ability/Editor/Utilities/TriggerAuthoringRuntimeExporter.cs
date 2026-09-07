#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Editor.Platform.Export;
using AbilityKit.Triggering.Blackboard;
using Newtonsoft.Json;
using RuntimeStableStringId = AbilityKit.Triggering.Eventing.StableStringId;

namespace AbilityKit.Ability.Editor.Utilities
{
    [Serializable]
    internal sealed class TriggerAuthoringRuntimeDatabaseDto
    {
        public int FormatVersion = 1;
        public List<TriggerAuthoringRuntimeTriggerDto> Triggers = new List<TriggerAuthoringRuntimeTriggerDto>();
        public Dictionary<int, string> Strings = new Dictionary<int, string>();
        public List<BlackboardInitializationPlan> Blackboards;
    }

    [Serializable]
    internal sealed class TriggerAuthoringRuntimeTriggerDto
    {
        public int TriggerId;
        public string EventName;
        public int EventId;
        public bool AllowExternal;
        public int Phase;
        public int Priority;
        public int Scope;
        public TriggerAuthoringRuntimeTemplateBindingDto Template;
        public TriggerAuthoringRuntimePredicateDto Predicate;
        public List<TriggerAuthoringRuntimeActionDto> Actions;
        public string CueId;
    }

    [Serializable]
    internal sealed class TriggerAuthoringRuntimeTemplateBindingDto
    {
        public string TemplateId;
        public Dictionary<string, TriggerAuthoringRuntimeValueRefDto> Bindings;
    }

    [Serializable]
    internal sealed class TriggerAuthoringRuntimePredicateDto
    {
        public string Kind;
        public List<TriggerAuthoringRuntimeBoolNodeDto> Nodes;
    }

    [Serializable]
    internal sealed class TriggerAuthoringRuntimeBoolNodeDto
    {
        public string Kind;
        public bool ConstValue;
        public string CompareOp;
        public TriggerAuthoringRuntimeValueRefDto Left;
        public TriggerAuthoringRuntimeValueRefDto Right;
        public int FunctionId;
        public int FunctionArity;
    }

    [Serializable]
    internal sealed class TriggerAuthoringRuntimeActionDto
    {
        public int ActionId;
        public int Arity;
        public Dictionary<string, TriggerAuthoringRuntimeValueRefDto> Args;
    }

    [Serializable]
    internal sealed class TriggerAuthoringRuntimeValueRefDto
    {
        public string Kind;
        public double ConstValue;
        public int BoardId;
        public int KeyId;
        public int FieldId;
        public string DomainId;
        public string Key;
        public string ExprText;
        public string Scope;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool BoolValue;
        public string StringValue;
        public bool HasScale;
        public double Scale = 1d;
        public BlackboardKeyType? KeyType;
    }

    internal sealed class TriggerAuthoringRuntimeCompileResult
    {
        public TriggerAuthoringRuntimeDatabaseDto Database;
        public List<TriggerAuthoringDiagnostic> Diagnostics = new List<TriggerAuthoringDiagnostic>();
        public int ExportedTriggerCount;
        public int SkippedDisabledCount;

        public bool Success => Database != null && !TriggerAuthoringValidator.HasErrors(Diagnostics);

        public string BuildMessage()
        {
            if (Success)
                return $"Exported {ExportedTriggerCount} trigger(s); skipped {SkippedDisabledCount} disabled trigger(s).";

            var builder = new StringBuilder();
            for (var i = 0; i < Diagnostics.Count; i++)
            {
                var diagnostic = Diagnostics[i];
                if (diagnostic.Severity != TriggerAuthoringDiagnosticSeverity.Error) continue;
                if (builder.Length > 0) builder.AppendLine();
                builder.Append(diagnostic.Code).Append(' ').Append(diagnostic.Path).Append(": ").Append(diagnostic.Message);
            }
            return builder.Length > 0 ? builder.ToString() : "Runtime Plan export failed.";
        }
    }

    internal static class TriggerAuthoringRuntimeExporter
    {
        private sealed class RuntimeTriggerCompileContext
        {
            public readonly TriggerAuthoringModuleData Module;
            public readonly TriggerDefinitionData Trigger;
            public readonly bool IsOwnerBound;
            public readonly List<BlackboardInitializationPlan> Blackboards;

            public RuntimeTriggerCompileContext(
                TriggerAuthoringModuleData module,
                TriggerDefinitionData trigger,
                bool isOwnerBound,
                List<BlackboardInitializationPlan> blackboards)
            {
                Module = module;
                Trigger = trigger;
                IsOwnerBound = isOwnerBound;
                Blackboards = blackboards ?? throw new ArgumentNullException(nameof(blackboards));
            }
        }

        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            Culture = CultureInfo.InvariantCulture
        };

        public static TriggerAuthoringRuntimeCompileResult Build(
            TriggerAuthoringModuleAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            return Build(asset.Module, TriggerAuthoringValidationContext.Create(asset));
        }

        public static TriggerAuthoringRuntimeCompileResult Build(
            TriggerAuthoringModuleData module,
            TriggerAuthoringValidationContext context = null)
        {
            context = context ?? new TriggerAuthoringValidationContext();
            context.Types = context.Types ?? TriggerTypeDescriptorCatalog.CreateProjectDefaults();
            var result = new TriggerAuthoringRuntimeCompileResult();
            result.Diagnostics.AddRange(TriggerAuthoringValidator.Validate(module, context));
            if (TriggerAuthoringValidator.HasErrors(result.Diagnostics)) return result;

            var database = new TriggerAuthoringRuntimeDatabaseDto();
            var strings = new SortedDictionary<int, string>();
            var blackboards = CompileGlobalBlackboards(context?.GlobalBlackboard, result.Diagnostics);
            var triggers = module.Triggers ?? new List<TriggerDefinitionData>();
            for (var i = 0; i < triggers.Count; i++)
            {
                var trigger = triggers[i];
                if (trigger == null) continue;
                if (!trigger.Enabled)
                {
                    result.SkippedDisabledCount++;
                    continue;
                }

                var path = $"module.triggers[{i}]";
                var triggerDto = CompileTrigger(module, trigger, path, context, strings, blackboards, result.Diagnostics);
                if (triggerDto != null)
                {
                    database.Triggers.Add(triggerDto);
                    result.ExportedTriggerCount++;
                }
            }

            if (TriggerAuthoringValidator.HasErrors(result.Diagnostics)) return result;
            foreach (var pair in strings) database.Strings.Add(pair.Key, pair.Value);
            if (blackboards.Count > 0)
            {
                blackboards.Sort((left, right) => left.BoardId.CompareTo(right.BoardId));
                database.Blackboards = blackboards;
            }
            result.Database = database;
            return result;
        }

        public static string Serialize(TriggerAuthoringRuntimeDatabaseDto database)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            return JsonConvert.SerializeObject(database, JsonSettings) + Environment.NewLine;
        }

        public static TriggerAuthoringRuntimeCompileResult Export(
            TriggerAuthoringModuleAsset asset,
            string path)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Runtime Plan path is required.", nameof(path));

            var result = Build(asset);
            if (!result.Success) return result;
            WriteFileAtomic(path, Serialize(result.Database));
            return result;
        }

        private static TriggerAuthoringRuntimeTriggerDto CompileTrigger(
            TriggerAuthoringModuleData module,
            TriggerDefinitionData trigger,
            string path,
            TriggerAuthoringValidationContext context,
            SortedDictionary<int, string> strings,
            List<BlackboardInitializationPlan> blackboards,
            ICollection<TriggerAuthoringDiagnostic> diagnostics)
        {
            ValidateRuntimeOnlyFields(trigger, path, diagnostics);
            if (!TryParsePhase(trigger.Phase, out var phase))
                AddError(diagnostics, "TRG2001", path + ".phase", $"Runtime Plan does not support phase '{trigger.Phase ?? string.Empty}'.");
            if (!TryParseScope(trigger.Scope, out var scope))
                AddError(diagnostics, "TRG2002", path + ".scope", $"Runtime Plan does not support scope '{trigger.Scope ?? string.Empty}'.");

            TriggerAuthoringTemplateData templateDefinition = null;
            var conditionSource = trigger.Condition;
            var actionsSource = trigger.Actions;
            if (trigger.Template != null && context?.Templates != null &&
                context.Templates.TryGet(trigger.Template.TemplateId, out var templateAsset) &&
                templateAsset?.Template != null)
            {
                templateDefinition = templateAsset.Template;
                conditionSource = templateDefinition.Condition;
                actionsSource = templateDefinition.Actions;
            }

            TriggerNodeData condition = null;
            if (conditionSource != null && !TriggerAuthoringGroupResolver.TryExpand(
                    module, conditionSource, TriggerNodeKind.Condition, out condition, out var conditionFailure))
            {
                AddError(diagnostics, "TRG2003", path + ".condition", conditionFailure?.Message ?? "Condition group expansion failed.");
            }

            TriggerNodeData actions = null;
            if (actionsSource != null && !TriggerAuthoringGroupResolver.TryExpand(
                    module, actionsSource, TriggerNodeKind.Action, out actions, out var actionFailure))
            {
                AddError(diagnostics, "TRG2004", path + ".actions", actionFailure?.Message ?? "Action group expansion failed.");
            }

            var compileContext = new RuntimeTriggerCompileContext(module, trigger, scope == 1, blackboards);
            var predicate = CompilePredicate(compileContext, condition, path + ".condition", context, strings, diagnostics);
            var actionList = new List<TriggerAuthoringRuntimeActionDto>();
            CompileActions(compileContext, actions, path + ".actions", context, strings, diagnostics, actionList);
            if (actionList.Count == 0)
                AddError(diagnostics, "TRG2005", path + ".actions", "An enabled trigger must compile at least one action.");

            var template = CompileTemplate(
                compileContext,
                trigger.Template,
                templateDefinition,
                path + ".template",
                context,
                strings,
                diagnostics);
            if (HasErrorsAtOrBelow(diagnostics, path)) return null;

            return new TriggerAuthoringRuntimeTriggerDto
            {
                TriggerId = trigger.Id,
                EventName = trigger.Event,
                EventId = RuntimeStableStringId.Get("event:" + trigger.Event),
                AllowExternal = trigger.AllowExternal,
                Phase = phase,
                Priority = trigger.Priority,
                Scope = scope,
                Template = template,
                Predicate = predicate,
                Actions = actionList,
                CueId = string.IsNullOrWhiteSpace(trigger.Cue?.CueId) ? null : trigger.Cue.CueId
            };
        }

        private static void ValidateRuntimeOnlyFields(
            TriggerDefinitionData trigger,
            string path,
            ICollection<TriggerAuthoringDiagnostic> diagnostics)
        {
            if (trigger.InterruptPriority != 0)
                AddError(diagnostics, "TRG2010", path + ".interruptPriority", "The current Runtime Plan JSON contract does not carry trigger InterruptPriority.");

            var schedule = trigger.Schedule;
            if (schedule != null &&
                (!string.IsNullOrWhiteSpace(schedule.Mode) && !string.Equals(schedule.Mode, "transient", StringComparison.OrdinalIgnoreCase) ||
                 schedule.DelayMilliseconds != 0 || schedule.IntervalMilliseconds != 0 || schedule.RepeatCount != 0))
            {
                AddError(diagnostics, "TRG2011", path + ".schedule", "Trigger-level Schedule cannot be represented by the current Runtime Plan JSON contract.");
            }

            var control = trigger.ExecutionControl;
            if (control != null &&
                (!string.IsNullOrWhiteSpace(control.InterruptPolicy) && !string.Equals(control.InterruptPolicy, "none", StringComparison.OrdinalIgnoreCase) ||
                 control.StopPropagationOnSuccess || control.StopPropagationOnFailure))
            {
                AddError(diagnostics, "TRG2012", path + ".executionControl", "Interrupt/propagation controls cannot be represented by the current Runtime Plan JSON contract.");
            }
        }

        private static TriggerAuthoringRuntimePredicateDto CompilePredicate(
            RuntimeTriggerCompileContext compileContext,
            TriggerNodeData root,
            string path,
            TriggerAuthoringValidationContext context,
            SortedDictionary<int, string> strings,
            ICollection<TriggerAuthoringDiagnostic> diagnostics)
        {
            if (root == null) return new TriggerAuthoringRuntimePredicateDto { Kind = "none" };
            var nodes = new List<TriggerAuthoringRuntimeBoolNodeDto>();
            CompileConditionNode(compileContext, root, path, context, strings, diagnostics, nodes);
            return new TriggerAuthoringRuntimePredicateDto { Kind = "expr", Nodes = nodes };
        }

        private static void CompileConditionNode(
            RuntimeTriggerCompileContext compileContext,
            TriggerNodeData node,
            string path,
            TriggerAuthoringValidationContext context,
            SortedDictionary<int, string> strings,
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            ICollection<TriggerAuthoringRuntimeBoolNodeDto> output)
        {
            if (node == null || !node.Enabled) return;
            var type = (node.Type ?? string.Empty).Trim().ToLowerInvariant();
            switch (type)
            {
                case "all":
                case "any":
                    var emitted = 0;
                    for (var i = 0; i < node.Children.Count; i++)
                    {
                        if (node.Children[i] == null || !node.Children[i].Enabled) continue;
                        CompileConditionNode(compileContext, node.Children[i], $"{path}.children[{i}]", context, strings, diagnostics, output);
                        if (emitted > 0) output.Add(new TriggerAuthoringRuntimeBoolNodeDto { Kind = type == "all" ? "And" : "Or" });
                        emitted++;
                    }
                    return;
                case "not":
                    var childIndex = FirstEnabledChildIndex(node.Children);
                    if (childIndex < 0) return;
                    CompileConditionNode(compileContext, node.Children[childIndex], $"{path}.children[{childIndex}]", context, strings, diagnostics, output);
                    output.Add(new TriggerAuthoringRuntimeBoolNodeDto { Kind = "Not" });
                    return;
                case "always_true":
                    output.Add(new TriggerAuthoringRuntimeBoolNodeDto { Kind = "Const", ConstValue = true });
                    return;
                case "always_false":
                    output.Add(new TriggerAuthoringRuntimeBoolNodeDto { Kind = "Const", ConstValue = false });
                    return;
                case "arg_eq":
                case "arg_neq":
                case "arg_gt":
                case "arg_gte":
                case "arg_geq":
                case "arg_lt":
                case "arg_lte":
                case "arg_leq":
                    CompileComparison(compileContext, node, path, context, strings, diagnostics, output, "left", "right", CompareOp(type));
                    return;
                case "num_var_eq":
                case "num_var_gt":
                case "num_var_lt":
                    CompileComparison(compileContext, node, path, context, strings, diagnostics, output, "variable", "value", CompareOp(type));
                    return;
                case "has_buff":
                    CompileHasBuff(compileContext, node, path, context, strings, diagnostics, output);
                    return;
                case "health_percent":
                    CompileHealthPercent(node, path, diagnostics, output);
                    return;
                case "owner_matches_payload_source":
                    AddFunction(output, "predicate:owner_matches_payload_source", Const(0), Const(0));
                    return;
                case "owner_matches_payload_target":
                    AddFunction(output, "predicate:owner_matches_payload_target", Const(0), Const(0));
                    return;
                case "target_is_flying_projectile":
                    AddFunction(output, "predicate:target_is_flying_projectile", Const(0), Const(0));
                    return;
                default:
                    AddError(diagnostics, "TRG2020", path + ".type", $"Condition '{node.Type ?? string.Empty}' has no Runtime Plan compiler.");
                    return;
            }
        }

        private static void CompileComparison(
            RuntimeTriggerCompileContext compileContext,
            TriggerNodeData node,
            string path,
            TriggerAuthoringValidationContext context,
            SortedDictionary<int, string> strings,
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            ICollection<TriggerAuthoringRuntimeBoolNodeDto> output,
            string leftName,
            string rightName,
            string op)
        {
            var left = FindArgument(node, leftName);
            var right = FindArgument(node, rightName);
            var leftRef = CompileValue(compileContext, left?.Value, path + ".arguments." + leftName, context, strings, diagnostics, false);
            var rightRef = CompileValue(compileContext, right?.Value, path + ".arguments." + rightName, context, strings, diagnostics, false);
            if (leftRef == null || rightRef == null) return;
            output.Add(new TriggerAuthoringRuntimeBoolNodeDto
            {
                Kind = "CompareNumeric",
                CompareOp = op,
                Left = leftRef,
                Right = rightRef
            });
        }

        private static void CompileHasBuff(
            RuntimeTriggerCompileContext compileContext,
            TriggerNodeData node,
            string path,
            TriggerAuthoringValidationContext context,
            SortedDictionary<int, string> strings,
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            ICollection<TriggerAuthoringRuntimeBoolNodeDto> output)
        {
            var buffIdMatch = TriggerAuthoringArgumentPathResolver.FindValue(
                diagnostics,
                node,
                path,
                true,
                "TRG2080",
                "Runtime Plan export can read Object fields only when the Object value is a constant field container.",
                "buff_id",
                "buff.id",
                "buff.buff_id",
                "options.id",
                "options.buff_id");
            var checkStackMatch = TriggerAuthoringArgumentPathResolver.FindValue(
                diagnostics,
                node,
                path,
                true,
                "TRG2080",
                "Runtime Plan export can read Object fields only when the Object value is a constant field container.",
                "check_stack",
                "options.check_stack");
            var targetModeMatch = TriggerAuthoringArgumentPathResolver.FindValue(
                diagnostics,
                node,
                path,
                true,
                "TRG2080",
                "Runtime Plan export can read Object fields only when the Object value is a constant field container.",
                "target_mode",
                "options.target_mode",
                "target.mode",
                "target.target_mode");
            var buffId = CompileValue(
                compileContext,
                buffIdMatch?.Value,
                path + (buffIdMatch?.PathSuffix ?? ".arguments.buff_id"),
                context,
                strings,
                diagnostics,
                false);
            var checkStack = checkStackMatch == null
                ? Const(0)
                : CompileValue(
                    compileContext,
                    checkStackMatch.Value,
                    path + checkStackMatch.PathSuffix,
                    context,
                    strings,
                    diagnostics,
                    false) ?? Const(0);
            var targetMode = targetModeMatch?.Value;
            if (targetMode != null && (targetMode.Source != TriggerValueSource.Constant ||
                                      targetMode.Type != TriggerValueType.Integer && targetMode.Type != TriggerValueType.Number))
            {
                AddError(diagnostics, "TRG2021", path + (targetModeMatch?.PathSuffix ?? ".arguments.target_mode"), "has_buff target_mode must be a constant so the runtime predicate can be selected.");
                return;
            }
            var owner = targetMode != null && (targetMode.Type == TriggerValueType.Integer ? targetMode.IntegerValue : targetMode.NumberValue) != 0d;
            if (buffId != null) AddFunction(output, owner ? "predicate:has_buff_owner" : "predicate:has_buff", buffId, checkStack);
        }

        private static void CompileHealthPercent(
            TriggerNodeData node,
            string path,
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            ICollection<TriggerAuthoringRuntimeBoolNodeDto> output)
        {
            var threshold = FindArgument(node, "threshold")?.Value;
            if (threshold == null || threshold.Source != TriggerValueSource.Constant ||
                threshold.Type != TriggerValueType.Integer && threshold.Type != TriggerValueType.Number)
            {
                AddError(diagnostics, "TRG2022", path + ".arguments.threshold", "health_percent threshold must be a numeric constant for Runtime Plan export.");
                return;
            }
            var thresholdValue = threshold.Type == TriggerValueType.Integer ? threshold.IntegerValue : threshold.NumberValue;

            var compareType = FindArgument(node, "compare_type")?.Value;
            if (compareType != null && (compareType.Source != TriggerValueSource.Constant ||
                                        compareType.Type != TriggerValueType.Integer && compareType.Type != TriggerValueType.Number))
            {
                AddError(diagnostics, "TRG2023", path + ".arguments.compare_type", "health_percent compare_type must be a constant.");
                return;
            }
            var compareValue = compareType == null
                ? 0d
                : compareType.Type == TriggerValueType.Integer ? compareType.IntegerValue : compareType.NumberValue;
            if (compareValue != 0d && compareValue != 1d)
            {
                AddError(diagnostics, "TRG2024", path + ".arguments.compare_type", "health_percent compare_type must be 0 (less) or 1 (greater).");
                return;
            }

            output.Add(new TriggerAuthoringRuntimeBoolNodeDto
            {
                Kind = "CompareNumeric",
                CompareOp = compareValue == 0d ? "LessThan" : "GreaterThan",
                Left = new TriggerAuthoringRuntimeValueRefDto
                {
                    Kind = "PayloadField",
                    FieldId = RuntimeStableStringId.Get("payload:target_hp")
                },
                Right = new TriggerAuthoringRuntimeValueRefDto
                {
                    Kind = "PayloadField",
                    FieldId = RuntimeStableStringId.Get("payload:target_max_hp"),
                    HasScale = true,
                    Scale = thresholdValue / 100d
                }
            });
        }

        private static void AddFunction(
            ICollection<TriggerAuthoringRuntimeBoolNodeDto> output,
            string functionKey,
            TriggerAuthoringRuntimeValueRefDto left,
            TriggerAuthoringRuntimeValueRefDto right)
        {
            output.Add(new TriggerAuthoringRuntimeBoolNodeDto
            {
                Kind = "Function",
                FunctionId = RuntimeStableStringId.Get(functionKey),
                FunctionArity = 2,
                Left = left,
                Right = right
            });
        }

        private static void CompileActions(
            RuntimeTriggerCompileContext compileContext,
            TriggerNodeData node,
            string path,
            TriggerAuthoringValidationContext context,
            SortedDictionary<int, string> strings,
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            ICollection<TriggerAuthoringRuntimeActionDto> output)
        {
            if (node == null || !node.Enabled) return;
            if (string.Equals(node.Type, "seq", StringComparison.OrdinalIgnoreCase))
            {
                for (var i = 0; i < node.Children.Count; i++)
                {
                    if (node.Children[i] == null || !node.Children[i].Enabled) continue;
                    CompileActions(compileContext, node.Children[i], $"{path}.children[{i}]", context, strings, diagnostics, output);
                }
                return;
            }

            if (node.Children != null && node.Children.Count > 0)
            {
                AddError(diagnostics, "TRG2030", path + ".children", $"Action '{node.Type ?? string.Empty}' cannot preserve child execution semantics.");
                return;
            }
            TriggerTypeDescriptor descriptor = null;
            context?.Types?.TryGet(TriggerNodeKind.Action, node.Type, out descriptor);
            if (descriptor == null || !descriptor.RuntimeSupported)
            {
                AddError(diagnostics, "TRG2032", path + ".type", $"Action '{node.Type ?? string.Empty}' is not registered by the current project Runtime PlanAction set.");
                return;
            }

            var action = new TriggerAuthoringRuntimeActionDto
            {
                ActionId = RuntimeStableStringId.Get("action:" + node.Type),
                Args = new Dictionary<string, TriggerAuthoringRuntimeValueRefDto>(StringComparer.Ordinal)
            };
            var arguments = new List<TriggerArgumentData>(node.Arguments ?? new List<TriggerArgumentData>());
            arguments.Sort((left, right) => string.Compare(left?.Name, right?.Name, StringComparison.Ordinal));
            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                if (argument == null || string.IsNullOrWhiteSpace(argument.Name)) continue;
                var argumentPath = path + ".arguments." + argument.Name;
                var writeTarget = IsWriteParameter(descriptor, argument.Name);
                if (argument.Value != null && argument.Value.Source == TriggerValueSource.Constant &&
                    argument.Value.Type == TriggerValueType.IntegerList)
                {
                    var values = argument.Value.IntegerListValue;
                    if (values == null || values.Count == 0)
                    {
                        AddError(diagnostics, "TRG2031", argumentPath, "IntegerList constants must contain at least one value for Runtime Plan export.");
                        continue;
                    }
                    for (var valueIndex = 0; valueIndex < values.Count; valueIndex++)
                        AddRuntimeActionArgument(
                            diagnostics,
                            action.Args,
                            argument.Name + valueIndex.ToString(CultureInfo.InvariantCulture),
                            Const(values[valueIndex]),
                            argumentPath);
                    continue;
                }

                if (argument.Value != null && argument.Value.Type == TriggerValueType.Object)
                {
                    CompileObjectActionArgument(
                        compileContext,
                        argument.Name,
                        argument.Value,
                        argumentPath,
                        context,
                        strings,
                        diagnostics,
                        action.Args);
                    continue;
                }

                var typedActionValue = string.Equals(node.Type, "set_var", StringComparison.Ordinal) &&
                                       string.Equals(argument.Name, "value", StringComparison.Ordinal);
                var valueRef = CompileValue(
                    compileContext,
                    argument.Value,
                    argumentPath,
                    context,
                    strings,
                    diagnostics,
                    true,
                    writeTarget,
                    typedActionValue);
                if (valueRef != null)
                    AddRuntimeActionArgument(diagnostics, action.Args, argument.Name, valueRef, argumentPath);
            }
            action.Arity = Math.Min(2, action.Args.Count);
            output.Add(action);
        }

        private static void CompileObjectActionArgument(
            RuntimeTriggerCompileContext compileContext,
            string rootName,
            TriggerValueRefData value,
            string path,
            TriggerAuthoringValidationContext context,
            SortedDictionary<int, string> strings,
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            IDictionary<string, TriggerAuthoringRuntimeValueRefDto> output)
        {
            if (value == null)
            {
                AddError(diagnostics, "TRG2050", path, "Value is required for Runtime Plan export.");
                return;
            }
            if (value.Source != TriggerValueSource.Constant)
            {
                AddError(diagnostics, "TRG2080", path + ".source",
                    "Runtime Plan export can flatten Object arguments only when the Object value is a constant field container.");
                return;
            }

            var fields = new List<TriggerArgumentData>(value.Fields ?? new List<TriggerArgumentData>());
            fields.Sort((left, right) => string.Compare(left?.Name, right?.Name, StringComparison.Ordinal));
            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                if (field == null || string.IsNullOrWhiteSpace(field.Name)) continue;
                var fieldPath = path + ".fields." + field.Name;
                var runtimeName = TriggerAuthoringArgumentPathResolver.ComposeRuntimeArgumentName(rootName, field.Name);
                if (field.Value != null && field.Value.Source == TriggerValueSource.Constant &&
                    field.Value.Type == TriggerValueType.IntegerList)
                {
                    var values = field.Value.IntegerListValue;
                    if (values == null || values.Count == 0)
                    {
                        AddError(diagnostics, "TRG2031", fieldPath, "IntegerList constants must contain at least one value for Runtime Plan export.");
                        continue;
                    }
                    for (var valueIndex = 0; valueIndex < values.Count; valueIndex++)
                        AddRuntimeActionArgument(
                            diagnostics,
                            output,
                            runtimeName + valueIndex.ToString(CultureInfo.InvariantCulture),
                            Const(values[valueIndex]),
                            fieldPath);
                    continue;
                }

                if (field.Value != null && field.Value.Type == TriggerValueType.Object)
                {
                    CompileObjectActionArgument(
                        compileContext,
                        runtimeName,
                        field.Value,
                        fieldPath,
                        context,
                        strings,
                        diagnostics,
                        output);
                    continue;
                }

                var valueRef = CompileValue(
                    compileContext,
                    field.Value,
                    fieldPath,
                    context,
                    strings,
                    diagnostics,
                    true);
                if (valueRef != null)
                    AddRuntimeActionArgument(diagnostics, output, runtimeName, valueRef, fieldPath);
            }
        }

        private static void AddRuntimeActionArgument(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            IDictionary<string, TriggerAuthoringRuntimeValueRefDto> output,
            string name,
            TriggerAuthoringRuntimeValueRefDto value,
            string path)
        {
            if (output.ContainsKey(name))
            {
                AddError(diagnostics, "TRG2081", path, $"Runtime argument '{name}' is produced more than once.");
                return;
            }
            output[name] = value;
        }

        private static int FirstEnabledChildIndex(IReadOnlyList<TriggerNodeData> children)
        {
            if (children == null) return -1;
            for (var i = 0; i < children.Count; i++)
                if (children[i] != null && children[i].Enabled) return i;
            return -1;
        }

        private static TriggerAuthoringRuntimeTemplateBindingDto CompileTemplate(
            RuntimeTriggerCompileContext compileContext,
            TriggerTemplateReferenceData template,
            TriggerAuthoringTemplateData templateDefinition,
            string path,
            TriggerAuthoringValidationContext context,
            SortedDictionary<int, string> strings,
            ICollection<TriggerAuthoringDiagnostic> diagnostics)
        {
            if (template == null) return null;
            if (string.IsNullOrWhiteSpace(template.TemplateId))
            {
                AddError(diagnostics, "TRG2040", path + ".templateId", "TemplateId is required.");
                return null;
            }

            var dto = new TriggerAuthoringRuntimeTemplateBindingDto
            {
                TemplateId = template.TemplateId,
                Bindings = new Dictionary<string, TriggerAuthoringRuntimeValueRefDto>(StringComparer.Ordinal)
            };
            var effectiveBindings = new SortedDictionary<string, TriggerValueRefData>(StringComparer.Ordinal);
            var parameters = templateDefinition?.Parameters ?? new List<TriggerAuthoringTemplateParameterData>();
            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name) || !parameter.HasDefault) continue;
                effectiveBindings[parameter.Name] = parameter.DefaultValue;
            }
            var bindings = template.Bindings ?? new List<TriggerArgumentData>();
            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null || string.IsNullOrWhiteSpace(binding.Name)) continue;
                effectiveBindings[binding.Name] = binding.Value;
            }
            foreach (var pair in effectiveBindings)
            {
                var value = CompileValue(
                    compileContext,
                    pair.Value,
                    path + ".bindings." + pair.Key,
                    context,
                    strings,
                    diagnostics,
                    true);
                if (value != null) dto.Bindings[pair.Key] = value;
            }
            return dto;
        }

        private static TriggerAuthoringRuntimeValueRefDto CompileValue(
            RuntimeTriggerCompileContext compileContext,
            TriggerValueRefData value,
            string path,
            TriggerAuthoringValidationContext context,
            SortedDictionary<int, string> strings,
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            bool allowStringConstant,
            bool writeTarget = false,
            bool typedActionValue = false)
        {
            if (value == null)
            {
                AddError(diagnostics, "TRG2050", path, "Value is required for Runtime Plan export.");
                return null;
            }
            if (writeTarget && value.Source != TriggerValueSource.LocalBlackboard &&
                value.Source != TriggerValueSource.GlobalBlackboard)
            {
                AddError(diagnostics, "TRG2059", path + ".source", "Blackboard write targets must reference a Local or Global Blackboard key.");
                return null;
            }
            if (value.Type == TriggerValueType.Vector3 ||
                value.Type == TriggerValueType.IntegerList ||
                value.Type == TriggerValueType.Object)
            {
                AddError(diagnostics, "TRG2051", path + ".type", $"{value.Type} cannot be represented by a single Runtime numeric value reference.");
                return null;
            }
            if (typedActionValue &&
                (value.Type == TriggerValueType.Boolean || value.Type == TriggerValueType.String) &&
                value.Source != TriggerValueSource.Constant)
            {
                AddError(
                    diagnostics,
                    "TRG2060",
                    path + ".source",
                    $"Runtime set_var currently supports {value.Type} values only as constants.");
                return null;
            }
            if (value.Type == TriggerValueType.String &&
                !writeTarget &&
                value.Source != TriggerValueSource.Constant &&
                value.Source != TriggerValueSource.TemplateParameter)
            {
                AddError(diagnostics, "TRG2052", path, "Runtime numeric value references only support String constants through the string table.");
                return null;
            }

            switch (value.Source)
            {
                case TriggerValueSource.Constant:
                    switch (value.Type)
                    {
                        case TriggerValueType.Integer:
                        case TriggerValueType.Entity:
                        case TriggerValueType.ObjectId:
                            return Const(value.IntegerValue);
                        case TriggerValueType.Number:
                            return Const(value.NumberValue);
                        case TriggerValueType.Boolean:
                            return typedActionValue
                                ? new TriggerAuthoringRuntimeValueRefDto { Kind = "Bool", BoolValue = value.BooleanValue }
                                : Const(value.BooleanValue ? 1d : 0d);
                        case TriggerValueType.String:
                            if (typedActionValue)
                            {
                                return new TriggerAuthoringRuntimeValueRefDto
                                {
                                    Kind = "String",
                                    StringValue = value.StringValue ?? string.Empty
                                };
                            }
                            if (!allowStringConstant)
                            {
                                AddError(diagnostics, "TRG2053", path, "String constants are not valid numeric condition or template values.");
                                return null;
                            }
                            var stringId = RuntimeStableStringId.Get("str:" + (value.StringValue ?? string.Empty));
                            if (strings.TryGetValue(stringId, out var existing) && !string.Equals(existing, value.StringValue ?? string.Empty, StringComparison.Ordinal))
                            {
                                AddError(diagnostics, "TRG2054", path, $"String table hash collision detected for id {stringId}.");
                                return null;
                            }
                            strings[stringId] = value.StringValue ?? string.Empty;
                            return Const(stringId);
                        default:
                            AddError(diagnostics, "TRG2055", path + ".type", $"Constant type {value.Type} is not supported by Runtime Plan export.");
                            return null;
                    }
                case TriggerValueSource.Payload:
                    return new TriggerAuthoringRuntimeValueRefDto
                    {
                        Kind = "PayloadField",
                        FieldId = RuntimeStableStringId.Get("payload:" + value.Path)
                    };
                case TriggerValueSource.Context:
                    return new TriggerAuthoringRuntimeValueRefDto { Kind = "Var", DomainId = "context", Key = value.Path };
                case TriggerValueSource.LocalBlackboard:
                    return CompileLocalBlackboard(compileContext, value, path, diagnostics, writeTarget);
                case TriggerValueSource.GlobalBlackboard:
                    if (context?.GlobalBlackboard == null)
                    {
                        AddError(diagnostics, "TRG2058", path + ".source", "Global Blackboard Catalog is required for Runtime Plan export.");
                        return null;
                    }
                    if (!context.GlobalBlackboard.TryGet(value.Path, out var globalKey) || globalKey == null)
                    {
                        AddError(diagnostics, "TRG2058", path + ".path", $"Global Blackboard key was not found: {value.Path ?? string.Empty}.");
                        return null;
                    }
                    var domain = string.IsNullOrWhiteSpace(globalKey.Domain) ? "global" : globalKey.Domain;
                    return new TriggerAuthoringRuntimeValueRefDto
                    {
                        Kind = writeTarget ? "BlackboardTarget" : "Blackboard",
                        BoardId = BlackboardIdMapper.BoardId(domain),
                        KeyId = BlackboardIdMapper.KeyId(globalKey.Key),
                        KeyType = ToBlackboardKeyType(globalKey.Type),
                        Scope = writeTarget ? BlackboardInitializationScopes.Global : null
                    };
                case TriggerValueSource.TemplateParameter:
                    return new TriggerAuthoringRuntimeValueRefDto { Kind = "TemplateParam", Key = value.Path };
                case TriggerValueSource.Expression:
                    return new TriggerAuthoringRuntimeValueRefDto { Kind = "Expr", ExprText = value.Expression };
                default:
                    AddError(diagnostics, "TRG2056", path + ".source", $"Value source {value.Source} is not supported by Runtime Plan export.");
                    return null;
            }
        }

        private static TriggerAuthoringRuntimeValueRefDto CompileLocalBlackboard(
            RuntimeTriggerCompileContext compileContext,
            TriggerValueRefData value,
            string path,
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            bool writeTarget)
        {
            if (compileContext == null || !compileContext.IsOwnerBound)
            {
                AddError(
                    diagnostics,
                    "TRG2057",
                    path + ".source",
                    "Local Blackboard can only be referenced by an owner-bound trigger.");
                return null;
            }

            if (!TriggerAuthoringLocalBlackboardPath.TryParse(value.Path, out var scope, out var key))
            {
                AddError(diagnostics, "TRG2070", path + ".path", $"Local Blackboard key was not found: {value.Path ?? string.Empty}.");
                return null;
            }

            var triggerVariables = compileContext.Trigger?.Blackboard;
            IReadOnlyList<TriggerBlackboardVariableData> variables = null;
            var variableIndex = -1;
            TriggerBlackboardVariableData variable = null;
            var isTriggerLocal = false;
            var declarationPath = "module.triggers[" + FindTriggerIndex(compileContext.Module, compileContext.Trigger) + "].blackboard";
            string boardName;
            string ownerId;
            if (scope != TriggerAuthoringLocalBlackboardScope.Module)
            {
                variableIndex = FindBlackboardVariable(triggerVariables, key, out variable);
                isTriggerLocal = variableIndex >= 0;
            }

            if (isTriggerLocal)
            {
                variables = triggerVariables;
                boardName = "local.trigger:" + compileContext.Module.ModuleId + ":" +
                            compileContext.Trigger.Id.ToString(CultureInfo.InvariantCulture);
                ownerId = compileContext.Module.ModuleId + ":" +
                          compileContext.Trigger.Id.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                variables = compileContext.Module?.Blackboard;
                variableIndex = scope == TriggerAuthoringLocalBlackboardScope.Trigger
                    ? -1
                    : FindBlackboardVariable(variables, key, out variable);
                declarationPath = "module.blackboard";
                boardName = "local.module:" + compileContext.Module?.ModuleId;
                ownerId = compileContext.Module?.ModuleId;
            }

            if (variableIndex < 0 || variable == null)
            {
                AddError(diagnostics, "TRG2070", path + ".path", $"Local Blackboard key was not found: {value.Path ?? string.Empty}.");
                return null;
            }

            var boardId = BlackboardIdMapper.BoardId(boardName);
            EnsureLocalBlackboardPlan(
                compileContext.Blackboards,
                boardId,
                boardName,
                ownerId,
                variables,
                declarationPath,
                diagnostics);
            return new TriggerAuthoringRuntimeValueRefDto
            {
                Kind = writeTarget ? "BlackboardTarget" : "Blackboard",
                BoardId = boardId,
                KeyId = BlackboardIdMapper.KeyId(variable.Key),
                KeyType = ToBlackboardKeyType(variable.Type),
                Scope = writeTarget ? BlackboardInitializationScopes.Owner : null
            };
        }

        private static void EnsureLocalBlackboardPlan(
            List<BlackboardInitializationPlan> blackboards,
            int boardId,
            string boardName,
            string ownerId,
            IReadOnlyList<TriggerBlackboardVariableData> variables,
            string path,
            ICollection<TriggerAuthoringDiagnostic> diagnostics)
        {
            for (var i = 0; i < blackboards.Count; i++)
            {
                var existing = blackboards[i];
                if (existing == null || existing.BoardId != boardId) continue;
                if (!string.Equals(existing.Name, boardName, StringComparison.Ordinal))
                    AddError(diagnostics, "TRG2071", path, $"Local Blackboard board ID collision with '{existing.Name}'.");
                return;
            }

            var plan = new BlackboardInitializationPlan
            {
                BoardId = boardId,
                Name = boardName,
                Scope = BlackboardInitializationScopes.Owner,
                OwnerId = ownerId
            };
            var keyNamesById = new Dictionary<int, string>();
            if (variables != null)
            {
                for (var i = 0; i < variables.Count; i++)
                {
                    var definition = variables[i];
                    if (definition == null || string.IsNullOrWhiteSpace(definition.Key)) continue;
                    if (!TryCompileBlackboardKey(definition, $"{path}[{i}]", diagnostics, out var key)) continue;
                    if (keyNamesById.TryGetValue(key.KeyId, out var existingKey) &&
                        !string.Equals(existingKey, definition.Key, StringComparison.Ordinal))
                    {
                        AddError(diagnostics, "TRG2072", $"{path}[{i}].key", $"Local Blackboard key ID collision with '{existingKey}'.");
                        continue;
                    }

                    keyNamesById[key.KeyId] = definition.Key;
                    plan.Keys.Add(key);
                }
            }

            plan.Keys.Sort((left, right) => left.KeyId.CompareTo(right.KeyId));
            blackboards.Add(plan);
        }

        private static int FindBlackboardVariable(
            IReadOnlyList<TriggerBlackboardVariableData> variables,
            string key,
            out TriggerBlackboardVariableData variable)
        {
            variable = null;
            if (variables == null || string.IsNullOrWhiteSpace(key)) return -1;
            for (var i = 0; i < variables.Count; i++)
            {
                var candidate = variables[i];
                if (candidate != null && string.Equals(candidate.Key, key, StringComparison.Ordinal))
                {
                    variable = candidate;
                    return i;
                }
            }

            return -1;
        }

        private static int FindTriggerIndex(TriggerAuthoringModuleData module, TriggerDefinitionData trigger)
        {
            var triggers = module?.Triggers;
            if (triggers == null) return -1;
            for (var i = 0; i < triggers.Count; i++)
                if (ReferenceEquals(triggers[i], trigger)) return i;
            return -1;
        }

        private static TriggerArgumentData FindArgument(TriggerNodeData node, string name)
        {
            return TriggerAuthoringArgumentPathResolver.FindArgument(node, name);
        }

        private static List<BlackboardInitializationPlan> CompileGlobalBlackboards(
            TriggerGlobalBlackboardDescriptorCatalog catalog,
            ICollection<TriggerAuthoringDiagnostic> diagnostics)
        {
            var byBoardId = new SortedDictionary<int, BlackboardInitializationPlan>();
            var keyNamesById = new Dictionary<int, string>();
            var definitions = catalog?.Definitions;
            if (definitions == null) return new List<BlackboardInitializationPlan>();

            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.Key)) continue;
                if (!TryCompileBlackboardKey(definition, $"project.globalBlackboard[{i}]", diagnostics, out var key))
                    continue;

                var domain = string.IsNullOrWhiteSpace(definition.Domain) ? "global" : definition.Domain;
                var boardId = BlackboardIdMapper.BoardId(domain);
                if (keyNamesById.TryGetValue(key.KeyId, out var existingKey) &&
                    !string.Equals(existingKey, definition.Key, StringComparison.Ordinal))
                {
                    AddError(diagnostics, "TRG2063", $"project.globalBlackboard[{i}].key", $"Blackboard key ID collision with '{existingKey}'.");
                    continue;
                }
                keyNamesById[key.KeyId] = definition.Key;

                if (!byBoardId.TryGetValue(boardId, out var board))
                {
                    board = new BlackboardInitializationPlan
                    {
                        BoardId = boardId,
                        Name = domain,
                        Scope = "global",
                        OwnerId = "project"
                    };
                    byBoardId.Add(boardId, board);
                }
                else if (!string.Equals(board.Name, domain, StringComparison.OrdinalIgnoreCase))
                {
                    AddError(diagnostics, "TRG2064", $"project.globalBlackboard[{i}].domain", $"Blackboard domain ID collision with '{board.Name}'.");
                    continue;
                }

                board.Keys.Add(key);
            }

            var output = new List<BlackboardInitializationPlan>(byBoardId.Values);
            for (var i = 0; i < output.Count; i++)
                output[i].Keys.Sort((left, right) => left.KeyId.CompareTo(right.KeyId));
            return output;
        }

        private static bool TryCompileBlackboardKey(
            TriggerGlobalBlackboardKeyData definition,
            string path,
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            out BlackboardInitializationKey key)
        {
            key = null;
            var value = definition.DefaultValue;
            if (value == null || value.Source != TriggerValueSource.Constant)
            {
                AddError(diagnostics, "TRG2060", path + ".defaultValue", "Global Blackboard default must be a constant value.");
                return false;
            }
            if (value.Type != definition.Type)
            {
                AddError(diagnostics, "TRG2061", path + ".defaultValue.type", $"Global Blackboard default must be {definition.Type}, got {value.Type}.");
                return false;
            }

            key = new BlackboardInitializationKey
            {
                KeyId = BlackboardIdMapper.KeyId(definition.Key),
                Name = definition.Key,
                CanRead = definition.CanRead,
                CanWrite = definition.CanWrite
            };
            switch (definition.Type)
            {
                case TriggerValueType.Integer:
                case TriggerValueType.Entity:
                case TriggerValueType.ObjectId:
                    if (value.IntegerValue < int.MinValue || value.IntegerValue > int.MaxValue)
                    {
                        AddError(diagnostics, "TRG2062", path + ".defaultValue.integerValue", "Runtime DictionaryBlackboard integer defaults must fit Int32.");
                        key = null;
                        return false;
                    }
                    key.Type = BlackboardKeyType.Int;
                    key.IntValue = (int)value.IntegerValue;
                    return true;
                case TriggerValueType.Number:
                    key.Type = BlackboardKeyType.Double;
                    key.DoubleValue = value.NumberValue;
                    return true;
                case TriggerValueType.Boolean:
                    key.Type = BlackboardKeyType.Bool;
                    key.BoolValue = value.BooleanValue;
                    return true;
                case TriggerValueType.String:
                    key.Type = BlackboardKeyType.String;
                    key.StringValue = value.StringValue ?? string.Empty;
                    return true;
                case TriggerValueType.IntegerList:
                case TriggerValueType.Vector3:
                case TriggerValueType.Object:
                    key = null;
                    return false;
                default:
                    AddError(diagnostics, "TRG2062", path + ".type", $"Global Blackboard type {definition.Type} has no Runtime initialization mapping.");
                    key = null;
                    return false;
            }
        }

        private static bool TryCompileBlackboardKey(
            TriggerBlackboardVariableData definition,
            string path,
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            out BlackboardInitializationKey key)
        {
            key = null;
            var value = definition.DefaultValue;
            if (value == null || value.Source != TriggerValueSource.Constant)
            {
                AddError(diagnostics, "TRG2073", path + ".defaultValue", "Local Blackboard default must be a constant value.");
                return false;
            }
            if (value.Type != definition.Type)
            {
                AddError(diagnostics, "TRG2074", path + ".defaultValue.type", $"Local Blackboard default must be {definition.Type}, got {value.Type}.");
                return false;
            }

            key = new BlackboardInitializationKey
            {
                KeyId = BlackboardIdMapper.KeyId(definition.Key),
                Name = definition.Key,
                CanRead = true,
                CanWrite = !definition.ReadOnly
            };
            switch (definition.Type)
            {
                case TriggerValueType.Integer:
                case TriggerValueType.Entity:
                case TriggerValueType.ObjectId:
                    if (value.IntegerValue < int.MinValue || value.IntegerValue > int.MaxValue)
                    {
                        AddError(diagnostics, "TRG2075", path + ".defaultValue.integerValue", "Runtime DictionaryBlackboard integer defaults must fit Int32.");
                        key = null;
                        return false;
                    }
                    key.Type = BlackboardKeyType.Int;
                    key.IntValue = (int)value.IntegerValue;
                    return true;
                case TriggerValueType.Number:
                    key.Type = BlackboardKeyType.Double;
                    key.DoubleValue = value.NumberValue;
                    return true;
                case TriggerValueType.Boolean:
                    key.Type = BlackboardKeyType.Bool;
                    key.BoolValue = value.BooleanValue;
                    return true;
                case TriggerValueType.String:
                    key.Type = BlackboardKeyType.String;
                    key.StringValue = value.StringValue ?? string.Empty;
                    return true;
                default:
                    AddError(diagnostics, "TRG2075", path + ".type", $"Local Blackboard type {definition.Type} has no Runtime initialization mapping.");
                    key = null;
                    return false;
            }
        }

        private static string CompareOp(string type)
        {
            switch (type)
            {
                case "arg_neq": return "NotEqual";
                case "arg_gt":
                case "num_var_gt": return "GreaterThan";
                case "arg_gte":
                case "arg_geq": return "GreaterThanOrEqual";
                case "arg_lt":
                case "num_var_lt": return "LessThan";
                case "arg_lte":
                case "arg_leq": return "LessThanOrEqual";
                default: return "Equal";
            }
        }

        private static bool IsWriteParameter(TriggerTypeDescriptor descriptor, string argumentName)
        {
            var parameters = descriptor?.Parameters;
            if (parameters == null) return false;
            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                if (parameter != null && string.Equals(parameter.Name, argumentName, StringComparison.Ordinal))
                    return parameter.Access == TriggerParameterAccess.Write;
            }
            return false;
        }

        private static BlackboardKeyType ToBlackboardKeyType(TriggerValueType type)
        {
            switch (type)
            {
                case TriggerValueType.Integer:
                case TriggerValueType.Entity:
                case TriggerValueType.ObjectId:
                    return BlackboardKeyType.Int;
                case TriggerValueType.Number:
                    return BlackboardKeyType.Double;
                case TriggerValueType.Boolean:
                    return BlackboardKeyType.Bool;
                case TriggerValueType.String:
                    return BlackboardKeyType.String;
                default:
                    return BlackboardKeyType.Unknown;
            }
        }

        private static bool TryParsePhase(string value, out int phase)
        {
            phase = 0;
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "immediate", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(value, "early", StringComparison.OrdinalIgnoreCase)) { phase = 1; return true; }
            if (string.Equals(value, "late", StringComparison.OrdinalIgnoreCase)) { phase = 2; return true; }
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out phase);
        }

        private static bool TryParseScope(string value, out int scope)
        {
            scope = 0;
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "global", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(value, "owner", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "ownerbound", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "owner_bound", StringComparison.OrdinalIgnoreCase))
            {
                scope = 1;
                return true;
            }
            return false;
        }

        private static TriggerAuthoringRuntimeValueRefDto Const(double value)
        {
            return new TriggerAuthoringRuntimeValueRefDto { Kind = "Const", ConstValue = value };
        }

        private static bool HasErrorsAtOrBelow(ICollection<TriggerAuthoringDiagnostic> diagnostics, string path)
        {
            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Severity == TriggerAuthoringDiagnosticSeverity.Error &&
                    (string.Equals(diagnostic.Path, path, StringComparison.Ordinal) ||
                     diagnostic.Path.StartsWith(path + ".", StringComparison.Ordinal)))
                    return true;
            }
            return false;
        }

        private static void AddError(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            string code,
            string path,
            string message)
        {
            diagnostics.Add(new TriggerAuthoringDiagnostic(code, TriggerAuthoringDiagnosticSeverity.Error, path, message));
        }

        private static void WriteFileAtomic(string path, string content)
        {
            EditorAtomicFileWriter.WriteAllText(path, content, Utf8WithoutBom);
        }
    }
}
#endif
