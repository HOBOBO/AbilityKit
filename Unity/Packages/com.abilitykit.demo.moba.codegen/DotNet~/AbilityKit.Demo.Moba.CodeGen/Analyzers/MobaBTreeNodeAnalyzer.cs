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
    public sealed class MobaBTreeNodeAnalyzer : DiagnosticAnalyzer
    {
        private const string NodeBaseMetadataName = "BTCore.Runtime.BTNode";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            MobaDiagnosticRules.InvalidBTreeNodeRule,
            MobaDiagnosticRules.DuplicateBTreeNodeNameRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(startContext =>
            {
                var nodeBaseType = startContext.Compilation.GetTypeByMetadataName(NodeBaseMetadataName);
                if (nodeBaseType == null)
                {
                    return;
                }

                var nodes = new ConcurrentBag<NodeDeclaration>();
                startContext.RegisterSymbolAction(
                    symbolContext => AnalyzeNamedType(symbolContext, nodeBaseType, nodes),
                    SymbolKind.NamedType);
                startContext.RegisterCompilationEndAction(endContext => ReportDuplicates(endContext, nodes));
            });
        }

        private static void AnalyzeNamedType(
            SymbolAnalysisContext context,
            INamedTypeSymbol nodeBaseType,
            ConcurrentBag<NodeDeclaration> nodes)
        {
            if (!(context.Symbol is INamedTypeSymbol type) ||
                !type.Locations.Any(location => location.IsInSource) ||
                !MobaBTreeNodeContract.IsCandidate(type, nodeBaseType))
            {
                return;
            }

            if (!MobaBTreeNodeContract.TryValidate(type, out var error))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MobaDiagnosticRules.InvalidBTreeNodeRule,
                    GetLocation(type),
                    type.Name,
                    error));
                return;
            }

            nodes.Add(new NodeDeclaration(type));
        }

        private static void ReportDuplicates(
            CompilationAnalysisContext context,
            IEnumerable<NodeDeclaration> nodes)
        {
            foreach (var group in nodes.GroupBy(node => node.NodeName, StringComparer.Ordinal))
            {
                var entries = group.OrderBy(node => node.QualifiedTypeName, StringComparer.Ordinal).ToArray();
                if (entries.Length <= 1) continue;

                context.ReportDiagnostic(Diagnostic.Create(
                    MobaDiagnosticRules.DuplicateBTreeNodeNameRule,
                    GetLocation(entries[1].NodeType),
                    group.Key,
                    entries[0].QualifiedTypeName,
                    entries[1].QualifiedTypeName));
            }
        }

        private static Location GetLocation(INamedTypeSymbol type)
        {
            return type.Locations.FirstOrDefault(location => location.IsInSource) ?? Location.None;
        }

        private sealed class NodeDeclaration
        {
            public NodeDeclaration(INamedTypeSymbol nodeType)
            {
                NodeType = nodeType;
                NodeName = nodeType.Name;
                QualifiedTypeName = nodeType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }

            public INamedTypeSymbol NodeType { get; }
            public string NodeName { get; }
            public string QualifiedTypeName { get; }
        }
    }
}
