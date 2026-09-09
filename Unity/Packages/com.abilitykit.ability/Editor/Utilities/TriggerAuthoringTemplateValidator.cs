#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using AbilityKit.Ability.Config.Authoring;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class TriggerAuthoringTemplateDefinition
    {
        public static TriggerDefinitionData Get(TriggerAuthoringTemplateData template)
        {
            if (template == null) return null;
            Normalize(template);
            return template.Definition;
        }

        public static bool Normalize(TriggerAuthoringTemplateData template)
        {
            if (template == null) return false;
            var changed = false;
            if (template.Definition == null)
            {
                template.Definition = TriggerAuthoringTemplateData.CreateDefaultDefinition();
                changed = true;
            }

            var definition = template.Definition;
            definition.Blackboard = definition.Blackboard ?? new List<TriggerBlackboardVariableData>();
            definition.Schedule = definition.Schedule ?? new TriggerScheduleData();
            definition.Cue = definition.Cue ?? new TriggerCueData();
            definition.ExecutionControl = definition.ExecutionControl ?? new TriggerExecutionControlData();
            definition.Tags = definition.Tags ?? new List<string>();
            template.Parameters = template.Parameters ?? new List<TriggerAuthoringTemplateParameterData>();
            for (var i = 0; i < template.Parameters.Count; i++)
            {
                var parameter = template.Parameters[i];
                if (parameter == null) continue;
                if (string.IsNullOrWhiteSpace(parameter.LocalVariableKey) || parameter.Type == TriggerValueType.None)
                    continue;

                var variable = FindVariable(definition.Blackboard, parameter.LocalVariableKey);
                if (variable == null)
                {
                    variable = new TriggerBlackboardVariableData
                    {
                        Key = parameter.LocalVariableKey,
                        Type = parameter.Type,
                        ReadOnly = true,
                        Description = string.IsNullOrWhiteSpace(parameter.Description)
                            ? "模板调用输入"
                            : "模板调用输入：" + parameter.Description,
                        DefaultValue = parameter.HasDefault
                            ? TriggerAuthoringGroupResolver.CloneValue(parameter.DefaultValue)
                            : CreateDefaultValue(parameter.Type)
                    };
                    definition.Blackboard.Add(variable);
                    changed = true;
                }
                else
                {
                    if (variable.Type != parameter.Type)
                    {
                        variable.Type = parameter.Type;
                        changed = true;
                    }
                    if (!variable.ReadOnly)
                    {
                        variable.ReadOnly = true;
                        changed = true;
                    }
                    if (parameter.HasDefault && !ValuesEqual(variable.DefaultValue, parameter.DefaultValue))
                    {
                        variable.DefaultValue = TriggerAuthoringGroupResolver.CloneValue(parameter.DefaultValue);
                        changed = true;
                    }
                    else if (variable.DefaultValue == null || variable.DefaultValue.Type != parameter.Type ||
                             variable.DefaultValue.Source != TriggerValueSource.Constant)
                    {
                        variable.DefaultValue = CreateDefaultValue(parameter.Type);
                        changed = true;
                    }
                }
            }
            return changed;
        }

        public static TriggerDefinitionData CreateEffective(
            TriggerDefinitionData instance,
            TriggerAuthoringTemplateData template,
            bool runtimeParameterReferences = false)
        {
            var prototype = Get(template);
            if (prototype == null) return instance;
            var effective = CloneDefinition(prototype);
            if (instance != null)
            {
                effective.Id = instance.Id;
                effective.Name = instance.Name;
                effective.GroupPath = instance.GroupPath;
                effective.Tags = instance.Tags != null ? new List<string>(instance.Tags) : new List<string>();
                effective.Enabled = instance.Enabled;
                effective.Template = instance.Template;
            }
            if (runtimeParameterReferences)
            {
                RewriteInputReferences(effective.Condition, template.Parameters, null, true);
                RewriteInputReferences(effective.Actions, template.Parameters, null, true);
                RemoveInputLocalDeclarations(effective.Blackboard, template.Parameters);
            }
            return effective;
        }

        public static TriggerDefinitionData ResolveEffective(
            TriggerDefinitionData instance,
            TriggerTemplateDescriptorCatalog templates)
        {
            if (instance?.Template == null || templates == null ||
                !templates.TryGet(instance.Template.TemplateId, out var templateAsset) ||
                templateAsset?.Template == null)
                return instance;
            return CreateEffective(instance, templateAsset.Template);
        }

        public static TriggerDefinitionData ResolveEffectiveView(
            TriggerDefinitionData instance,
            TriggerTemplateDescriptorCatalog templates)
        {
            if (instance?.Template == null || templates == null ||
                !templates.TryGet(instance.Template.TemplateId, out var templateAsset) ||
                templateAsset?.Template == null)
                return instance;
            var prototype = Get(templateAsset.Template);
            return new TriggerDefinitionData
            {
                Id = instance.Id,
                Name = instance.Name,
                GroupPath = instance.GroupPath,
                Tags = instance.Tags,
                Enabled = instance.Enabled,
                EntryMode = prototype.EntryMode,
                Event = prototype.Event,
                Phase = prototype.Phase,
                Priority = prototype.Priority,
                InterruptPriority = prototype.InterruptPriority,
                Scope = prototype.Scope,
                AllowExternal = prototype.AllowExternal,
                Schedule = prototype.Schedule,
                Cue = prototype.Cue,
                ExecutionControl = prototype.ExecutionControl,
                Template = instance.Template,
                Condition = prototype.Condition,
                Actions = prototype.Actions,
                Blackboard = prototype.Blackboard,
                Note = prototype.Note
            };
        }

        public static TriggerDefinitionData CreateMaterialized(
            TriggerDefinitionData instance,
            TriggerAuthoringTemplateData template,
            out string error)
        {
            return CreateMaterialized(instance, template, null, out error);
        }

        public static TriggerDefinitionData CreateMaterialized(
            TriggerDefinitionData instance,
            TriggerAuthoringTemplateData template,
            IReadOnlyDictionary<string, string> internalLocalKeyMap,
            out string error)
        {
            error = null;
            var effective = CreateEffective(instance, template);
            if (effective == null) return null;
            RewriteInternalLocalReferences(effective.Condition, internalLocalKeyMap);
            RewriteInternalLocalReferences(effective.Actions, internalLocalKeyMap);
            RenameInternalLocalDeclarations(effective.Blackboard, internalLocalKeyMap);
            if (!RewriteInputReferences(effective.Condition, template.Parameters, instance?.Template, false, out error) ||
                !RewriteInputReferences(effective.Actions, template.Parameters, instance?.Template, false, out error))
                return null;
            RemoveInputLocalDeclarations(effective.Blackboard, template.Parameters);
            effective.Template = null;
            return effective;
        }

        public static void CopyInto(TriggerDefinitionData target, TriggerDefinitionData source)
        {
            if (target == null || source == null) return;
            var copy = CloneDefinition(source);
            target.Id = copy.Id;
            target.Name = copy.Name;
            target.GroupPath = copy.GroupPath;
            target.Tags = copy.Tags;
            target.Enabled = copy.Enabled;
            target.EntryMode = copy.EntryMode;
            target.Event = copy.Event;
            target.Phase = copy.Phase;
            target.Priority = copy.Priority;
            target.InterruptPriority = copy.InterruptPriority;
            target.Scope = copy.Scope;
            target.AllowExternal = copy.AllowExternal;
            target.Schedule = copy.Schedule;
            target.Cue = copy.Cue;
            target.ExecutionControl = copy.ExecutionControl;
            target.Template = copy.Template;
            target.Condition = copy.Condition;
            target.Actions = copy.Actions;
            target.Blackboard = copy.Blackboard;
            target.Note = copy.Note;
        }

        public static TriggerDefinitionData CloneDefinition(TriggerDefinitionData source)
        {
            if (source == null) return new TriggerDefinitionData();
            var clone = new TriggerDefinitionData
            {
                Id = source.Id,
                Name = source.Name,
                GroupPath = source.GroupPath,
                Tags = source.Tags != null ? new List<string>(source.Tags) : new List<string>(),
                Enabled = source.Enabled,
                EntryMode = source.EntryMode,
                Event = source.Event,
                Phase = source.Phase,
                Priority = source.Priority,
                InterruptPriority = source.InterruptPriority,
                Scope = source.Scope,
                AllowExternal = source.AllowExternal,
                Schedule = source.Schedule == null ? new TriggerScheduleData() : new TriggerScheduleData
                {
                    Mode = source.Schedule.Mode,
                    DelayMilliseconds = source.Schedule.DelayMilliseconds,
                    IntervalMilliseconds = source.Schedule.IntervalMilliseconds,
                    RepeatCount = source.Schedule.RepeatCount
                },
                Cue = source.Cue == null ? new TriggerCueData() : new TriggerCueData { CueId = source.Cue.CueId },
                ExecutionControl = source.ExecutionControl == null
                    ? new TriggerExecutionControlData()
                    : new TriggerExecutionControlData
                    {
                        InterruptPolicy = source.ExecutionControl.InterruptPolicy,
                        StopPropagationOnSuccess = source.ExecutionControl.StopPropagationOnSuccess,
                        StopPropagationOnFailure = source.ExecutionControl.StopPropagationOnFailure
                    },
                Template = source.Template,
                Condition = TriggerAuthoringGroupResolver.CloneNode(source.Condition),
                Actions = TriggerAuthoringGroupResolver.CloneNode(source.Actions),
                Note = source.Note
            };
            var variables = source.Blackboard;
            if (variables != null)
            {
                for (var i = 0; i < variables.Count; i++)
                {
                    var variable = variables[i];
                    if (variable == null) continue;
                    clone.Blackboard.Add(new TriggerBlackboardVariableData
                    {
                        Key = variable.Key,
                        Type = variable.Type,
                        ReadOnly = variable.ReadOnly,
                        Description = variable.Description,
                        DefaultValue = TriggerAuthoringGroupResolver.CloneValue(variable.DefaultValue)
                    });
                }
            }
            return clone;
        }

        private static void RewriteInputReferences(
            TriggerNodeData node,
            IReadOnlyList<TriggerAuthoringTemplateParameterData> parameters,
            TriggerTemplateReferenceData reference,
            bool toRuntimeParameter)
        {
            RewriteInputReferences(node, parameters, reference, toRuntimeParameter, out _);
        }

        private static bool RewriteInputReferences(
            TriggerNodeData node,
            IReadOnlyList<TriggerAuthoringTemplateParameterData> parameters,
            TriggerTemplateReferenceData reference,
            bool toRuntimeParameter,
            out string error)
        {
            error = null;
            if (node == null) return true;
            var arguments = node.Arguments;
            if (arguments != null)
                for (var i = 0; i < arguments.Count; i++)
                    if (!RewriteInputValue(arguments[i]?.Value, parameters, reference, toRuntimeParameter, out error))
                        return false;
            if (!RewriteInputReferences(node.Condition, parameters, reference, toRuntimeParameter, out error)) return false;
            if (!RewriteInputReferences(node.Children, parameters, reference, toRuntimeParameter, out error)) return false;
            return RewriteInputReferences(node.ElseChildren, parameters, reference, toRuntimeParameter, out error);
        }

        private static bool RewriteInputReferences(
            IReadOnlyList<TriggerNodeData> nodes,
            IReadOnlyList<TriggerAuthoringTemplateParameterData> parameters,
            TriggerTemplateReferenceData reference,
            bool toRuntimeParameter,
            out string error)
        {
            error = null;
            if (nodes == null) return true;
            for (var i = 0; i < nodes.Count; i++)
                if (!RewriteInputReferences(nodes[i], parameters, reference, toRuntimeParameter, out error))
                    return false;
            return true;
        }

        private static bool RewriteInputValue(
            TriggerValueRefData value,
            IReadOnlyList<TriggerAuthoringTemplateParameterData> parameters,
            TriggerTemplateReferenceData reference,
            bool toRuntimeParameter,
            out string error)
        {
            error = null;
            if (value == null) return true;
            if (value.Source == TriggerValueSource.LocalBlackboard &&
                TriggerAuthoringLocalBlackboardPath.TryParse(value.Path, out var scope, out var key) &&
                scope != TriggerAuthoringLocalBlackboardScope.Module)
            {
                var parameter = FindParameterByLocalKey(parameters, key);
                if (parameter != null)
                {
                    if (toRuntimeParameter)
                    {
                        value.Source = TriggerValueSource.TemplateParameter;
                        value.Path = parameter.Name;
                    }
                    else
                    {
                        var bound = FindBinding(reference, parameter.Name) ??
                                    (parameter.HasDefault ? parameter.DefaultValue : null);
                        if (bound == null)
                        {
                            error = $"模板输入“{parameter.Name}”没有实例绑定或默认值，无法转为本地副本。";
                            return false;
                        }
                        CopyValue(value, bound);
                    }
                }
            }
            var fields = value.Fields;
            if (fields != null)
                for (var i = 0; i < fields.Count; i++)
                    if (!RewriteInputValue(fields[i]?.Value, parameters, reference, toRuntimeParameter, out error))
                        return false;
            return true;
        }

        private static TriggerValueRefData FindBinding(TriggerTemplateReferenceData reference, string name)
        {
            var bindings = reference?.Bindings;
            if (bindings == null) return null;
            for (var i = 0; i < bindings.Count; i++)
                if (bindings[i] != null && string.Equals(bindings[i].Name, name, StringComparison.Ordinal))
                    return bindings[i].Value;
            return null;
        }

        private static TriggerAuthoringTemplateParameterData FindParameterByLocalKey(
            IReadOnlyList<TriggerAuthoringTemplateParameterData> parameters,
            string key)
        {
            if (parameters == null) return null;
            for (var i = 0; i < parameters.Count; i++)
                if (parameters[i] != null && string.Equals(parameters[i].LocalVariableKey, key, StringComparison.Ordinal))
                    return parameters[i];
            return null;
        }

        private static void RemoveInputLocalDeclarations(
            List<TriggerBlackboardVariableData> variables,
            IReadOnlyList<TriggerAuthoringTemplateParameterData> parameters)
        {
            if (variables == null || parameters == null) return;
            for (var i = variables.Count - 1; i >= 0; i--)
            {
                var variable = variables[i];
                if (variable != null && FindParameterByLocalKey(parameters, variable.Key) != null)
                    variables.RemoveAt(i);
            }
        }

        private static void RenameInternalLocalDeclarations(
            IReadOnlyList<TriggerBlackboardVariableData> variables,
            IReadOnlyDictionary<string, string> keyMap)
        {
            if (variables == null || keyMap == null) return;
            for (var i = 0; i < variables.Count; i++)
            {
                var variable = variables[i];
                if (variable != null && keyMap.TryGetValue(variable.Key ?? string.Empty, out var replacement))
                    variable.Key = replacement;
            }
        }

        private static void RewriteInternalLocalReferences(
            TriggerNodeData node,
            IReadOnlyDictionary<string, string> keyMap)
        {
            if (node == null || keyMap == null || keyMap.Count == 0) return;
            var arguments = node.Arguments;
            if (arguments != null)
                for (var i = 0; i < arguments.Count; i++)
                    RewriteInternalLocalValue(arguments[i]?.Value, keyMap);
            RewriteInternalLocalReferences(node.Condition, keyMap);
            RewriteInternalLocalReferences(node.Children, keyMap);
            RewriteInternalLocalReferences(node.ElseChildren, keyMap);
        }

        private static void RewriteInternalLocalReferences(
            IReadOnlyList<TriggerNodeData> nodes,
            IReadOnlyDictionary<string, string> keyMap)
        {
            if (nodes == null) return;
            for (var i = 0; i < nodes.Count; i++)
                RewriteInternalLocalReferences(nodes[i], keyMap);
        }

        private static void RewriteInternalLocalValue(
            TriggerValueRefData value,
            IReadOnlyDictionary<string, string> keyMap)
        {
            if (value == null) return;
            if (value.Source == TriggerValueSource.LocalBlackboard &&
                TriggerAuthoringLocalBlackboardPath.TryParse(value.Path, out var scope, out var key) &&
                scope != TriggerAuthoringLocalBlackboardScope.Module &&
                keyMap.TryGetValue(key, out var replacement))
            {
                value.Path = TriggerAuthoringLocalBlackboardPath.Format(
                    TriggerAuthoringLocalBlackboardScope.Trigger,
                    replacement);
            }
            var fields = value.Fields;
            if (fields != null)
                for (var i = 0; i < fields.Count; i++)
                    RewriteInternalLocalValue(fields[i]?.Value, keyMap);
        }

        private static TriggerBlackboardVariableData FindVariable(
            IReadOnlyList<TriggerBlackboardVariableData> variables,
            string key)
        {
            if (variables == null) return null;
            for (var i = 0; i < variables.Count; i++)
                if (variables[i] != null && string.Equals(variables[i].Key, key, StringComparison.Ordinal))
                    return variables[i];
            return null;
        }

        private static TriggerValueRefData CreateDefaultValue(TriggerValueType type)
        {
            return new TriggerValueRefData { Source = TriggerValueSource.Constant, Type = type };
        }

        private static bool ValuesEqual(TriggerValueRefData left, TriggerValueRefData right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Source != right.Source || left.Type != right.Type) return false;
            switch (left.Type)
            {
                case TriggerValueType.Integer:
                case TriggerValueType.Entity:
                case TriggerValueType.ObjectId:
                    return left.IntegerValue == right.IntegerValue;
                case TriggerValueType.Number:
                    return left.NumberValue.Equals(right.NumberValue);
                case TriggerValueType.Boolean:
                    return left.BooleanValue == right.BooleanValue;
                case TriggerValueType.String:
                    return string.Equals(left.StringValue, right.StringValue, StringComparison.Ordinal);
                default:
                    return string.Equals(left.Path, right.Path, StringComparison.Ordinal) &&
                           string.Equals(left.Expression, right.Expression, StringComparison.Ordinal);
            }
        }

        private static void CopyValue(TriggerValueRefData target, TriggerValueRefData source)
        {
            var copy = TriggerAuthoringGroupResolver.CloneValue(source);
            target.Source = copy.Source;
            target.Type = copy.Type;
            target.Path = copy.Path;
            target.IntegerValue = copy.IntegerValue;
            target.NumberValue = copy.NumberValue;
            target.BooleanValue = copy.BooleanValue;
            target.StringValue = copy.StringValue;
            target.IntegerListValue = copy.IntegerListValue;
            target.Vector3Value = copy.Vector3Value;
            target.Fields = copy.Fields;
            target.Expression = copy.Expression;
        }
    }

    internal static class TriggerAuthoringTemplateValidator
    {
        public static List<TriggerAuthoringDiagnostic> Validate(
            TriggerAuthoringTemplateData template,
            TriggerAuthoringValidationContext context = null)
        {
            context = context ?? new TriggerAuthoringValidationContext();
            var diagnostics = new List<TriggerAuthoringDiagnostic>();
            if (template == null)
            {
                AddError(diagnostics, "TRG1610", "template", "模板为空。");
                return diagnostics;
            }
            TriggerAuthoringTemplateDefinition.Normalize(template);
            var definition = template.Definition;

            if (string.IsNullOrWhiteSpace(template.TemplateId))
                AddError(diagnostics, "TRG1610", "template.templateId", "必须填写 Template ID。");
            if (string.IsNullOrWhiteSpace(template.TemplateVersion))
                AddError(diagnostics, "TRG1611", "template.templateVersion", "必须填写模板版本。");
            if (definition == null)
                AddError(diagnostics, "TRG1612", "template.definition", "模板必须包含完整触发器定义。");
            else if (definition.EntryMode == TriggerEntryMode.Event && string.IsNullOrWhiteSpace(definition.Event))
                AddError(diagnostics, "TRG1612", "template.definition.event", "事件触发模板必须设置事件。");
            if (definition?.Actions == null)
                AddError(diagnostics, "TRG1616", "template.definition.actions", "模板必须包含执行行为。");
            if (definition?.Template != null)
                AddError(diagnostics, "TRG1622", "template.definition.template", "模板原型不能再次绑定另一个触发器模板。");

            var parameters = BuildParameterMap(template, diagnostics);
            ValidateTemplateInputs(template, parameters, diagnostics);
            ValidateTemplateNode(diagnostics, definition?.Condition, "template.definition.condition");
            ValidateTemplateNode(diagnostics, definition?.Actions, "template.definition.actions");

            var syntheticModule = new TriggerAuthoringModuleData
            {
                ModuleId = "template:" + (template.TemplateId ?? string.Empty),
            };
            var syntheticTrigger = TriggerAuthoringTemplateDefinition.CloneDefinition(definition);
            syntheticTrigger.Id = 1;
            syntheticTrigger.Template = null;
            syntheticModule.Triggers.Add(syntheticTrigger);
            var nodeDiagnostics = TriggerAuthoringValidator.Validate(syntheticModule, new TriggerAuthoringValidationContext
            {
                Types = context.Types,
                Events = context.Events,
                GlobalBlackboard = context.GlobalBlackboard
            });
            for (var i = 0; i < nodeDiagnostics.Count; i++)
            {
                var diagnostic = nodeDiagnostics[i];
                if (diagnostic.Code == "TRG1001" || diagnostic.Code == "TRG1003" ||
                    diagnostic.Code == "TRG1005" || diagnostic.Code == "TRG1200")
                    continue;
                diagnostics.Add(new TriggerAuthoringDiagnostic(
                    diagnostic.Code,
                    diagnostic.Severity,
                    RewriteSyntheticPath(diagnostic.Path),
                    diagnostic.Message));
            }
            return diagnostics;
        }

        public static bool TryResolveReference(
            TriggerTemplateReferenceData reference,
            TriggerDefinitionData trigger,
            string path,
            TriggerAuthoringValidationContext context,
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            out TriggerAuthoringTemplateAsset asset)
        {
            asset = null;
            if (reference == null) return false;
            if (string.IsNullOrWhiteSpace(reference.TemplateId))
            {
                AddError(diagnostics, "TRG1600", path + ".templateId", "必须填写 Template ID。");
                return false;
            }
            if (context?.Templates == null)
            {
                AddError(diagnostics, "TRG1600", path + ".templateId", $"尚未分配模板目录：{reference.TemplateId}。");
                return false;
            }
            if (context.Templates.IsAmbiguous(reference.TemplateId))
            {
                AddError(diagnostics, "TRG1601", path + ".templateId", $"项目目录中存在重复的 Template ID：{reference.TemplateId}。");
                return false;
            }
            if (!context.Templates.TryGet(reference.TemplateId, out asset) || asset?.Template == null)
            {
                AddError(diagnostics, "TRG1600", path + ".templateId", $"未找到模板：{reference.TemplateId}。");
                return false;
            }

            var template = asset.Template;
            if (string.IsNullOrWhiteSpace(reference.Version) ||
                !string.Equals(reference.Version, template.TemplateVersion, StringComparison.Ordinal))
            {
                AddError(
                    diagnostics,
                    "TRG1602",
                    path + ".version",
                    $"模板版本必须完全匹配。请求版本='{reference.Version ?? string.Empty}'，资产版本='{template.TemplateVersion ?? string.Empty}'。");
            }
            if (trigger != null && (trigger.Condition != null || trigger.Actions != null))
            {
                AddError(
                    diagnostics,
                    "TRG1603",
                    path,
                    "模板实例不能同时包含本地条件树或行为树。");
            }
            if (trigger?.Blackboard != null && trigger.Blackboard.Count > 0)
            {
                AddError(
                    diagnostics,
                    "TRG1604",
                    path,
                    "模板实例不能另外声明触发器局部变量；局部变量由模板原型统一定义。");
            }

            var templateDiagnostics = Validate(template, context);
            for (var i = 0; i < templateDiagnostics.Count; i++)
            {
                var diagnostic = templateDiagnostics[i];
                diagnostics.Add(new TriggerAuthoringDiagnostic(
                    diagnostic.Code,
                    diagnostic.Severity,
                    path + ".asset." + TrimTemplatePrefix(diagnostic.Path),
                    diagnostic.Message));
            }
            return true;
        }

        public static Dictionary<string, TriggerAuthoringTemplateParameterData> BuildParameterMap(
            TriggerAuthoringTemplateData template,
            ICollection<TriggerAuthoringDiagnostic> diagnostics = null)
        {
            var result = new Dictionary<string, TriggerAuthoringTemplateParameterData>(StringComparer.Ordinal);
            var parameters = template?.Parameters ?? new List<TriggerAuthoringTemplateParameterData>();
            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                var path = $"template.parameters[{i}]";
                if (parameter == null)
                {
                    AddError(diagnostics, "TRG1613", path, "模板参数为空。");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(parameter.Name))
                {
                    AddError(diagnostics, "TRG1613", path + ".name", "必须填写模板参数名称。");
                    continue;
                }
                if (result.ContainsKey(parameter.Name))
                {
                    AddError(diagnostics, "TRG1613", path + ".name", $"模板参数重复：{parameter.Name}。");
                    continue;
                }
                result.Add(parameter.Name, parameter);
                if (string.IsNullOrWhiteSpace(parameter.LocalVariableKey))
                    AddError(diagnostics, "TRG1620", path + ".localVariableKey", "模板输入必须绑定一个触发器 LocalVar。");
                if (parameter.Type == TriggerValueType.None)
                    AddError(diagnostics, "TRG1614", path + ".type", "必须设置模板参数类型。");
                if (parameter.AllowedSources == TriggerTemplateValueSourceMask.None)
                    AddError(diagnostics, "TRG1614", path + ".allowedSources", "必须至少允许一种绑定来源。");
                if ((parameter.AllowedSources & TriggerTemplateValueSourceMask.TemplateParameter) != 0)
                    AddError(diagnostics, "TRG1619", path + ".allowedSources", "TemplateParameter 不能作为实例绑定来源。");
                if (parameter.HasDefault)
                {
                    if (parameter.DefaultValue == null || parameter.DefaultValue.Source != TriggerValueSource.Constant)
                        AddError(diagnostics, "TRG1615", path + ".defaultValue", "模板默认值必须是常量。");
                    else if (!IsTypeCompatible(parameter.Type, parameter.DefaultValue.Type))
                        AddError(diagnostics, "TRG1615", path + ".defaultValue.type", $"默认值类型必须为 {parameter.Type}，当前为 {parameter.DefaultValue.Type}。");
                }
            }
            return result;
        }

        private static void ValidateTemplateInputs(
            TriggerAuthoringTemplateData template,
            IReadOnlyDictionary<string, TriggerAuthoringTemplateParameterData> parameters,
            ICollection<TriggerAuthoringDiagnostic> diagnostics)
        {
            var definition = template?.Definition;
            var variables = definition?.Blackboard;
            var usedKeys = new HashSet<string>(StringComparer.Ordinal);
            if (parameters == null) return;
            foreach (var pair in parameters)
            {
                var parameter = pair.Value;
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.LocalVariableKey)) continue;
                var path = "template.parameters." + parameter.Name;
                if (!usedKeys.Add(parameter.LocalVariableKey))
                {
                    AddError(diagnostics, "TRG1620", path + ".localVariableKey",
                        $"多个模板输入不能写入同一个 LocalVar：{parameter.LocalVariableKey}。");
                    continue;
                }

                TriggerBlackboardVariableData variable = null;
                if (variables != null)
                    for (var i = 0; i < variables.Count; i++)
                        if (variables[i] != null && string.Equals(variables[i].Key, parameter.LocalVariableKey, StringComparison.Ordinal))
                        {
                            variable = variables[i];
                            break;
                        }
                if (variable == null)
                    AddError(diagnostics, "TRG1620", path + ".localVariableKey",
                        $"未找到模板输入对应的触发器 LocalVar：{parameter.LocalVariableKey}。");
                else if (!IsTypeCompatible(variable.Type, parameter.Type))
                    AddError(diagnostics, "TRG1621", path + ".type",
                        $"输入类型 {parameter.Type} 与 LocalVar 类型 {variable.Type} 不一致。");
                else if (!variable.ReadOnly)
                    AddError(diagnostics, "TRG1621", path + ".localVariableKey",
                        "作为模板输入的 LocalVar 必须是只读变量。");
            }
        }

        public static string BuildMessage(IReadOnlyList<TriggerAuthoringDiagnostic> diagnostics)
        {
            var builder = new StringBuilder("触发器模板校验失败：");
            if (diagnostics == null) return builder.ToString();
            for (var i = 0; i < diagnostics.Count; i++)
            {
                var diagnostic = diagnostics[i];
                if (diagnostic.Severity != TriggerAuthoringDiagnosticSeverity.Error) continue;
                builder.AppendLine();
                builder.Append(diagnostic.Code).Append(' ').Append(diagnostic.Path).Append(": ").Append(diagnostic.Message);
            }
            return builder.ToString();
        }

        internal static bool IsTypeCompatible(TriggerValueType expected, TriggerValueType actual)
        {
            return expected == TriggerValueType.None || expected == actual ||
                   expected == TriggerValueType.Number && actual == TriggerValueType.Integer;
        }

        private static void ValidateTemplateNode(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerNodeData node,
            string path)
        {
            if (node == null) return;
            if (!string.IsNullOrWhiteSpace(node.GroupReference))
                AddError(diagnostics, "TRG1617", path + ".groupReference", "模板树不能引用模块级分组。");
            var arguments = node.Arguments ?? new List<TriggerArgumentData>();
            for (var i = 0; i < arguments.Count; i++)
                ValidateTemplateValue(diagnostics, arguments[i]?.Value, $"{path}.arguments[{i}].value");
            var children = node.Children ?? new List<TriggerNodeData>();
            for (var i = 0; i < children.Count; i++)
                ValidateTemplateNode(diagnostics, children[i], $"{path}.children[{i}]");
            ValidateTemplateNode(diagnostics, node.Condition, path + ".condition");
            var elseChildren = node.ElseChildren ?? new List<TriggerNodeData>();
            for (var i = 0; i < elseChildren.Count; i++)
                ValidateTemplateNode(diagnostics, elseChildren[i], $"{path}.elseChildren[{i}]");
        }

        private static void ValidateTemplateValue(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerValueRefData value,
            string path)
        {
            if (value == null) return;
            if (value.Source == TriggerValueSource.TemplateParameter)
                AddError(
                    diagnostics,
                    "TRG1618",
                    path + ".source",
                    "模板节点不能直接读取 TemplateParameter；请读取对应的 Trigger LocalVar。");
            var fields = value.Fields ?? new List<TriggerArgumentData>();
            for (var i = 0; i < fields.Count; i++)
                ValidateTemplateValue(diagnostics, fields[i]?.Value, $"{path}.fields[{i}].value");
        }

        private static string RewriteSyntheticPath(string path)
        {
            const string triggerPrefix = "module.triggers[0]";
            if (path != null && path.StartsWith(triggerPrefix, StringComparison.Ordinal))
                return "template.definition" + path.Substring(triggerPrefix.Length);
            return path == "module" ? "template.definition" : path;
        }

        private static string TrimTemplatePrefix(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            return path.StartsWith("template.", StringComparison.Ordinal) ? path.Substring(9) : path;
        }

        private static void AddError(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            string code,
            string path,
            string message)
        {
            diagnostics?.Add(new TriggerAuthoringDiagnostic(
                code,
                TriggerAuthoringDiagnosticSeverity.Error,
                path,
                message));
        }
    }
}
#endif
