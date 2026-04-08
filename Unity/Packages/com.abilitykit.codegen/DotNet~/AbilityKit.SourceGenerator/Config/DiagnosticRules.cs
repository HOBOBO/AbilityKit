using Microsoft.CodeAnalysis;

namespace Share.SourceGenerator
{
    public static class DiagnosticRules
    {
        public static readonly DiagnosticDescriptor GenerateGetComponentRule = new DiagnosticDescriptor(
            id: DiagnosticIds.GenerateGetComponentAnalyzerRuleId,
            title: "Generate GetComponent",
            messageFormat: "Failed to generate GetComponent: {0}",
            category: DiagnosticCategories.Generator,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor GenerateEntitySerializeFormatterRule = new DiagnosticDescriptor(
            id: DiagnosticIds.GenerateEntitySerializeFormatterAnalyzerRuleId,
            title: "Generate Entity Serialize Formatter",
            messageFormat: "Failed to generate Entity Serialize Formatter: {0}",
            category: DiagnosticCategories.Generator,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor GenerateSystemRule = new DiagnosticDescriptor(
            id: DiagnosticIds.GenerateSystemAnalyzerRuleId,
            title: "Generate System",
            messageFormat: "Failed to generate System: {0}",
            category: DiagnosticCategories.Generator,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}