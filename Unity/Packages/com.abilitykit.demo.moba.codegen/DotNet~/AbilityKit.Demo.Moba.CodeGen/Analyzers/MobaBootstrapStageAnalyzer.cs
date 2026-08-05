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
    public sealed class MobaBootstrapStageAnalyzer : DiagnosticAnalyzer
    {
        private const string AttributeMetadataName =
            "AbilityKit.Demo.Moba.Systems.Bootstrap.Flow.MobaBootstrapStageAttribute";
        private const string BaseTypeMetadataName =
            "AbilityKit.Demo.Moba.Systems.Bootstrap.Flow.MobaBootstrapStageBase";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            MobaDiagnosticRules.InvalidBootstrapStageRule,
            MobaDiagnosticRules.DuplicateBootstrapStageNameRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(startContext =>
            {
                var attributeType = startContext.Compilation.GetTypeByMetadataName(AttributeMetadataName);
                var baseType = startContext.Compilation.GetTypeByMetadataName(BaseTypeMetadataName);
                if (attributeType == null || baseType == null)
                {
                    return;
                }

                var stages = new ConcurrentBag<StageDeclaration>();
                startContext.RegisterSymbolAction(
                    symbolContext => AnalyzeNamedType(
                        symbolContext,
                        startContext.Compilation,
                        attributeType,
                        baseType,
                        stages),
                    SymbolKind.NamedType);
                startContext.RegisterCompilationEndAction(endContext => ReportDuplicates(endContext, stages));
            });
        }

        private static void AnalyzeNamedType(
            SymbolAnalysisContext context,
            Compilation compilation,
            INamedTypeSymbol attributeType,
            INamedTypeSymbol baseType,
            ConcurrentBag<StageDeclaration> stages)
        {
            if (!(context.Symbol is INamedTypeSymbol type) ||
                !type.Locations.Any(location => location.IsInSource) ||
                !type.GetAttributes().Any(attribute =>
                    SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType)))
            {
                return;
            }

            if (!MobaBootstrapStageContract.TryValidate(type, baseType, out var error))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MobaDiagnosticRules.InvalidBootstrapStageRule,
                    GetLocation(type),
                    type.Name,
                    error));
                return;
            }

            stages.Add(new StageDeclaration(
                type,
                MobaBootstrapStageContract.TryResolveStageName(type, compilation)));
        }

        private static void ReportDuplicates(
            CompilationAnalysisContext context,
            IEnumerable<StageDeclaration> stages)
        {
            foreach (var group in stages.Where(stage => stage.StageName != null)
                         .GroupBy(stage => stage.StageName!, StringComparer.Ordinal))
            {
                var entries = group.OrderBy(stage => stage.QualifiedTypeName, StringComparer.Ordinal).ToArray();
                if (entries.Length <= 1) continue;

                context.ReportDiagnostic(Diagnostic.Create(
                    MobaDiagnosticRules.DuplicateBootstrapStageNameRule,
                    GetLocation(entries[1].StageType),
                    group.Key,
                    entries[0].StageType.Name,
                    entries[1].StageType.Name));
            }
        }

        private static Location GetLocation(INamedTypeSymbol type)
        {
            return type.Locations.FirstOrDefault(location => location.IsInSource) ?? Location.None;
        }

        private sealed class StageDeclaration
        {
            public StageDeclaration(INamedTypeSymbol stageType, string? stageName)
            {
                StageType = stageType;
                StageName = stageName;
                QualifiedTypeName = stageType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }

            public INamedTypeSymbol StageType { get; }
            public string? StageName { get; }
            public string QualifiedTypeName { get; }
        }
    }
}
