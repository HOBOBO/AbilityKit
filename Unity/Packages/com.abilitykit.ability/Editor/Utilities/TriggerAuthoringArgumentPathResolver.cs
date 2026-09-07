#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal sealed class TriggerAuthoringArgumentValueMatch
    {
        public TriggerAuthoringArgumentValueMatch(
            TriggerValueRefData value,
            string pathSuffix,
            string alias)
        {
            Value = value;
            PathSuffix = pathSuffix ?? string.Empty;
            Alias = alias ?? string.Empty;
        }

        public TriggerValueRefData Value { get; }
        public string PathSuffix { get; }
        public string Alias { get; }
    }

    internal static class TriggerAuthoringArgumentPathResolver
    {
        public static TriggerAuthoringArgumentValueMatch FindValue(
            TriggerNodeData node,
            params string[] aliases)
        {
            return FindValue(null, node, string.Empty, false, null, null, aliases);
        }

        public static TriggerAuthoringArgumentValueMatch FindValue(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerNodeData node,
            string nodePath,
            bool requireConstantObjectContainers,
            string diagnosticCode,
            string diagnosticMessage,
            params string[] aliases)
        {
            if (node?.Arguments == null || aliases == null) return null;
            var rejectedObjectRoots = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < aliases.Length; i++)
            {
                if (TryFindValue(
                    diagnostics,
                    node,
                    nodePath,
                    aliases[i],
                    requireConstantObjectContainers,
                    diagnosticCode,
                    diagnosticMessage,
                    rejectedObjectRoots,
                    out var match))
                {
                    return match;
                }
            }

            return null;
        }

        public static bool TryFindValue(
            TriggerNodeData node,
            string alias,
            out TriggerAuthoringArgumentValueMatch match)
        {
            return TryFindValue(null, node, string.Empty, alias, false, null, null, null, out match);
        }

        public static bool TryFindValue(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            TriggerNodeData node,
            string nodePath,
            string alias,
            bool requireConstantObjectContainers,
            string diagnosticCode,
            string diagnosticMessage,
            ISet<string> rejectedObjectRoots,
            out TriggerAuthoringArgumentValueMatch match)
        {
            match = null;
            if (string.IsNullOrWhiteSpace(alias)) return false;
            var parts = alias.Split('.');
            if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0])) return false;
            var argument = FindArgument(node, parts[0]);
            var value = argument?.Value;
            if (value == null) return false;

            var pathSuffix = ".arguments." + parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                if (value.Type != TriggerValueType.Object || value.Fields == null) return false;
                if (requireConstantObjectContainers && value.Source != TriggerValueSource.Constant)
                {
                    AddRejectedObjectDiagnostic(
                        diagnostics,
                        nodePath,
                        pathSuffix,
                        diagnosticCode,
                        diagnosticMessage,
                        rejectedObjectRoots);
                    return false;
                }

                var field = FindField(value.Fields, parts[i]);
                value = field?.Value;
                if (value == null) return false;
                pathSuffix += ".fields." + parts[i];
            }

            match = new TriggerAuthoringArgumentValueMatch(value, pathSuffix, alias);
            return true;
        }

        public static TriggerArgumentData FindArgument(TriggerNodeData node, string name)
        {
            if (node?.Arguments == null) return null;
            return FindField(node.Arguments, name);
        }

        public static TriggerArgumentData FindField(IReadOnlyList<TriggerArgumentData> fields, string name)
        {
            if (fields == null || string.IsNullOrWhiteSpace(name)) return null;
            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                if (field != null && string.Equals(field.Name, name, StringComparison.Ordinal)) return field;
            }

            return null;
        }

        public static string ComposeRuntimeArgumentName(string prefix, string fieldName)
        {
            prefix = prefix ?? string.Empty;
            fieldName = fieldName ?? string.Empty;
            if (string.IsNullOrEmpty(prefix)) return fieldName;
            if (string.IsNullOrEmpty(fieldName)) return prefix;
            return prefix + "_" + fieldName;
        }

        private static void AddRejectedObjectDiagnostic(
            ICollection<TriggerAuthoringDiagnostic> diagnostics,
            string nodePath,
            string pathSuffix,
            string diagnosticCode,
            string diagnosticMessage,
            ISet<string> rejectedObjectRoots)
        {
            if (diagnostics == null) return;
            if (rejectedObjectRoots != null && !rejectedObjectRoots.Add(pathSuffix)) return;
            diagnostics.Add(new TriggerAuthoringDiagnostic(
                string.IsNullOrWhiteSpace(diagnosticCode) ? "TRG2080" : diagnosticCode,
                TriggerAuthoringDiagnosticSeverity.Error,
                (nodePath ?? string.Empty) + pathSuffix + ".source",
                string.IsNullOrWhiteSpace(diagnosticMessage)
                    ? "Runtime Plan export can read Object fields only when the Object value is a constant field container."
                    : diagnosticMessage));
        }
    }
}
#endif
