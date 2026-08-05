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
    public sealed class MobaTargetQueryFactoryAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            MobaDiagnosticRules.InvalidTargetQueryFactoryRule,
            MobaDiagnosticRules.DuplicateTargetQueryFactoryCodeRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(startContext =>
            {
                var definitions = MobaTargetQueryFactoryContract.ResolveDefinitions(startContext.Compilation);
                if (definitions.Count == 0)
                {
                    return;
                }

                var mappings = new ConcurrentBag<MobaTargetQueryFactoryMapping>();
                startContext.RegisterSymbolAction(
                    symbolContext => AnalyzeNamedType(symbolContext, definitions, mappings),
                    SymbolKind.NamedType);
                startContext.RegisterCompilationEndAction(endContext => ReportDuplicates(endContext, mappings));
            });
        }

        private static void AnalyzeNamedType(
            SymbolAnalysisContext context,
            IReadOnlyList<MobaResolvedTargetQueryFactoryDefinition> definitions,
            ConcurrentBag<MobaTargetQueryFactoryMapping> mappings)
        {
            if (!(context.Symbol is INamedTypeSymbol type) ||
                !type.Locations.Any(location => location.IsInSource))
            {
                return;
            }

            foreach (var definition in definitions)
            {
                foreach (var attribute in type.GetAttributes().Where(candidate =>
                             SymbolEqualityComparer.Default.Equals(candidate.AttributeClass, definition.AttributeType)))
                {
                    if (!MobaTargetQueryFactoryContract.TryCreateMapping(
                            type,
                            definition,
                            attribute,
                            out var mapping,
                            out var error))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            MobaDiagnosticRules.InvalidTargetQueryFactoryRule,
                            GetLocation(type),
                            type.Name,
                            error));
                        continue;
                    }

                    mappings.Add(mapping);
                }
            }
        }

        private static void ReportDuplicates(
            CompilationAnalysisContext context,
            IEnumerable<MobaTargetQueryFactoryMapping> mappings)
        {
            foreach (var group in mappings.GroupBy(
                         MobaTargetQueryFactoryContract.GetDuplicateKey,
                         StringComparer.Ordinal))
            {
                var entries = group
                    .OrderBy(mapping => mapping.QualifiedTypeName, StringComparer.Ordinal)
                    .ToArray();
                if (entries.Length <= 1) continue;

                context.ReportDiagnostic(Diagnostic.Create(
                    MobaDiagnosticRules.DuplicateTargetQueryFactoryCodeRule,
                    GetLocation(entries[1].FactoryType),
                    entries[0].Definition.Kind,
                    entries[0].Code,
                    entries[0].FactoryType.Name,
                    entries[1].FactoryType.Name));
            }
        }

        private static Location GetLocation(INamedTypeSymbol type)
        {
            return type.Locations.FirstOrDefault(location => location.IsInSource) ?? Location.None;
        }
    }
}
