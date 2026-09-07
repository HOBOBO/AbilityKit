using System.Collections.Generic;

namespace AbilityKit.BehaviorTree
{
    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public enum BtValidationSeverity
    {
        Error = 0,
        Warning = 1,
    }

    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public sealed class BtValidationDiagnostic
    {
        public string Code { get; }
        public BtValidationSeverity Severity { get; }
        public string Message { get; }
        public string? NodeId { get; }
        public string? PropertyName { get; }
        public string? BlackboardKey { get; }

        public BtValidationDiagnostic(
            string code,
            BtValidationSeverity severity,
            string message,
            string? nodeId = null,
            string? propertyName = null,
            string? blackboardKey = null)
        {
            Code = code ?? "";
            Severity = severity;
            Message = message ?? "";
            NodeId = nodeId;
            PropertyName = propertyName;
            BlackboardKey = blackboardKey;
        }

        internal BtValidationDiagnostic(AbilityKit.BehaviorTree.Diagnostics.ValidationDiagnostic source)
            : this(
                source.Code,
                (BtValidationSeverity)(int)source.Severity,
                source.Message,
                source.NodeId,
                source.PropertyName,
                source.BlackboardKey)
        {
        }
    }

    /// <summary>
    /// Compatibility bridge for the obsolete prefixed validator API.
    /// </summary>
    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public static class BtTreeValidator
    {
        public static List<string> Validate(BtTreeDefinition definition, BtNodeRegistry registry)
            => AbilityKit.BehaviorTree.Diagnostics.TreeValidator.Validate(
                definition == null ? null! : AbilityKit.BehaviorTree.Definition.TreeDefinition.FromLegacy(definition),
                registry == null ? null! : AbilityKit.BehaviorTree.Registry.NodeRegistry.FromLegacy(registry));

        public static List<BtValidationDiagnostic> ValidateDiagnostics(BtTreeDefinition definition, BtNodeRegistry registry)
        {
            var canonical = AbilityKit.BehaviorTree.Diagnostics.TreeValidator.ValidateDiagnostics(
                definition == null ? null! : AbilityKit.BehaviorTree.Definition.TreeDefinition.FromLegacy(definition),
                registry == null ? null! : AbilityKit.BehaviorTree.Registry.NodeRegistry.FromLegacy(registry));
            var result = new List<BtValidationDiagnostic>(canonical.Count);
            foreach (var item in canonical)
            {
                result.Add(new BtValidationDiagnostic(item));
            }
            return result;
        }
    }
}
