using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace AbilityKit.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ConfigTableDefinitionAnalyzer : DiagnosticAnalyzer
    {
        private const string DefinitionMetadataName =
            "AbilityKit.Ability.Config.ConfigTableDefinition";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DiagnosticRules.PartialConfigTableFactoryRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(compilationContext =>
            {
                var definitionType = compilationContext.Compilation.GetTypeByMetadataName(
                    DefinitionMetadataName);
                if (definitionType == null) return;

                compilationContext.RegisterOperationAction(
                    operationContext => AnalyzeObjectCreation(
                        operationContext,
                        definitionType),
                    OperationKind.ObjectCreation);
            });
        }

        private static void AnalyzeObjectCreation(
            OperationAnalysisContext context,
            INamedTypeSymbol definitionType)
        {
            var creation = (IObjectCreationOperation)context.Operation;
            if (!SymbolEqualityComparer.Default.Equals(
                    creation.Constructor?.ContainingType,
                    definitionType))
            {
                return;
            }

            IArgumentOperation dtoFactoryArgument = null;
            IArgumentOperation entryFactoryArgument = null;
            foreach (var argument in creation.Arguments)
            {
                switch (argument.Parameter?.Name)
                {
                    case "dtoTableFactory":
                        dtoFactoryArgument = argument;
                        break;
                    case "entryTableFactory":
                        entryFactoryArgument = argument;
                        break;
                }
            }

            if (dtoFactoryArgument == null || entryFactoryArgument == null) return;

            var dtoFactoryIsNull = IsNullConstant(dtoFactoryArgument.Value);
            var entryFactoryIsNull = IsNullConstant(entryFactoryArgument.Value);
            if (dtoFactoryIsNull == entryFactoryIsNull) return;

            var invalidArgument = dtoFactoryIsNull
                ? dtoFactoryArgument
                : entryFactoryArgument;
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticRules.PartialConfigTableFactoryRule,
                invalidArgument.Syntax.GetLocation()));
        }

        private static bool IsNullConstant(IOperation operation)
        {
            return operation.ConstantValue.HasValue && operation.ConstantValue.Value == null;
        }
    }
}
