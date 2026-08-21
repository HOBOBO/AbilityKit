#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using AbilityKit.Ability.Config.Authoring;

namespace AbilityKit.Ability.Editor.Utilities
{
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
                AddError(diagnostics, "TRG1610", "template", "Template is null.");
                return diagnostics;
            }

            if (string.IsNullOrWhiteSpace(template.TemplateId))
                AddError(diagnostics, "TRG1610", "template.templateId", "TemplateId is required.");
            if (string.IsNullOrWhiteSpace(template.TemplateVersion))
                AddError(diagnostics, "TRG1611", "template.templateVersion", "TemplateVersion is required.");
            if (string.IsNullOrWhiteSpace(template.Event))
                AddError(diagnostics, "TRG1612", "template.event", "Template event is required.");
            if (template.Actions == null)
                AddError(diagnostics, "TRG1616", "template.actions", "Template actions are required.");

            var parameters = BuildParameterMap(template, diagnostics);
            ValidateTemplateNode(diagnostics, template.Condition, "template.condition", parameters);
            ValidateTemplateNode(diagnostics, template.Actions, "template.actions", parameters);
            ValidateTemplateParameterReferences(template.Condition, "template.condition", parameters, diagnostics);
            ValidateTemplateParameterReferences(template.Actions, "template.actions", parameters, diagnostics);

            var syntheticModule = new TriggerAuthoringModuleData
            {
                ModuleId = "template:" + (template.TemplateId ?? string.Empty),
                Triggers =
                {
                    new TriggerDefinitionData
                    {
                        Id = 1,
                        Event = template.Event,
                        Condition = TriggerAuthoringGroupResolver.CloneNode(template.Condition),
                        Actions = TriggerAuthoringGroupResolver.CloneNode(template.Actions)
                    }
                }
            };
            var nodeDiagnostics = TriggerAuthoringValidator.Validate(syntheticModule, new TriggerAuthoringValidationContext
            {
                Types = context.Types,
                Events = context.Events,
                GlobalBlackboard = context.GlobalBlackboard
            });
            for (var i = 0; i < nodeDiagnostics.Count; i++)
            {
                var diagnostic = nodeDiagnostics[i];
                if (diagnostic.Code == "TRG1001" || diagnostic.Code == "TRG1003") continue;
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
                AddError(diagnostics, "TRG1600", path + ".templateId", "TemplateId is required.");
                return false;
            }
            if (context?.Templates == null)
            {
                AddError(diagnostics, "TRG1600", path + ".templateId", $"Template catalog is not assigned: {reference.TemplateId}.");
                return false;
            }
            if (context.Templates.IsAmbiguous(reference.TemplateId))
            {
                AddError(diagnostics, "TRG1601", path + ".templateId", $"TemplateId is ambiguous in the project catalog: {reference.TemplateId}.");
                return false;
            }
            if (!context.Templates.TryGet(reference.TemplateId, out asset) || asset?.Template == null)
            {
                AddError(diagnostics, "TRG1600", path + ".templateId", $"Template was not found: {reference.TemplateId}.");
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
                    $"Template version must exactly match. requested='{reference.Version ?? string.Empty}', asset='{template.TemplateVersion ?? string.Empty}'.");
            }
            if (trigger != null && (trigger.Condition != null || trigger.Actions != null))
            {
                AddError(
                    diagnostics,
                    "TRG1603",
                    path,
                    "A template instance cannot also contain local Condition or Actions trees.");
            }
            if (trigger != null && !string.Equals(trigger.Event, template.Event, StringComparison.Ordinal))
            {
                AddError(
                    diagnostics,
                    "TRG1604",
                    path + ".templateId",
                    $"Template event '{template.Event ?? string.Empty}' does not match trigger event '{trigger.Event ?? string.Empty}'.");
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
                    AddError(diagnostics, "TRG1613", path, "Template parameter is null.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(parameter.Name))
                {
                    AddError(diagnostics, "TRG1613", path + ".name", "Template parameter name is required.");
                    continue;
                }
                if (result.ContainsKey(parameter.Name))
                {
                    AddError(diagnostics, "TRG1613", path + ".name", $"Duplicate template parameter: {parameter.Name}.");
                    continue;
                }
                result.Add(parameter.Name, parameter);
                if (parameter.Type == TriggerValueType.None)
                    AddError(diagnostics, "TRG1614", path + ".type", "Template parameter type is required.");
                if (parameter.AllowedSources == TriggerTemplateValueSourceMask.None)
                    AddError(diagnostics, "TRG1614", path + ".allowedSources", "At least one binding source must be allowed.");
                if ((parameter.AllowedSources & TriggerTemplateValueSourceMask.TemplateParameter) != 0)
                    AddError(diagnostics, "TRG1619", path + ".allowedSources", "TemplateParameter cannot be used as an instance binding source.");
                if (parameter.HasDefault)
                {
                    if (parameter.DefaultValue == null || parameter.DefaultValue.Source != TriggerValueSource.Constant)
                        AddError(diagnostics, "TRG1615", path + ".defaultValue", "Template defaults must be constant values.");
                    else if (!IsTypeCompatible(parameter.Type, parameter.DefaultValue.Type))
                        AddError(diagnostics, "TRG1615", path + ".defaultValue.type", $"Default type must be {parameter.Type}, got {parameter.DefaultValue.Type}.");
                }
            }
            return result;
        }

        public static void ValidateTemplateParameterReferences(
            TriggerNodeData node,
            string path,
            IReadOnlyDictionary<string, TriggerAuthoringTemplateParameterData> parameters,
            ICollection<TriggerAuthoringDiagnostic> diagnostics)
        {
            if (node == null) return;
            var arguments = node.Arguments ?? new List<TriggerArgumentData>();
            for (var i = 0; i < arguments.Count; i++)
            {
                var value = arguments[i]?.Value;
                if (value?.Source != TriggerValueSource.TemplateParameter) continue;
                var valuePath = $"{path}.arguments[{i}].value";
                if (parameters == null || !parameters.TryGetValue(value.Path ?? string.Empty, out var parameter))
                    AddError(diagnostics, "TRG1618", valuePath + ".path", $"Unknown template parameter: {value.Path ?? string.Empty}.");
                else if (!IsTypeCompatible(parameter.Type, value.Type))
                    AddError(diagnostics, "TRG1618", valuePath + ".type", $"Template parameter '{parameter.Name}' is {parameter.Type}, got {value.Type}.");
            }
            var children = node.Children ?? new List<TriggerNodeData>();
            for (var i = 0; i < children.Count; i++)
                ValidateTemplateParameterReferences(children[i], $"{path}.children[{i}]", parameters, diagnostics);
        }

        public static string BuildMessage(IReadOnlyList<TriggerAuthoringDiagnostic> diagnostics)
        {
            var builder = new StringBuilder("Trigger template validation failed:");
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
            string path,
            IReadOnlyDictionary<string, TriggerAuthoringTemplateParameterData> parameters)
        {
            if (node == null) return;
            if (!string.IsNullOrWhiteSpace(node.GroupReference))
                AddError(diagnostics, "TRG1617", path + ".groupReference", "Template trees cannot reference module-local groups.");
            var children = node.Children ?? new List<TriggerNodeData>();
            for (var i = 0; i < children.Count; i++)
                ValidateTemplateNode(diagnostics, children[i], $"{path}.children[{i}]", parameters);
        }

        private static string RewriteSyntheticPath(string path)
        {
            const string triggerPrefix = "module.triggers[0]";
            if (path != null && path.StartsWith(triggerPrefix, StringComparison.Ordinal))
                return "template" + path.Substring(triggerPrefix.Length);
            return path == "module" ? "template" : path;
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
