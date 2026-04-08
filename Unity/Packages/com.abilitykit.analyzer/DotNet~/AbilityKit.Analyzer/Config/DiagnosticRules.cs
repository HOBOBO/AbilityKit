using Microsoft.CodeAnalysis;

namespace AbilityKit.Analyzer
{
    public static class DiagnosticRules
    {
        public static readonly DiagnosticDescriptor ForbiddenNamespaceRule = new DiagnosticDescriptor(
            id: DiagnosticIds.ForbiddenNamespaceAnalyzerRuleId,
            title: "Forbidden namespace",
            messageFormat: "Forbidden namespace '{0}' in assembly '{1}'",
            category: "AbilityKit.Framework",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ForbiddenAssemblyRule = new DiagnosticDescriptor(
            id: DiagnosticIds.ForbiddenAssemblyAnalyzerRuleId,
            title: "Forbidden assembly",
            messageFormat: "Forbidden assembly '{0}' in assembly '{1}'",
            category: "AbilityKit.Framework",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnmatchedConstraintPackageRule = new DiagnosticDescriptor(
            id: DiagnosticIds.UnmatchedConstraintPackageRuleId,
            title: "Unmatched constraint",
            messageFormat: "Constraint package '{0}' not found",
            category: "AbilityKit.Maintainability",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: false);
    }
}
