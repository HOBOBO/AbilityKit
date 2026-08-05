using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AbilityKit.Demo.Moba.CodeGen
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MobaPlanActionModuleAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                MobaDiagnosticRules.InvalidPlanActionModuleRule,
                MobaDiagnosticRules.InvalidPlanActionModuleShapeRule,
                MobaDiagnosticRules.InvalidPlanActionSelfTypeRule,
                MobaDiagnosticRules.MissingPlanActionConstructorRule,
                MobaDiagnosticRules.MissingPlanActionModuleAttributeRule,
                MobaDiagnosticRules.InvalidPlanActionNameRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(StartAnalysis);
        }

        private static void StartAnalysis(CompilationStartAnalysisContext context)
        {
            var attributeType = context.Compilation.GetTypeByMetadataName(
                MobaPlanActionContract.AttributeMetadataName);
            var moduleBaseType = context.Compilation.GetTypeByMetadataName(
                MobaPlanActionContract.ModuleBaseMetadataName);
            var schemaBaseType = context.Compilation.GetTypeByMetadataName(
                MobaPlanActionContract.SchemaBaseMetadataName);
            if (attributeType == null || moduleBaseType == null || schemaBaseType == null)
            {
                return;
            }

            var actionNames = new ConcurrentBag<ActionNameInfo>();
            context.RegisterSymbolAction(
                symbolContext => AnalyzeType(
                    symbolContext,
                    attributeType,
                    moduleBaseType),
                SymbolKind.NamedType);
            context.RegisterSyntaxNodeAction(
                syntaxContext => AnalyzeActionNameProperty(syntaxContext, schemaBaseType, actionNames),
                SyntaxKind.PropertyDeclaration);

            context.RegisterCompilationEndAction(endContext => ReportDuplicateActionNames(endContext, actionNames));
        }

        private static void AnalyzeType(
            SymbolAnalysisContext context,
            INamedTypeSymbol attributeType,
            INamedTypeSymbol moduleBaseType)
        {
            var type = (INamedTypeSymbol)context.Symbol;
            if (type.TypeKind != TypeKind.Class)
            {
                return;
            }

            var attribute = type.GetAttributes().FirstOrDefault(candidate =>
                SymbolEqualityComparer.Default.Equals(candidate.AttributeClass, attributeType));
            var constructedBase = MobaPlanActionContract.FindConstructedBase(type, moduleBaseType);
            if (attribute == null && constructedBase == null)
            {
                return;
            }

            var location = GetLocation(type);
            if (constructedBase == null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MobaDiagnosticRules.InvalidPlanActionModuleRule,
                    location,
                    type.Name));
                return;
            }

            if (attribute == null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MobaDiagnosticRules.MissingPlanActionModuleAttributeRule,
                    location,
                    type.Name));
                return;
            }

            if (!MobaPlanActionContract.HasValidShape(type))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MobaDiagnosticRules.InvalidPlanActionModuleShapeRule,
                    location,
                    type.Name));
            }

            if (!MobaPlanActionContract.HasValidSelfType(type, constructedBase))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MobaDiagnosticRules.InvalidPlanActionSelfTypeRule,
                    location,
                    type.Name));
            }

            if (!MobaPlanActionContract.HasAccessibleParameterlessConstructor(type))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MobaDiagnosticRules.MissingPlanActionConstructorRule,
                    location,
                    type.Name));
            }

        }

        private static void AnalyzeActionNameProperty(
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol schemaBaseType,
            ConcurrentBag<ActionNameInfo> actionNames)
        {
            var declaration = (PropertyDeclarationSyntax)context.Node;
            if (declaration.Identifier.ValueText != "ActionName" ||
                !(context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is IPropertySymbol property) ||
                MobaPlanActionContract.FindConstructedBase(property.ContainingType, schemaBaseType) == null ||
                !TryGetPropertyExpression(property, out var expression))
            {
                return;
            }

            var constant = context.SemanticModel.GetConstantValue(expression, context.CancellationToken);
            if (!constant.HasValue || !(constant.Value is string actionName))
            {
                return;
            }

            var location = declaration.Identifier.GetLocation();
            if (string.IsNullOrWhiteSpace(actionName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MobaDiagnosticRules.InvalidPlanActionNameRule,
                    location,
                    "<empty>",
                    property.ContainingType.Name));
                return;
            }

            actionNames.Add(new ActionNameInfo(actionName, property.ContainingType.Name, location));
        }

        private static bool TryGetPropertyExpression(IPropertySymbol property, out ExpressionSyntax expression)
        {
            expression = null!;
            if (property == null)
            {
                return false;
            }

            foreach (var syntaxReference in property.DeclaringSyntaxReferences)
            {
                if (!(syntaxReference.GetSyntax() is PropertyDeclarationSyntax declaration))
                {
                    continue;
                }

                if (declaration.ExpressionBody != null)
                {
                    expression = declaration.ExpressionBody.Expression;
                    return true;
                }

                var getter = declaration.AccessorList?.Accessors
                    .FirstOrDefault(accessor => accessor.Keyword.ValueText == "get");
                if (getter?.ExpressionBody != null)
                {
                    expression = getter.ExpressionBody.Expression;
                    return true;
                }

                var returnStatement = getter?.Body?.Statements.OfType<ReturnStatementSyntax>().FirstOrDefault();
                if (returnStatement?.Expression != null)
                {
                    expression = returnStatement.Expression;
                    return true;
                }
            }

            return false;
        }

        private static void ReportDuplicateActionNames(
            CompilationAnalysisContext context,
            IEnumerable<ActionNameInfo> actionNames)
        {
            foreach (var duplicateGroup in actionNames
                         .GroupBy(item => item.ActionName, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                foreach (var item in duplicateGroup)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        MobaDiagnosticRules.InvalidPlanActionNameRule,
                        item.Location,
                        item.ActionName,
                        item.TypeName));
                }
            }
        }

        private static Location GetLocation(INamedTypeSymbol type)
        {
            return type.Locations.FirstOrDefault(candidate => candidate.IsInSource) ?? Location.None;
        }

        private sealed class ActionNameInfo
        {
            public ActionNameInfo(string actionName, string typeName, Location location)
            {
                ActionName = actionName;
                TypeName = typeName;
                Location = location;
            }

            public string ActionName { get; }
            public string TypeName { get; }
            public Location Location { get; }
        }
    }
}
