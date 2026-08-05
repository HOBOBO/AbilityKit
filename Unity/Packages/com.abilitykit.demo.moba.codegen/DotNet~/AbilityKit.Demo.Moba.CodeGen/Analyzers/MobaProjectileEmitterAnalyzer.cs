using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AbilityKit.Demo.Moba.CodeGen
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MobaProjectileEmitterAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            MobaDiagnosticRules.InvalidProjectileEmitterRule,
            MobaDiagnosticRules.AmbiguousProjectileEmitterRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(startContext =>
            {
                var attributeType = startContext.Compilation.GetTypeByMetadataName(
                    MobaProjectileEmitterContract.AttributeMetadataName);
                var interfaceType = startContext.Compilation.GetTypeByMetadataName(
                    MobaProjectileEmitterContract.InterfaceMetadataName);
                if (attributeType == null || interfaceType == null)
                {
                    return;
                }

                var mappings = new ConcurrentBag<MobaProjectileEmitterMapping>();
                startContext.RegisterSymbolAction(
                    symbolContext => AnalyzeNamedType(symbolContext, attributeType, interfaceType, mappings),
                    SymbolKind.NamedType);
                startContext.RegisterCompilationEndAction(endContext => ReportAmbiguities(endContext, mappings));
            });
        }

        private static void AnalyzeNamedType(
            SymbolAnalysisContext context,
            INamedTypeSymbol attributeType,
            INamedTypeSymbol interfaceType,
            ConcurrentBag<MobaProjectileEmitterMapping> mappings)
        {
            if (!(context.Symbol is INamedTypeSymbol type) ||
                !type.Locations.Any(location => location.IsInSource))
            {
                return;
            }

            foreach (var attribute in type.GetAttributes().Where(candidate =>
                         SymbolEqualityComparer.Default.Equals(candidate.AttributeClass, attributeType)))
            {
                if (!MobaProjectileEmitterContract.TryCreateMapping(
                        type,
                        interfaceType,
                        attribute,
                        out var mapping,
                        out var error))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        MobaDiagnosticRules.InvalidProjectileEmitterRule,
                        GetLocation(type),
                        type.Name,
                        error));
                    continue;
                }

                mappings.Add(mapping);
            }
        }

        private static void ReportAmbiguities(
            CompilationAnalysisContext context,
            IEnumerable<MobaProjectileEmitterMapping> mappings)
        {
            foreach (var group in mappings.GroupBy(
                         MobaProjectileEmitterContract.GetAmbiguityKey,
                         StringComparer.Ordinal))
            {
                var entries = group
                    .OrderBy(mapping => mapping.QualifiedTypeName, StringComparer.Ordinal)
                    .ToArray();
                if (entries.Length <= 1) continue;

                context.ReportDiagnostic(Diagnostic.Create(
                    MobaDiagnosticRules.AmbiguousProjectileEmitterRule,
                    GetLocation(entries[1].OwnerType),
                    entries[0].EmitterValue,
                    entries[0].Priority,
                    entries[0].OwnerType.Name,
                    entries[1].OwnerType.Name));
            }
        }

        private static Location GetLocation(INamedTypeSymbol type)
        {
            return type.Locations.FirstOrDefault(location => location.IsInSource) ?? Location.None;
        }
    }
}
