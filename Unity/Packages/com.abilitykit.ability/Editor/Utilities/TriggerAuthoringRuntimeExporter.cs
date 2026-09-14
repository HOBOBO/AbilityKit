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
        public TriggerAuthoringRuntimeExecutionNodeDto ExecutionRoot;
        public TriggerAuthoringRuntimeExecutionControlDto ExecutionControl;
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
    public sealed class TriggerAuthoringRuntimeBoolNodeDto
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
    internal sealed class TriggerAuthoringRuntimeExecutionNodeDto
    {
        public string Kind;
        public TriggerAuthoringRuntimeActionDto Action;
        public TriggerAuthoringRuntimePredicateDto Condition;
        public TriggerAuthoringRuntimeValueRefDto Collection;
        public TriggerAuthoringRuntimeValueRefDto ItemTarget;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int MaxIterations;
        public List<TriggerAuthoringRuntimeExecutionNodeDto> Children;
        public List<TriggerAuthoringRuntimeExecutionNodeDto> ElseChildren;
        public float? Weight;
        public string ScheduleMode;
        public float? IntervalMs;
        public int? MaxExecutions;
        public bool? CanBeInterrupted;
    }

    [Serializable]
    internal sealed class TriggerAuthoringRuntimeExecutionControlDto
    {
        public string Mode;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int MaxExecutions;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public double CooldownMs;
    }

    [Serializable]
    public sealed class TriggerAuthoringRuntimeValueRefDto
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
                return $"已导出 {ExportedTriggerCount} 个触发器，跳过 {SkippedDisabledCount} 个已停用触发器。";

            var builder = new StringBuilder();
            for (var i = 0; i < Diagnostics.Count; i++)
            {
                var diagnostic = Diagnostics[i];
                if (diagnostic.Severity != TriggerAuthoringDiagnosticSeverity.Error) continue;
                if (builder.Length > 0) builder.AppendLine();
                builder.Append(diagnostic.Code).Append(' ').Append(diagnostic.Path).Append(": ").Append(diagnostic.Message);
            }
            return builder.Length > 0 ? builder.ToString() : "Runtime Plan 导出失败。";
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
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("必须提供 Runtime Plan 路径。", nameof(path));

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
            TriggerAuthoringTemplateData templateDefinition = null;
            if (trigger.Template != null && context?.Templates != null &&
                context.Templates.TryGet(trigger.Template.TemplateId, out var templateAsset) &&
                templateAsset?.Template != null)
            {
                templateDefinition = templateAsset.Template;
                trigger = TriggerAuthoringTemplateDefinition.CreateEffective(
                    trigger,
                    templateDefinition,
                    true);
            }

            ValidateRuntimeOnlyFields(trigger, path, diagnostics);
            if (!TryParsePhase(trigger.Phase, out var phase))
                AddError(diagnostics, "TRG2001", path + ".phase", $"Runtime Plan 不支持阶段“{trigger.Phase ?? string.Empty}”。");
            if (!TryParseScope(trigger.Scope, out var scope))
                AddError(diagnostics, "TRG2002", path + ".scope", $"Runtime Plan 不支持作用域“{trigger.Scope ?? string.Empty}”。");

            var conditionSource = trigger.Condition;
            var actionsSource = trigger.Actions;

            TriggerNodeData condition = null;
            if (conditionSource != null && !TriggerAuthoringGroupResolver.TryExpand(
                    module, conditionSource, TriggerNodeKind.Condition, out condition, out var conditionFailure))
            {
                AddError(diagnostics, "TRG2003", path + ".condition", conditionFailure?.Message ?? "条件分组展开失败。");
            }

            TriggerNodeData actions = null;
            if (actionsSource != null && !TriggerAuthoringGroupResolver.TryExpand(
                    module, actionsSource, TriggerNodeKind.Action, out actions, out var actionFailure))
            {
                AddError(diagnostics, "TRG2004", path + ".actions", actionFailure?.Message ?? "行为分组展开失败。");
            }

            var compileContext = new RuntimeTriggerCompileContext(module, trigger, scope == 1, blackboards);
            var predicate = CompilePredicate(compileContext, condition, path + ".condition", context, strings, diagnostics);
            List<TriggerAuthoringRuntimeActionDto> actionList = null;
            TriggerAuthoringRuntimeExecutionNodeDto executionRoot = null;
            if (RequiresExecutionTree(actions))
            {
                executionRoot = CompileExecutionNode(
                    compileContext,
                    actions,
                    path + ".actions",
                    context,
                    strings,
                    diagnostics);
            }
            else
            {
                actionList = new List<TriggerAuthoringRuntimeActionDto>();
                CompileActions(compileContext, actions, path + ".actions", context, strings, diagnostics, actionList);
            }
            if (executionRoot == null && (actionList == null || actionList.Count == 0))
                AddError(diagnostics, "TRG2005", path + ".actions", "已启用的触发器必须至少编译出一个行为。");

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
                EventName = trigger.EntryMode == TriggerEntryMode.Event ? trigger.Event : null,
                EventId = trigger.EntryMode == TriggerEntryMode.Event
                    ? RuntimeStableStringId.Get("event:" + trigger.Event)
                    : 0,
                AllowExternal = trigger.AllowExternal,
                Phase = phase,
                Priority = trigger.Priority,
                Scope = scope,
                Template = template,
                Predicate = predicate,
                Actions = actionList,
                ExecutionRoot = executionRoot,
                ExecutionControl = CompileExecutionControl(trigger.ExecutionControl),
                CueId = string.IsNullOrWhiteSpace(trigger.Cue?.CueId) ? null : trigger.Cue.CueId
            };
        }

        private static TriggerAuthoringRuntimeExecutionControlDto CompileExecutionControl(
            TriggerExecutionControlData control)
        {
            if (control == null || string.IsNullOrWhiteSpace(control.Mode)) return null;
            return new TriggerAuthoringRuntimeExecutionControlDto
            {
                Mode = control.Mode.Trim().ToLowerInvariant(),
                MaxExecutions = control.MaxExecutions,
                CooldownMs = control.CooldownMilliseconds
            };
        }

        private static void ValidateRuntimeOnlyFields(
            TriggerDefinitionData trigger,
            string path,
            ICollection<TriggerAuthoringDiagnostic> diagnostics)
        {
            if (trigger.InterruptPriority != 0)
                AddError(diagnostics, "TRG2010", path + ".interruptPriority", "当前 Runtime Plan JSON 协议不支持触发器 InterruptPriority。");

            var schedule = trigger.Schedule;
            if (schedule != null &&
                (!string.IsNullOrWhiteSpace(schedule.Mode) && !string.Equals(schedule.Mode, "transient", StringComparison.OrdinalIgnoreCase) ||
                 schedule.DelayMilliseconds != 0 || schedule.IntervalMilliseconds != 0 || schedule.RepeatCount != 0))
            {
                AddError(diagnostics, "TRG2011", path + ".schedule", "当前 Runtime Plan JSON 协议无法表示触发器级 Schedule。");
            }

            var control = trigger.ExecutionControl;
            if (control != null &&
                (!IsSupportedExecutionMode(control.Mode) ||
                 control.MaxExecutions < 0 ||
                 control.CooldownMilliseconds < 0d ||
                 !string.IsNullOrWhiteSpace(control.InterruptPolicy) && !string.Equals(control.InterruptPolicy, "none", StringComparison.OrdinalIgnoreCase) ||
                 control.StopPropagationOnSuccess || control.StopPropagationOnFailure))
            {
                AddError(diagnostics, "TRG2012", path + ".executionControl", "Runtime Plan 仅支持 always/once/repeat/cooldown 执行模式、非负次数与冷却值，且暂不支持中断或传播控制。");
            }
        }

        private static bool IsSupportedExecutionMode(string mode)
        {
            return string.IsNullOrWhiteSpace(mode) ||
                   string.Equals(mode, "always", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(mode, "once", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(mode, "repeat", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(mode, "cooldown", StringComparison.OrdinalIgnoreCase);
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
            if (context?.Types != null &&
                context.Types.TryGetConditionCompiler(type, out var extensionCompiler))
            {
                try
                {
                    extensionCompiler.Compile(new TriggerAuthoringConditionCompilerContext(
                        node,
                        path,
                        (required, aliases) => CompileExtensionConditionArgument(
                            compileContext,
                            node,
                            path,
                            context,
                            strings,
                            diagnostics,
                            required,
                            aliases),
                        (functionKey, left, right) => AddFunction(
                            output,
                            functionKey,
                            left ?? Const(0),
                            right ?? Const(0)),
                        value => output.Add(new TriggerAuthoringRuntimeBoolNodeDto
                        {
                            Kind = "Const",
                            ConstValue = value
                        }),
                        (leftField, rightField, rightScale, compareOp) =>
                            output.Add(new TriggerAuthoringRuntimeBoolNodeDto
                            {
                                Kind = "CompareNumeric",
                                CompareOp = compareOp,
                                Left = new TriggerAuthoringRuntimeValueRefDto
                                {
                                    Kind = "PayloadField",
                                    FieldId = RuntimeStableStringId.Get("payload:" + leftField)
                                },
                                Right = new TriggerAuthoringRuntimeValueRefDto
                                {
                                    Kind = "PayloadField",
                                    FieldId = RuntimeStableStringId.Get("payload:" + rightField),
                                    HasScale = true,
                                    Scale = rightScale
                                }
                            }),
                        (code, pathSuffix, message) => AddError(
                            diagnostics,
                            code,
                            path + (pathSuffix ?? string.Empty),
                            message)));
                }
                catch (Exception ex)
                {
                    AddError(
                        diagnostics,
                        "TRG2099",
                        path + ".type",
                        $"条件“{node.Type ?? string.Empty}”的扩展编译器失败：{ex.Message}");
                }
                return;
            }
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
                    AddError(diagnostics, "TRG2020", path + ".type", $"条件“{node.Type ?? string.Empty}”没有对应的 Runtime Plan 编译器。");
                    return;
            }
        }

        private static TriggerAuthoringRuntimeValueRefDto CompileExtensionConditionArgument(
            RuntimeTriggerCompileContext compileContext,
            TriggerNodeData node,
            string path,
            TriggerAuthoringValidationContext context,
            SortedDictionary<int, string> strings,
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            bool required,
            params string[] aliases)
        {
            var match = TriggerAuthoringArgumentPathResolver.FindValue(
                diagnostics,
                node,
                path,
                true,
                "TRG2080",
                "仅当 Object 值为常量字段容器时，Runtime Plan 导出才能读取其字段。",
                aliases);
            if (match == null)
            {
                if (required)
                {
                    var name = aliases != null && aliases.Length > 0 ? aliases[0] : "value";
                    AddError(diagnostics, "TRG2090", path + ".arguments." + name, "扩展条件缺少必需参数。");
                }
                return null;
            }
            return CompileValue(
                compileContext,
                match.Value,
                path + match.PathSuffix,
                context,
                strings,
                diagnostics,
                false);
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
                            "仅当 Object 值为常量字段容器时，Runtime Plan 导出才能读取其字段。",
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
                            "仅当 Object 值为常量字段容器时，Runtime Plan 导出才能读取其字段。",
                "check_stack",
                "options.check_stack");
            var targetModeMatch = TriggerAuthoringArgumentPathResolver.FindValue(
                diagnostics,
                node,
                path,
                true,
                "TRG2080",
                            "仅当 Object 值为常量字段容器时，Runtime Plan 导出才能读取其字段。",
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
                AddError(diagnostics, "TRG2021", path + (targetModeMatch?.PathSuffix ?? ".arguments.target_mode"), "has_buff 的 target_mode 必须是常量，以便选择运行时谓词。");
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
                AddError(diagnostics, "TRG2022", path + ".arguments.threshold", "导出 Runtime Plan 时，health_percent 的 threshold 必须是数值常量。");
                return;
            }
            var thresholdValue = threshold.Type == TriggerValueType.Integer ? threshold.IntegerValue : threshold.NumberValue;

            var compareType = FindArgument(node, "compare_type")?.Value;
            if (compareType != null && (compareType.Source != TriggerValueSource.Constant ||
                                        compareType.Type != TriggerValueType.Integer && compareType.Type != TriggerValueType.Number))
            {
                AddError(diagnostics, "TRG2023", path + ".arguments.compare_type", "health_percent 的 compare_type 必须是常量。");
                return;
            }
            var compareValue = compareType == null
                ? 0d
                : compareType.Type == TriggerValueType.Integer ? compareType.IntegerValue : compareType.NumberValue;
            if (compareValue != 0d && compareValue != 1d)
            {
                AddError(diagnostics, "TRG2024", path + ".arguments.compare_type", "health_percent 的 compare_type 必须为 0（小于）或 1（大于）。");
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

        private static bool RequiresExecutionTree(TriggerNodeData node)
        {
            if (node == null || !node.Enabled) return false;
            if (string.Equals(node.Type, "conditional", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(node.Type, "for_each", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(node.Type, "random", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(node.Type, "weighted", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(node.Type, "scheduled", StringComparison.OrdinalIgnoreCase)) return true;
            if (TriggerAuthoringTriggerReuse.IsReference(node)) return true;
            if (RequiresExecutionTree(node.Children)) return true;
            return RequiresExecutionTree(node.ElseChildren);
        }

        private static bool RequiresExecutionTree(IReadOnlyList<TriggerNodeData> nodes)
        {
            if (nodes == null) return false;
            for (var i = 0; i < nodes.Count; i++)
                if (RequiresExecutionTree(nodes[i]))
                    return true;
            return false;
        }

        private static TriggerAuthoringRuntimeExecutionNodeDto CompileExecutionNode(
            RuntimeTriggerCompileContext compileContext,
            TriggerNodeData node,
            string path,
            TriggerAuthoringValidationContext context,
            SortedDictionary<int, string> strings,
            ICollection<TriggerAuthoringDiagnostic> diagnostics)
        {
            if (node == null || !node.Enabled) return null;
            if (TriggerAuthoringTriggerReuse.IsReference(node))
                return CompileCallableInvocation(
                    compileContext,
                    node,
                    path,
                    context,
                    strings,
                    diagnostics);
            if (string.Equals(node.Type, "conditional", StringComparison.OrdinalIgnoreCase))
            {
                return new TriggerAuthoringRuntimeExecutionNodeDto
                {
                    Kind = "If",
                    Condition = CompilePredicate(
                        compileContext,
                        node.Condition,
                        path + ".condition",
                        context,
                        strings,
                        diagnostics),
                    Children = CompileExecutionChildren(
                        compileContext,
                        node.Children,
                        path + ".children",
                        context,
                        strings,
                        diagnostics),
                    ElseChildren = CompileExecutionChildren(
                        compileContext,
                        node.ElseChildren,
                        path + ".elseChildren",
                        context,
                        strings,
                        diagnostics)
                };
            }

            if (string.Equals(node.Type, "seq", StringComparison.OrdinalIgnoreCase))
            {
                return new TriggerAuthoringRuntimeExecutionNodeDto
                {
                    Kind = "Sequence",
                    Children = CompileExecutionChildren(
                        compileContext,
                        node.Children,
                        path + ".children",
                        context,
                        strings,
                        diagnostics)
                };
            }

            if (string.Equals(node.Type, "random", StringComparison.OrdinalIgnoreCase))
            {
                return new TriggerAuthoringRuntimeExecutionNodeDto
                {
                    Kind = "Random",
                    Children = CompileExecutionChildren(
                        compileContext,
                        node.Children,
                        path + ".children",
                        context,
                        strings,
                        diagnostics)
                };
            }

            if (string.Equals(node.Type, "weighted", StringComparison.OrdinalIgnoreCase))
            {
                var child = CompileFirstEnabledChild(
                    compileContext,
                    node.Children,
                    path + ".children",
                    context,
                    strings,
                    diagnostics);
                var weight = ReadConstantNumber(
                    FindArgument(node, "weight")?.Value,
                    1f,
                    path + ".arguments.weight",
                    diagnostics);
                if (child != null) child.Weight = weight;
                return child;
            }

            if (string.Equals(node.Type, "scheduled", StringComparison.OrdinalIgnoreCase))
            {
                var mode = ReadConstantInteger(
                    FindArgument(node, "schedule_mode")?.Value,
                    0,
                    path + ".arguments.schedule_mode",
                    diagnostics);
                if (mode < 0 || mode > 5)
                {
                    AddError(diagnostics, "TRG2034", path + ".arguments.schedule_mode",
                        "scheduled 的 schedule_mode 必须在 0 到 5 之间。");
                    mode = 0;
                }
                var intervalMs = ReadConstantNumber(
                    FindArgument(node, "interval_ms")?.Value,
                    0f,
                    path + ".arguments.interval_ms",
                    diagnostics,
                    required: false);
                var maxExecutions = ReadConstantInteger(
                    FindArgument(node, "max_executions")?.Value,
                    -1,
                    path + ".arguments.max_executions",
                    diagnostics,
                    required: false);
                var canBeInterrupted = ReadConstantBoolean(
                    FindArgument(node, "can_be_interrupted")?.Value,
                    true,
                    path + ".arguments.can_be_interrupted",
                    diagnostics);
                return new TriggerAuthoringRuntimeExecutionNodeDto
                {
                    Kind = "Scheduled",
                    ScheduleMode = ScheduleModeName(mode),
                    IntervalMs = intervalMs,
                    MaxExecutions = maxExecutions,
                    CanBeInterrupted = canBeInterrupted,
                    Children = CompileExecutionChildren(
                        compileContext,
                        node.Children,
                        path + ".children",
                        context,
                        strings,
                        diagnostics)
                };
            }

            if (string.Equals(node.Type, "for_each", StringComparison.OrdinalIgnoreCase))
            {
                var collectionArgument = FindArgument(node, "collection");
                var itemArgument = FindArgument(node, "item");
                var maxIterationsArgument = FindArgument(node, "max_iterations");
                var maxIterations = 0;
                if (maxIterationsArgument?.Value == null ||
                    maxIterationsArgument.Value.Source != TriggerValueSource.Constant ||
                    maxIterationsArgument.Value.Type != TriggerValueType.Integer ||
                    maxIterationsArgument.Value.IntegerValue <= 0 ||
                    maxIterationsArgument.Value.IntegerValue > int.MaxValue)
                {
                    AddError(
                        diagnostics,
                        "TRG2033",
                        path + ".arguments.max_iterations",
                        "for_each 的 max_iterations 必须是大于 0 的整数常量。");
                }
                else
                {
                    maxIterations = (int)maxIterationsArgument.Value.IntegerValue;
                }

                return new TriggerAuthoringRuntimeExecutionNodeDto
                {
                    Kind = "ForEach",
                    Collection = CompileValue(
                        compileContext,
                        collectionArgument?.Value,
                        path + ".arguments.collection",
                        context,
                        strings,
                        diagnostics,
                        true),
                    ItemTarget = CompileValue(
                        compileContext,
                        itemArgument?.Value,
                        path + ".arguments.item",
                        context,
                        strings,
                        diagnostics,
                        true,
                        true),
                    MaxIterations = maxIterations,
                    Children = CompileExecutionChildren(
                        compileContext,
                        node.Children,
                        path + ".children",
                        context,
                        strings,
                        diagnostics)
                };
            }

            if (node.Children != null && node.Children.Count > 0)
            {
                AddError(diagnostics, "TRG2030", path + ".children", $"行为“{node.Type ?? string.Empty}”无法保留子节点执行语义。");
                return null;
            }

            var action = CompileAction(compileContext, node, path, context, strings, diagnostics);
            return action == null
                ? null
                : new TriggerAuthoringRuntimeExecutionNodeDto { Kind = "Action", Action = action };
        }

        private static TriggerAuthoringRuntimeExecutionNodeDto CompileCallableInvocation(
            RuntimeTriggerCompileContext callerContext,
            TriggerNodeData node,
            string path,
            TriggerAuthoringValidationContext context,
            SortedDictionary<int, string> strings,
            ICollection<TriggerAuthoringDiagnostic> diagnostics)
        {
            if (!TriggerAuthoringTriggerReuse.TryGetReferencedTriggerId(node, out var targetId))
                return null;
            var targetSource = TriggerAuthoringTriggerReuse.FindTrigger(callerContext.Module, targetId);
            var target = TriggerAuthoringTemplateDefinition.ResolveEffective(targetSource, context?.Templates);
            var parameters = target?.CallableParameters;
            if (target == null || parameters == null || parameters.Count == 0)
            {
                var legacyAction = CompileAction(callerContext, node, path, context, strings, diagnostics);
                return legacyAction == null
                    ? null
                    : new TriggerAuthoringRuntimeExecutionNodeDto { Kind = "Action", Action = legacyAction };
            }

            var targetContext = new RuntimeTriggerCompileContext(
                callerContext.Module,
                target,
                true,
                callerContext.Blackboards);
            var children = new List<TriggerAuthoringRuntimeExecutionNodeDto>();
            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                if (parameter == null || parameter.Direction != TriggerCallableParameterDirection.Input) continue;
                var binding = FindArgument(node, parameter.Name);
                var input = binding?.Value ?? (parameter.HasDefault ? parameter.DefaultValue : null);
                if (input == null) continue;
                var targetValue = CompileCallableLocalValue(
                    targetContext,
                    target,
                    parameter,
                    true,
                    path + ".arguments." + parameter.Name,
                    diagnostics);
                var inputValue = CompileValue(
                    callerContext,
                    input,
                    path + ".arguments." + parameter.Name,
                    context,
                    strings,
                    diagnostics,
                    true,
                    false,
                    IsTypedBlackboardValue(parameter.Type));
                AddSetVariableExecution(children, targetValue, inputValue);
            }

            var triggerIdArgument = FindArgument(node, TriggerAuthoringTriggerReuse.TriggerIdArgument);
            var callNode = new TriggerNodeData
            {
                Enabled = true,
                Kind = TriggerNodeKind.Action,
                Type = TriggerAuthoringTriggerReuse.ExecuteTriggerType,
                Arguments = triggerIdArgument == null
                    ? new List<TriggerArgumentData>()
                    : new List<TriggerArgumentData> { triggerIdArgument }
            };
            var callAction = CompileAction(callerContext, callNode, path, context, strings, diagnostics);
            if (callAction != null)
                children.Add(new TriggerAuthoringRuntimeExecutionNodeDto { Kind = "Action", Action = callAction });

            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                if (parameter == null || parameter.Direction != TriggerCallableParameterDirection.Output) continue;
                var binding = FindArgument(node, parameter.Name);
                if (binding?.Value == null) continue;
                var outputTarget = CompileValue(
                    callerContext,
                    binding.Value,
                    path + ".arguments." + parameter.Name,
                    context,
                    strings,
                    diagnostics,
                    true,
                    true);
                var outputValue = CompileCallableLocalValue(
                    targetContext,
                    target,
                    parameter,
                    false,
                    path + ".arguments." + parameter.Name,
                    diagnostics);
                if (outputValue != null && IsTypedBlackboardValue(parameter.Type))
                    outputValue.Kind = "BlackboardValue";
                AddSetVariableExecution(children, outputTarget, outputValue);
            }

            return new TriggerAuthoringRuntimeExecutionNodeDto
            {
                Kind = "Sequence",
                Children = children
            };
        }

        private static TriggerAuthoringRuntimeValueRefDto CompileCallableLocalValue(
            RuntimeTriggerCompileContext targetContext,
            TriggerDefinitionData target,
            TriggerCallableParameterData parameter,
            bool writeTarget,
            string path,
            ICollection<TriggerAuthoringDiagnostic> diagnostics)
        {
            if (FindBlackboardVariable(target?.Blackboard, parameter.LocalVariableKey, out var variable) < 0 ||
                variable == null)
            {
                AddError(diagnostics, "TRG2086", path, $"未找到可调用触发器局部变量：{parameter.LocalVariableKey ?? string.Empty}。");
                return null;
            }

            var boardName = "local.trigger:" + targetContext.Module.ModuleId + ":" +
                            target.Id.ToString(CultureInfo.InvariantCulture);
            var ownerId = targetContext.Module.ModuleId + ":" +
                          target.Id.ToString(CultureInfo.InvariantCulture);
            var boardId = BlackboardIdMapper.BoardId(boardName);
            EnsureLocalBlackboardPlan(
                targetContext.Blackboards,
                boardId,
                boardName,
                ownerId,
                target.Blackboard,
                path,
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

        private static void AddSetVariableExecution(
            ICollection<TriggerAuthoringRuntimeExecutionNodeDto> output,
            TriggerAuthoringRuntimeValueRefDto target,
            TriggerAuthoringRuntimeValueRefDto value)
        {
            if (target == null || value == null) return;
            output.Add(new TriggerAuthoringRuntimeExecutionNodeDto
            {
                Kind = "Action",
                Action = new TriggerAuthoringRuntimeActionDto
                {
                    ActionId = RuntimeStableStringId.Get("action:set_var"),
                    Arity = 2,
                    Args = new Dictionary<string, TriggerAuthoringRuntimeValueRefDto>(StringComparer.Ordinal)
                    {
                        { "target", target },
                        { "value", value }
                    }
                }
            });
        }

        private static bool IsTypedBlackboardValue(TriggerValueType type)
        {
            return type == TriggerValueType.Boolean || type == TriggerValueType.String;
        }

        private static List<TriggerAuthoringRuntimeExecutionNodeDto> CompileExecutionChildren(
            RuntimeTriggerCompileContext compileContext,
            IReadOnlyList<TriggerNodeData> nodes,
            string path,
            TriggerAuthoringValidationContext context,
            SortedDictionary<int, string> strings,
            ICollection<TriggerAuthoringDiagnostic> diagnostics)
        {
            var result = new List<TriggerAuthoringRuntimeExecutionNodeDto>();
            if (nodes == null) return result;
            for (var i = 0; i < nodes.Count; i++)
            {
                var child = CompileExecutionNode(
                    compileContext,
                    nodes[i],
                    path + "[" + i + "]",
                    context,
                    strings,
                    diagnostics);
                if (child != null) result.Add(child);
            }
            return result;
        }

        private static TriggerAuthoringRuntimeExecutionNodeDto CompileFirstEnabledChild(
            RuntimeTriggerCompileContext compileContext,
            IReadOnlyList<TriggerNodeData> nodes,
            string path,
            TriggerAuthoringValidationContext context,
            SortedDictionary<int, string> strings,
            ICollection<TriggerAuthoringDiagnostic> diagnostics)
        {
            if (nodes == null) return null;
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] == null || !nodes[i].Enabled) continue;
                return CompileExecutionNode(
                    compileContext,
                    nodes[i],
                    path + "[" + i + "]",
                    context,
                    strings,
                    diagnostics);
            }
            return null;
        }

        private static float ReadConstantNumber(
            TriggerValueRefData value,
            float fallback,
            string path,
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            bool required = true)
        {
            if (value == null)
            {
                if (required) AddError(diagnostics, "TRG2035", path, "必须提供数值常量。");
                return fallback;
            }
            if (value.Source == TriggerValueSource.Constant && value.Type == TriggerValueType.Number)
                return (float)value.NumberValue;
            if (value.Source == TriggerValueSource.Constant && value.Type == TriggerValueType.Integer)
                return (float)value.IntegerValue;
            AddError(diagnostics, "TRG2035", path, "必须提供数值常量。");
            return fallback;
        }

        private static int ReadConstantInteger(
            TriggerValueRefData value,
            int fallback,
            string path,
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            bool required = true)
        {
            if (value == null)
            {
                if (required) AddError(diagnostics, "TRG2036", path, "必须提供整数常量。");
                return fallback;
            }
            if (value.Source == TriggerValueSource.Constant && value.Type == TriggerValueType.Integer &&
                value.IntegerValue >= int.MinValue && value.IntegerValue <= int.MaxValue)
                return (int)value.IntegerValue;
            AddError(diagnostics, "TRG2036", path, "必须提供整数常量。");
            return fallback;
        }

        private static bool ReadConstantBoolean(
            TriggerValueRefData value,
            bool fallback,
            string path,
            ICollection<TriggerAuthoringDiagnostic> diagnostics)
        {
            if (value == null) return fallback;
            if (value.Source == TriggerValueSource.Constant && value.Type == TriggerValueType.Boolean)
                return value.BooleanValue;
            AddError(diagnostics, "TRG2037", path, "必须提供 Boolean 常量。");
            return fallback;
        }

        private static string ScheduleModeName(int value)
        {
            switch (value)
            {
                case 1: return "Timed";
                case 2: return "Periodic";
                case 3: return "External";
                case 4: return "Conditional";
                case 5: return "Continuous";
                default: return "Transient";
            }
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
                AddError(diagnostics, "TRG2030", path + ".children", $"行为“{node.Type ?? string.Empty}”无法保留子节点执行语义。");
                return;
            }
            var action = CompileAction(compileContext, node, path, context, strings, diagnostics);
            if (action != null) output.Add(action);
        }

        private static TriggerAuthoringRuntimeActionDto CompileAction(
            RuntimeTriggerCompileContext compileContext,
            TriggerNodeData node,
            string path,
            TriggerAuthoringValidationContext context,
            SortedDictionary<int, string> strings,
            ICollection<TriggerAuthoringDiagnostic> diagnostics)
        {
            TriggerTypeDescriptor descriptor = null;
            context?.Types?.TryGet(TriggerNodeKind.Action, node.Type, out descriptor);
            if (descriptor == null || !descriptor.RuntimeSupported)
            {
                AddError(diagnostics, "TRG2032", path + ".type", $"行为“{node.Type ?? string.Empty}”未注册到当前项目的 Runtime PlanAction 集合。");
                return null;
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
                        AddError(diagnostics, "TRG2031", argumentPath, "导出 Runtime Plan 时，IntegerList 常量必须至少包含一个值。");
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
            return action;
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
                AddError(diagnostics, "TRG2050", path, "导出 Runtime Plan 时必须设置值。");
                return;
            }
            if (value.Source != TriggerValueSource.Constant)
            {
                AddError(diagnostics, "TRG2080", path + ".source",
                    "仅当 Object 值为常量字段容器时，Runtime Plan 导出才能展开对象参数。");
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
                        AddError(diagnostics, "TRG2031", fieldPath, "导出 Runtime Plan 时，IntegerList 常量必须至少包含一个值。");
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
                AddError(diagnostics, "TRG2081", path, $"运行时参数“{name}”被重复生成。");
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
                AddError(diagnostics, "TRG2040", path + ".templateId", "必须填写 Template ID。");
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
                AddError(diagnostics, "TRG2050", path, "导出 Runtime Plan 时必须设置值。");
                return null;
            }
            if (writeTarget && value.Source != TriggerValueSource.LocalBlackboard &&
                value.Source != TriggerValueSource.GlobalBlackboard &&
                value.Source != TriggerValueSource.Context)
            {
                AddError(diagnostics, "TRG2059", path + ".source",
                    "黑板写入目标必须引用局部、全局或业务扩展注册的可写运行时 Blackboard Key。");
                return null;
            }
            if (value.Type == TriggerValueType.Vector3 ||
                value.Type == TriggerValueType.IntegerList ||
                value.Type == TriggerValueType.Object)
            {
                AddError(diagnostics, "TRG2051", path + ".type", $"{value.Type} 无法表示为单个运行时数值引用。");
                return null;
            }
            if (typedActionValue &&
                (value.Type == TriggerValueType.Boolean || value.Type == TriggerValueType.String) &&
                value.Source != TriggerValueSource.Constant &&
                value.Source != TriggerValueSource.LocalBlackboard &&
                value.Source != TriggerValueSource.GlobalBlackboard)
            {
                AddError(
                    diagnostics,
                    "TRG2060",
                    path + ".source",
                    $"运行时 {value.Type} 值当前支持常量或黑板引用。");
                return null;
            }
            if (value.Type == TriggerValueType.String &&
                !writeTarget &&
                !typedActionValue &&
                value.Source != TriggerValueSource.Constant &&
                value.Source != TriggerValueSource.TemplateParameter)
            {
                AddError(diagnostics, "TRG2052", path, "运行时数值引用仅能通过字符串表支持 String 常量。");
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
                                AddError(diagnostics, "TRG2053", path, "String 常量不能作为数值条件或模板值。");
                                return null;
                            }
                            var stringId = RuntimeStableStringId.Get("str:" + (value.StringValue ?? string.Empty));
                            if (strings.TryGetValue(stringId, out var existing) && !string.Equals(existing, value.StringValue ?? string.Empty, StringComparison.Ordinal))
                            {
                                AddError(diagnostics, "TRG2054", path, $"检测到字符串表哈希冲突，ID 为 {stringId}。");
                                return null;
                            }
                            strings[stringId] = value.StringValue ?? string.Empty;
                            return Const(stringId);
                        default:
                            AddError(diagnostics, "TRG2055", path + ".type", $"Runtime Plan 导出不支持常量类型 {value.Type}。");
                            return null;
                    }
                case TriggerValueSource.Payload:
                    return new TriggerAuthoringRuntimeValueRefDto
                    {
                        Kind = "PayloadField",
                        FieldId = RuntimeStableStringId.Get("payload:" + value.Path)
                    };
                case TriggerValueSource.Context:
                    if (writeTarget)
                    {
                        if (context?.ValueSources == null ||
                            !context.ValueSources.TryGet(value.Path, out var writableRuntimeValue) ||
                            writableRuntimeValue == null || !writableRuntimeValue.CanWrite)
                        {
                            AddError(diagnostics, "TRG2087", path + ".path",
                                $"运行上下文值不可写：{value.Path ?? string.Empty}。");
                            return null;
                        }
                        return new TriggerAuthoringRuntimeValueRefDto
                        {
                            Kind = "BlackboardTarget",
                            BoardId = BlackboardIdMapper.BoardId(writableRuntimeValue.BlackboardName),
                            KeyId = BlackboardIdMapper.KeyId(writableRuntimeValue.BlackboardKey),
                            KeyType = ToBlackboardKeyType(value.Type),
                            Scope = string.IsNullOrWhiteSpace(writableRuntimeValue.BlackboardScope)
                                ? null
                                : writableRuntimeValue.BlackboardScope
                        };
                    }
                    SplitContextPath(value.Path, out var contextDomain, out var contextKey);
                    return new TriggerAuthoringRuntimeValueRefDto
                    {
                        Kind = "Var",
                        DomainId = contextDomain,
                        Key = contextKey
                    };
                case TriggerValueSource.LocalBlackboard:
                    var localValue = CompileLocalBlackboard(compileContext, value, path, diagnostics, writeTarget);
                    if (localValue != null && typedActionValue && !writeTarget &&
                        (value.Type == TriggerValueType.Boolean || value.Type == TriggerValueType.String))
                        localValue.Kind = "BlackboardValue";
                    return localValue;
                case TriggerValueSource.GlobalBlackboard:
                    if (context?.GlobalBlackboard == null)
                    {
                        AddError(diagnostics, "TRG2058", path + ".source", "导出 Runtime Plan 时必须分配全局黑板目录。");
                        return null;
                    }
                    if (!context.GlobalBlackboard.TryGet(value.Path, out var globalKey) || globalKey == null)
                    {
                        AddError(diagnostics, "TRG2058", path + ".path", $"未找到全局黑板 Key：{value.Path ?? string.Empty}。");
                        return null;
                    }
                    var domain = string.IsNullOrWhiteSpace(globalKey.Domain) ? "global" : globalKey.Domain;
                    var globalValue = new TriggerAuthoringRuntimeValueRefDto
                    {
                        Kind = writeTarget ? "BlackboardTarget" : "Blackboard",
                        BoardId = BlackboardIdMapper.BoardId(domain),
                        KeyId = BlackboardIdMapper.KeyId(globalKey.Key),
                        KeyType = ToBlackboardKeyType(globalKey.Type),
                        Scope = writeTarget ? BlackboardInitializationScopes.Global : null
                    };
                    if (typedActionValue && !writeTarget &&
                        (value.Type == TriggerValueType.Boolean || value.Type == TriggerValueType.String))
                        globalValue.Kind = "BlackboardValue";
                    return globalValue;
                case TriggerValueSource.TemplateParameter:
                    return new TriggerAuthoringRuntimeValueRefDto { Kind = "TemplateParam", Key = value.Path };
                case TriggerValueSource.Expression:
                    if (!TriggerAuthoringValueRefEditor.TryValidateExpression(value.Expression, out var expressionError))
                    {
                        AddError(diagnostics, "TRG2082", path + ".expression", expressionError);
                        return null;
                    }
                    if (!TryRewriteExpression(
                            compileContext,
                            context,
                            value.Expression,
                            path + ".expression",
                            diagnostics,
                            out var runtimeExpression))
                        return null;
                    return new TriggerAuthoringRuntimeValueRefDto { Kind = "Expr", ExprText = runtimeExpression };
                default:
                    AddError(diagnostics, "TRG2056", path + ".source", $"Runtime Plan 导出不支持值来源 {value.Source}。");
                    return null;
            }
        }

        private static void SplitContextPath(string path, out string domain, out string key)
        {
            path = path ?? string.Empty;
            var separator = path.IndexOf(':');
            if (separator > 0 && separator < path.Length - 1)
            {
                domain = path.Substring(0, separator);
                key = path.Substring(separator + 1);
                return;
            }

            domain = "context";
            key = path;
        }

        private static bool TryRewriteExpression(
            RuntimeTriggerCompileContext compileContext,
            TriggerAuthoringValidationContext context,
            string expression,
            string path,
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            out string rewritten)
        {
            var output = new StringBuilder(expression != null ? expression.Length + 32 : 32);
            expression = expression ?? string.Empty;
            var success = true;
            var index = 0;
            while (index < expression.Length)
            {
                var current = expression[index];
                if (!char.IsLetter(current) && current != '_')
                {
                    output.Append(current);
                    index++;
                    continue;
                }

                var start = index++;
                while (index < expression.Length)
                {
                    current = expression[index];
                    if (char.IsLetterOrDigit(current) || current == '_' || current == '.')
                    {
                        index++;
                        continue;
                    }
                    break;
                }

                var identifier = expression.Substring(start, index - start);
                var next = index;
                while (next < expression.Length && char.IsWhiteSpace(expression[next])) next++;
                if (next < expression.Length && expression[next] == '(')
                {
                    output.Append(identifier);
                    continue;
                }

                if (!TryRewriteExpressionReference(
                        compileContext,
                        context,
                        identifier,
                        path,
                        diagnostics,
                        out var replacement))
                    success = false;
                output.Append(replacement ?? identifier);
            }

            rewritten = output.ToString();
            return success;
        }

        private static bool TryRewriteExpressionReference(
            RuntimeTriggerCompileContext compileContext,
            TriggerAuthoringValidationContext context,
            string identifier,
            string path,
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            out string replacement)
        {
            replacement = identifier;
            var dot = identifier.IndexOf('.');
            if (dot <= 0 || dot >= identifier.Length - 1) return true;

            var scope = identifier.Substring(0, dot);
            var key = identifier.Substring(dot + 1);
            if (!string.Equals(scope, "trigger", StringComparison.Ordinal) &&
                !string.Equals(scope, "module", StringComparison.Ordinal) &&
                !string.Equals(scope, "global", StringComparison.Ordinal))
                return true;

            int boardId;
            int keyId;
            if (string.Equals(scope, "global", StringComparison.Ordinal))
            {
                if (context?.GlobalBlackboard == null ||
                    !context.GlobalBlackboard.TryGet(key, out var global) ||
                    global == null || !global.CanRead)
                {
                    AddError(diagnostics, "TRG2083", path, $"公式引用了未知或不可读的全局黑板 Key：{key}。");
                    return false;
                }

                var domain = string.IsNullOrWhiteSpace(global.Domain) ? "global" : global.Domain;
                boardId = BlackboardIdMapper.BoardId(domain);
                keyId = BlackboardIdMapper.KeyId(global.Key);
            }
            else
            {
                if (compileContext == null || !compileContext.IsOwnerBound)
                {
                    AddError(diagnostics, "TRG2084", path, "公式中的局部黑板引用要求触发器绑定所有者。");
                    return false;
                }

                IReadOnlyList<TriggerBlackboardVariableData> variables;
                string boardName;
                string ownerId;
                string declarationPath;
                if (string.Equals(scope, "trigger", StringComparison.Ordinal))
                {
                    variables = compileContext.Trigger?.Blackboard;
                    boardName = "local.trigger:" + compileContext.Module.ModuleId + ":" +
                                compileContext.Trigger.Id.ToString(CultureInfo.InvariantCulture);
                    ownerId = compileContext.Module.ModuleId + ":" +
                              compileContext.Trigger.Id.ToString(CultureInfo.InvariantCulture);
                    declarationPath = "module.triggers[" +
                                      FindTriggerIndex(compileContext.Module, compileContext.Trigger) + "].blackboard";
                }
                else
                {
                    variables = compileContext.Module?.Blackboard;
                    boardName = "local.module:" + compileContext.Module?.ModuleId;
                    ownerId = compileContext.Module?.ModuleId;
                    declarationPath = "module.blackboard";
                }

                if (FindBlackboardVariable(variables, key, out var variable) < 0 || variable == null)
                {
                    AddError(diagnostics, "TRG2085", path, $"公式引用了未知的{scope}黑板 Key：{key}。");
                    return false;
                }

                boardId = BlackboardIdMapper.BoardId(boardName);
                keyId = BlackboardIdMapper.KeyId(variable.Key);
                EnsureLocalBlackboardPlan(
                    compileContext.Blackboards,
                    boardId,
                    boardName,
                    ownerId,
                    variables,
                    declarationPath,
                    diagnostics);
            }

            replacement = "__bb" + boardId.ToString(CultureInfo.InvariantCulture) +
                          ".k" + keyId.ToString(CultureInfo.InvariantCulture);
            return true;
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
                    "局部黑板只能由绑定所有者的触发器引用。");
                return null;
            }

            if (!TriggerAuthoringLocalBlackboardPath.TryParse(value.Path, out var scope, out var key))
            {
                AddError(diagnostics, "TRG2070", path + ".path", $"未找到局部黑板 Key：{value.Path ?? string.Empty}。");
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
                AddError(diagnostics, "TRG2070", path + ".path", $"未找到局部黑板 Key：{value.Path ?? string.Empty}。");
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
                    AddError(diagnostics, "TRG2071", path, $"局部黑板 Board ID 与“{existing.Name}”发生冲突。");
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
                        AddError(diagnostics, "TRG2072", $"{path}[{i}].key", $"局部黑板 Key ID 与“{existingKey}”发生冲突。");
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
                    AddError(diagnostics, "TRG2063", $"project.globalBlackboard[{i}].key", $"黑板 Key ID 与“{existingKey}”发生冲突。");
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
                    AddError(diagnostics, "TRG2064", $"project.globalBlackboard[{i}].domain", $"黑板域 ID 与“{board.Name}”发生冲突。");
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
                AddError(diagnostics, "TRG2060", path + ".defaultValue", "全局黑板默认值必须是常量。");
                return false;
            }
            if (value.Type != definition.Type)
            {
                AddError(diagnostics, "TRG2061", path + ".defaultValue.type", $"全局黑板默认值必须为 {definition.Type}，当前为 {value.Type}。");
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
                        AddError(diagnostics, "TRG2062", path + ".defaultValue.integerValue", "Runtime DictionaryBlackboard 的整数默认值必须在 Int32 范围内。");
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
                    AddError(diagnostics, "TRG2062", path + ".type", $"全局黑板类型 {definition.Type} 没有对应的运行时初始化映射。");
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
                AddError(diagnostics, "TRG2073", path + ".defaultValue", "局部黑板默认值必须是常量。");
                return false;
            }
            if (value.Type != definition.Type)
            {
                AddError(diagnostics, "TRG2074", path + ".defaultValue.type", $"局部黑板默认值必须为 {definition.Type}，当前为 {value.Type}。");
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
                        AddError(diagnostics, "TRG2075", path + ".defaultValue.integerValue", "Runtime DictionaryBlackboard 的整数默认值必须在 Int32 范围内。");
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
                    AddError(diagnostics, "TRG2075", path + ".type", $"局部黑板类型 {definition.Type} 没有对应的运行时初始化映射。");
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
                    return TriggerParameterAccessRules.IsWrite(parameter.Access);
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
