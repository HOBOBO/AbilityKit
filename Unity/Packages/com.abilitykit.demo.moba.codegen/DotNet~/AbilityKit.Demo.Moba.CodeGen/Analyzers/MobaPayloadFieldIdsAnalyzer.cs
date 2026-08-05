using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AbilityKit.Demo.Moba.CodeGen
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MobaPayloadFieldIdsAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            MobaDiagnosticRules.InvalidPayloadFieldIdsDeclarationRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(startContext =>
            {
                var attributeType = startContext.Compilation.GetTypeByMetadataName(
                    MobaPayloadFieldIdsContract.AttributeMetadataName);
                if (attributeType == null)
                {
                    return;
                }

                startContext.RegisterSymbolAction(
                    symbolContext => AnalyzeNamedType(
                        symbolContext,
                        startContext.Compilation,
                        attributeType),
                    SymbolKind.NamedType);
            });
        }

        private static void AnalyzeNamedType(
            SymbolAnalysisContext context,
            Compilation compilation,
            INamedTypeSymbol attributeType)
        {
            if (!(context.Symbol is INamedTypeSymbol type) ||
                !type.Locations.Any(location => location.IsInSource))
            {
                return;
            }

            var attributes = type.GetAttributes()
                .Where(candidate => SymbolEqualityComparer.Default.Equals(candidate.AttributeClass, attributeType))
                .ToArray();
            if (attributes.Length == 0)
            {
                return;
            }

            var validation = MobaPayloadFieldIdsContract.Validate(compilation, type, attributes);
            foreach (var error in validation.Errors)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MobaDiagnosticRules.InvalidPayloadFieldIdsDeclarationRule,
                    GetLocation(type),
                    type.Name,
                    error));
            }
        }

        private static Location GetLocation(INamedTypeSymbol type)
        {
            return type.Locations.FirstOrDefault(location => location.IsInSource) ?? Location.None;
        }
    }
}
