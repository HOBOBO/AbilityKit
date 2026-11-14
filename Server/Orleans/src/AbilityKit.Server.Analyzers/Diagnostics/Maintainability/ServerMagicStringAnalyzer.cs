using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AbilityKit.Server.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ServerMagicStringAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> HardcodedGrainKeys = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "global",
        "default");

    private static readonly ImmutableHashSet<string> HardcodedServerStrings = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "moba",
        "shooter",
        "tickRate",
        "mapId",
        "sandbox",
        "joinMode",
        "running-battle-late-join",
        "pure-state-authority",
        "state-sync-authority",
        "frame-sync-authority",
        "moba-state-sync-authority",
        "runtime-snapshot-interpolation",
        "predict-rollback-authority");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        ServerAnalyzerDescriptors.AvoidHardcodedGrainKey,
        ServerAnalyzerDescriptors.AvoidServerMagicString);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeStringLiteral, SyntaxKind.StringLiteralExpression);
    }

    private static void AnalyzeStringLiteral(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;
        if (literal.Token.ValueText is not { Length: > 0 } value)
        {
            return;
        }

        if (!ServerSourceFileFilter.IsProductionServerSourceFile(literal.SyntaxTree.FilePath))
        {
            return;
        }

        if (HardcodedGrainKeys.Contains(value) && IsGetGrainArgument(literal))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ServerAnalyzerDescriptors.AvoidHardcodedGrainKey,
                literal.GetLocation(),
                value));
            return;
        }

        if (HardcodedServerStrings.Contains(value) && IsProductionServerStringContext(literal))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ServerAnalyzerDescriptors.AvoidServerMagicString,
                literal.GetLocation(),
                value));
        }
    }

    private static bool IsGetGrainArgument(LiteralExpressionSyntax literal)
    {
        var argument = literal.FirstAncestorOrSelf<ArgumentSyntax>();
        var invocation = argument?.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        return invocation?.Expression is MemberAccessExpressionSyntax memberAccess
            && memberAccess.Name.Identifier.ValueText == "GetGrain";
    }

    private static bool IsProductionServerStringContext(LiteralExpressionSyntax literal)
    {
        return IsGetGrainArgument(literal)
            || literal.FirstAncestorOrSelf<InitializerExpressionSyntax>() is not null
            || literal.FirstAncestorOrSelf<EqualsValueClauseSyntax>() is not null
            || literal.FirstAncestorOrSelf<ArgumentSyntax>() is not null;
    }

}
