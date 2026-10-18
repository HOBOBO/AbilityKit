using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AbilityKit.Demo.Moba.CodeGen
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MobaPortableSourceAnalyzer : DiagnosticAnalyzer
    {
        private static readonly string[] PortablePaths =
        {
            "/com.abilitykit.demo.moba.view.runtime/Runtime/Logic/",
            "/com.abilitykit.demo.moba.view.runtime/Runtime/Networking/"
        };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(MobaDiagnosticRules.PortableSourceEngineDependencyRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeIdentifier, SyntaxKind.IdentifierName);
        }

        private static void AnalyzeIdentifier(SyntaxNodeAnalysisContext context)
        {
            var path = context.Node.SyntaxTree.FilePath?.Replace('\\', '/');
            if (path == null || !IsPortablePath(path)) return;

            var identifier = (IdentifierNameSyntax)context.Node;
            var name = identifier.Identifier.ValueText;
            if (name != "UnityEngine" && name != "UnityEditor" && name != "Godot") return;

            var symbol = context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol;
            if (!(symbol is INamespaceSymbol namespaceSymbol) ||
                namespaceSymbol.ToDisplayString() != name) return;

            context.ReportDiagnostic(Diagnostic.Create(
                MobaDiagnosticRules.PortableSourceEngineDependencyRule,
                identifier.GetLocation(), name));
        }

        private static bool IsPortablePath(string path)
        {
            foreach (var portablePath in PortablePaths)
                if (path.IndexOf(portablePath, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }
    }
}
