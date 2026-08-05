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
    public sealed class MobaEventMappingAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            MobaDiagnosticRules.InvalidEventMappingRule,
            MobaDiagnosticRules.DuplicateEventMappingRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(startContext =>
            {
                var attributeType = startContext.Compilation.GetTypeByMetadataName(
                    MobaEventMappingContract.AttributeMetadataName);
                if (attributeType == null)
                {
                    return;
                }

                var mappings = new ConcurrentBag<MobaEventMapping>();
                startContext.RegisterSymbolAction(
                    symbolContext => AnalyzeNamedType(symbolContext, attributeType, mappings),
                    SymbolKind.NamedType);
                startContext.RegisterCompilationEndAction(endContext => ReportDuplicates(endContext, mappings));
            });
        }

        private static void AnalyzeNamedType(
            SymbolAnalysisContext context,
            INamedTypeSymbol attributeType,
            ConcurrentBag<MobaEventMapping> mappings)
        {
            if (!(context.Symbol is INamedTypeSymbol type) ||
                !type.Locations.Any(location => location.IsInSource))
            {
                return;
            }

            foreach (var attribute in type.GetAttributes().Where(candidate =>
                         SymbolEqualityComparer.Default.Equals(candidate.AttributeClass, attributeType)))
            {
                if (!MobaEventMappingContract.TryCreateMapping(type, attribute, out var mapping, out var error))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        MobaDiagnosticRules.InvalidEventMappingRule,
                        GetLocation(type),
                        type.Name,
                        error));
                    continue;
                }

                mappings.Add(mapping);
            }
        }

        private static void ReportDuplicates(
            CompilationAnalysisContext context,
            IEnumerable<MobaEventMapping> mappings)
        {
            foreach (var group in mappings.GroupBy(
                         MobaEventMappingContract.GetDuplicateKey,
                         StringComparer.Ordinal))
            {
                var entries = group
                    .OrderBy(mapping => mapping.QualifiedOwnerTypeName, StringComparer.Ordinal)
                    .ThenBy(mapping => mapping.ArgsType.ToDisplayString(), StringComparer.Ordinal)
                    .ToArray();
                if (entries.Length <= 1) continue;

                context.ReportDiagnostic(Diagnostic.Create(
                    MobaDiagnosticRules.DuplicateEventMappingRule,
                    GetLocation(entries[1].OwnerType),
                    entries[0].EventId));
            }
        }

        private static Location GetLocation(INamedTypeSymbol type)
        {
            return type.Locations.FirstOrDefault(location => location.IsInSource) ?? Location.None;
        }
    }
}
