using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AbilityKit.Demo.Moba.CodeGen
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MobaConfigTableAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            MobaDiagnosticRules.InvalidConfigTableRule,
            MobaDiagnosticRules.DuplicateConfigTableRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationAction(AnalyzeCompilation);
        }

        private static void AnalyzeCompilation(CompilationAnalysisContext context)
        {
            var attributeType = context.Compilation.GetTypeByMetadataName(
                MobaConfigTableContract.AttributeMetadataName);
            var manifestType = context.Compilation.GetTypeByMetadataName(
                MobaConfigTableContract.ManifestMetadataName);
            if (attributeType == null || !MobaConfigTableContract.IsSourceManifest(manifestType)) return;

            var uniqueness = new MobaConfigTableUniquenessTracker();
            foreach (var attribute in context.Compilation.Assembly.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType)) continue;

                if (!MobaConfigTableContract.TryCreateSpec(attribute, out var spec, out var error))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        MobaDiagnosticRules.InvalidConfigTableRule,
                        GetLocation(attribute),
                        error));
                    continue;
                }

                if (!uniqueness.TryAdd(spec, out var duplicateKind, out var duplicateKey))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        MobaDiagnosticRules.DuplicateConfigTableRule,
                        GetLocation(attribute),
                        duplicateKind,
                        duplicateKey));
                }
            }
        }

        private static Location GetLocation(AttributeData attribute)
        {
            return attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None;
        }
    }
}
