using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AbilityKit.Demo.Moba.CodeGen
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MobaSnapshotEmitterAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            MobaDiagnosticRules.InvalidSnapshotEmitterRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(startContext =>
            {
                var attributeType = startContext.Compilation.GetTypeByMetadataName(
                    MobaSnapshotEmitterContract.AttributeMetadataName);
                var interfaceType = startContext.Compilation.GetTypeByMetadataName(
                    MobaSnapshotEmitterContract.InterfaceMetadataName);
                var manifestType = startContext.Compilation.GetTypeByMetadataName(
                    MobaSnapshotEmitterContract.ManifestMetadataName);
                if (attributeType == null || interfaceType == null ||
                    !MobaSnapshotEmitterContract.IsSourceManifest(manifestType))
                {
                    return;
                }

                startContext.RegisterSymbolAction(
                    symbolContext => AnalyzeNamedType(symbolContext, attributeType, interfaceType),
                    SymbolKind.NamedType);
            });
        }

        private static void AnalyzeNamedType(
            SymbolAnalysisContext context,
            INamedTypeSymbol attributeType,
            INamedTypeSymbol interfaceType)
        {
            if (!(context.Symbol is INamedTypeSymbol type) ||
                !type.Locations.Any(location => location.IsInSource))
            {
                return;
            }

            var attribute = type.GetAttributes().FirstOrDefault(candidate =>
                SymbolEqualityComparer.Default.Equals(candidate.AttributeClass, attributeType));
            if (attribute == null)
            {
                return;
            }

            if (!MobaSnapshotEmitterContract.TryCreateMapping(
                    type,
                    interfaceType,
                    attribute,
                    out _,
                    out var error))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MobaDiagnosticRules.InvalidSnapshotEmitterRule,
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
