namespace AbilityKit.BehaviorTree.Diagnostics
{
    public sealed class ValidationDiagnostic
    {
        public string Code { get; }
        public ValidationSeverity Severity { get; }
        public string Message { get; }
        public string? NodeId { get; }
        public string? PropertyName { get; }
        public string? BlackboardKey { get; }

        public ValidationDiagnostic(
            string code,
            ValidationSeverity severity,
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

    }
}
