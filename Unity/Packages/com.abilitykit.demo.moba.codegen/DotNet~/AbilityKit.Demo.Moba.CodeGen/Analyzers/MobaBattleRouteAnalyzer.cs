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
    public sealed class MobaBattleRouteAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            MobaDiagnosticRules.InvalidInputCommandHandlerRule,
            MobaDiagnosticRules.DuplicateBattleRouteRule,
            MobaDiagnosticRules.UnsupportedBattleRouteAttributeRule,
            MobaDiagnosticRules.InvalidBattleRouteIdentityRule,
            MobaDiagnosticRules.MissingInputHandlerFallbackConstructorRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(startContext =>
            {
                var routeAttributeType = startContext.Compilation.GetTypeByMetadataName(
                    MobaBattleRouteContract.RouteAttributeMetadataName);
                var inputAttributeType = startContext.Compilation.GetTypeByMetadataName(
                    MobaBattleRouteContract.InputAttributeMetadataName);
                var inputHandlerType = startContext.Compilation.GetTypeByMetadataName(
                    MobaBattleRouteContract.InputHandlerMetadataName);
                if (routeAttributeType == null || inputAttributeType == null || inputHandlerType == null)
                {
                    return;
                }

                var routes = new ConcurrentBag<RouteDeclaration>();
                startContext.RegisterSymbolAction(
                    symbolContext => AnalyzeNamedType(
                        symbolContext,
                        routeAttributeType,
                        inputAttributeType,
                        inputHandlerType,
                        routes),
                    SymbolKind.NamedType);
                startContext.RegisterCompilationEndAction(endContext => ReportDuplicates(endContext, routes));
            });
        }

        private static void AnalyzeNamedType(
            SymbolAnalysisContext context,
            INamedTypeSymbol routeAttributeType,
            INamedTypeSymbol inputAttributeType,
            INamedTypeSymbol inputHandlerType,
            ConcurrentBag<RouteDeclaration> routes)
        {
            if (!(context.Symbol is INamedTypeSymbol type) ||
                !type.Locations.Any(location => location.IsInSource))
            {
                return;
            }

            var attributes = type.GetAttributes();
            var routeAttribute = attributes.FirstOrDefault(attribute =>
                MobaBattleRouteContract.IsOrDerivesFrom(attribute.AttributeClass, routeAttributeType));
            if (routeAttribute != null)
            {
                AnalyzeRouteAttribute(
                    context,
                    type,
                    routeAttribute,
                    routeAttributeType,
                    inputAttributeType,
                    inputHandlerType,
                    routes);
            }

            var separateInputAttribute = attributes.FirstOrDefault(attribute =>
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, inputAttributeType));
            if (separateInputAttribute != null && !ReferenceEquals(separateInputAttribute, routeAttribute))
            {
                AnalyzeInputHandler(context, type, separateInputAttribute, inputHandlerType, routes);
            }
        }

        private static void AnalyzeRouteAttribute(
            SymbolAnalysisContext context,
            INamedTypeSymbol type,
            AttributeData attribute,
            INamedTypeSymbol routeAttributeType,
            INamedTypeSymbol inputAttributeType,
            INamedTypeSymbol inputHandlerType,
            ConcurrentBag<RouteDeclaration> routes)
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, inputAttributeType))
            {
                AnalyzeInputHandler(context, type, attribute, inputHandlerType, routes);
                return;
            }

            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, routeAttributeType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MobaDiagnosticRules.UnsupportedBattleRouteAttributeRule,
                    GetLocation(type),
                    attribute.AttributeClass?.Name ?? "<unknown>",
                    type.Name));
                return;
            }

            if (!MobaBattleRouteContract.TryGetDirectRouteIdentity(attribute, out var opCode, out var kind))
            {
                return;
            }

            if (!MobaBattleRouteContract.IsValidRouteIdentity(opCode, kind))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MobaDiagnosticRules.InvalidBattleRouteIdentityRule,
                    GetLocation(type),
                    type.Name));
                return;
            }

            routes.Add(new RouteDeclaration(type, opCode, kind));
        }

        private static void AnalyzeInputHandler(
            SymbolAnalysisContext context,
            INamedTypeSymbol type,
            AttributeData attribute,
            INamedTypeSymbol inputHandlerType,
            ConcurrentBag<RouteDeclaration> routes)
        {
            if (!MobaBattleRouteContract.TryValidateInputHandler(type, inputHandlerType, out var error))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MobaDiagnosticRules.InvalidInputCommandHandlerRule,
                    GetLocation(type),
                    type.Name,
                    error));
                return;
            }

            if (!MobaBattleRouteContract.HasPublicParameterlessConstructor(type))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MobaDiagnosticRules.MissingInputHandlerFallbackConstructorRule,
                    GetLocation(type),
                    type.Name));
            }

            if (!MobaBattleRouteContract.TryGetInputRouteIdentity(attribute, out var opCode))
            {
                return;
            }

            if (!MobaBattleRouteContract.IsValidRouteIdentity(
                    opCode,
                    MobaBattleRouteContract.RuntimeInputKind))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MobaDiagnosticRules.InvalidBattleRouteIdentityRule,
                    GetLocation(type),
                    type.Name));
                return;
            }

            routes.Add(new RouteDeclaration(
                type,
                opCode,
                MobaBattleRouteContract.RuntimeInputKind));
        }

        private static void ReportDuplicates(
            CompilationAnalysisContext context,
            IEnumerable<RouteDeclaration> routes)
        {
            foreach (var group in routes.GroupBy(
                         route => MobaBattleRouteContract.GetRouteKey(route.Kind, route.OpCode),
                         StringComparer.Ordinal))
            {
                var entries = group.OrderBy(route => route.QualifiedOwnerTypeName, StringComparer.Ordinal).ToArray();
                if (entries.Length <= 1) continue;

                context.ReportDiagnostic(Diagnostic.Create(
                    MobaDiagnosticRules.DuplicateBattleRouteRule,
                    GetLocation(entries[1].OwnerType),
                    entries[0].Kind,
                    entries[0].OpCode,
                    entries[0].OwnerType.Name,
                    entries[1].OwnerType.Name));
            }
        }

        private static Location GetLocation(INamedTypeSymbol type)
        {
            return type.Locations.FirstOrDefault(location => location.IsInSource) ?? Location.None;
        }

        private sealed class RouteDeclaration
        {
            public RouteDeclaration(INamedTypeSymbol ownerType, int opCode, int kind)
            {
                OwnerType = ownerType;
                OpCode = opCode;
                Kind = kind;
                QualifiedOwnerTypeName = ownerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }

            public INamedTypeSymbol OwnerType { get; }
            public int OpCode { get; }
            public int Kind { get; }
            public string QualifiedOwnerTypeName { get; }
        }
    }
}
