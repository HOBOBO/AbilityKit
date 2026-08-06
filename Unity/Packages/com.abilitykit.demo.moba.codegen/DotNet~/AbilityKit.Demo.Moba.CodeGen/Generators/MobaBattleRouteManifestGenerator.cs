using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AbilityKit.Demo.Moba.CodeGen
{
    [Generator]
    public sealed class MobaBattleRouteManifestGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new BattleRouteSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxContextReceiver is BattleRouteSyntaxReceiver receiver)) return;

            var routeAttributeType = context.Compilation.GetTypeByMetadataName(
                MobaBattleRouteContract.RouteAttributeMetadataName);
            var inputAttributeType = context.Compilation.GetTypeByMetadataName(
                MobaBattleRouteContract.InputAttributeMetadataName);
            var inputHandlerType = context.Compilation.GetTypeByMetadataName(
                MobaBattleRouteContract.InputHandlerMetadataName);
            var routeManifestType = context.Compilation.GetTypeByMetadataName(
                MobaBattleRouteContract.RouteManifestMetadataName);
            var inputManifestType = context.Compilation.GetTypeByMetadataName(
                MobaBattleRouteContract.InputManifestMetadataName);
            if (routeAttributeType == null || inputAttributeType == null || inputHandlerType == null ||
                routeManifestType == null || inputManifestType == null ||
                !routeManifestType.Locations.Any(location => location.IsInSource) ||
                !inputManifestType.Locations.Any(location => location.IsInSource))
            {
                return;
            }

            var routes = new List<RouteMapping>();
            var inputs = new List<InputMapping>();
            var seenTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var type in receiver.Types)
            {
                if (!seenTypes.Add(type)) continue;

                var attributes = type.GetAttributes().Where(attribute =>
                    MobaBattleRouteContract.IsOrDerivesFrom(attribute.AttributeClass, routeAttributeType));
                foreach (var routeAttribute in attributes)
                {
                    if (SymbolEqualityComparer.Default.Equals(routeAttribute.AttributeClass, routeAttributeType))
                    {
                        if (TryCreateDirectRoute(type, routeAttribute, out var route, out var error))
                        {
                            routes.Add(route);
                        }
                    }
                    else if (SymbolEqualityComparer.Default.Equals(routeAttribute.AttributeClass, inputAttributeType))
                    {
                        if (TryCreateInputRoute(type, routeAttribute, inputHandlerType, out var route, out var input, out var error))
                        {
                            routes.Add(route);
                            inputs.Add(input);
                        }
                    }
                }
            }

            RemoveDuplicateRoutes(routes);
            RemoveDuplicateInputs(inputs);
            routes.Sort(CompareRoutes);
            inputs.Sort(CompareInputs);
            context.AddSource("MobaGeneratedBattleRouteManifest.g.cs", GenerateSource(routes, inputs));
        }

        private static bool TryCreateDirectRoute(
            INamedTypeSymbol ownerType,
            AttributeData attribute,
            out RouteMapping route,
            out string error)
        {
            route = null!;
            error = null!;
            if (!MobaBattleRouteContract.TryGetDirectRouteIdentity(attribute, out var opCode, out var kind))
            {
                error = "opCode and route kind must be compile-time values";
                return false;
            }

            if (!MobaBattleRouteContract.IsValidRouteIdentity(opCode, kind))
            {
                error = "opCode must be non-zero and route kind must not be Unknown";
                return false;
            }

            var payloadType = MobaBattleRouteContract.GetNamedType(attribute, "PayloadType");
            var handlerType = MobaBattleRouteContract.GetNamedType(attribute, "HandlerType");
            if (!MobaBattleRouteContract.TryValidateGeneratedRouteTypes(
                    ownerType,
                    payloadType,
                    handlerType,
                    out error))
            {
                return false;
            }

            route = new RouteMapping(
                ownerType,
                opCode,
                kind,
                payloadType,
                handlerType,
                MobaBattleRouteContract.GetNamedString(attribute, "Name"));
            return true;
        }

        private static bool TryCreateInputRoute(
            INamedTypeSymbol ownerType,
            AttributeData attribute,
            INamedTypeSymbol inputHandlerType,
            out RouteMapping route,
            out InputMapping input,
            out string error)
        {
            route = null!;
            input = null!;
            error = null!;
            if (!MobaBattleRouteContract.TryValidateInputHandler(ownerType, inputHandlerType, out error))
            {
                return false;
            }

            if (!MobaBattleRouteContract.TryGetInputRouteIdentity(attribute, out var opCode))
            {
                error = "opCode must be a compile-time int value";
                return false;
            }

            if (!MobaBattleRouteContract.IsValidRouteIdentity(
                    opCode,
                    MobaBattleRouteContract.RuntimeInputKind))
            {
                error = "opCode must be non-zero";
                return false;
            }

            var payloadType = MobaBattleRouteContract.GetNamedType(attribute, "PayloadType");
            if (!MobaBattleRouteContract.TryValidateGeneratedRouteTypes(
                    ownerType,
                    payloadType,
                    ownerType,
                    out error))
            {
                return false;
            }

            route = new RouteMapping(
                ownerType,
                opCode,
                kind: MobaBattleRouteContract.RuntimeInputKind,
                payloadType,
                ownerType,
                MobaBattleRouteContract.GetNamedString(attribute, "Name"));
            input = new InputMapping(ownerType, opCode);
            return true;
        }

        private static void RemoveDuplicateRoutes(List<RouteMapping> routes)
        {
            var duplicateKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var group in routes.GroupBy(
                         route => MobaBattleRouteContract.GetRouteKey(route.Kind, route.OpCode),
                         StringComparer.Ordinal))
            {
                var entries = group.OrderBy(route => route.QualifiedOwnerTypeName, StringComparer.Ordinal).ToArray();
                if (entries.Length <= 1) continue;
                duplicateKeys.Add(MobaBattleRouteContract.GetRouteKey(entries[0].Kind, entries[0].OpCode));
            }

            routes.RemoveAll(route => duplicateKeys.Contains(
                MobaBattleRouteContract.GetRouteKey(route.Kind, route.OpCode)));
        }

        private static void RemoveDuplicateInputs(List<InputMapping> inputs)
        {
            var duplicateOpCodes = new HashSet<int>();
            foreach (var group in inputs.GroupBy(input => input.OpCode))
            {
                var entries = group.OrderBy(input => input.QualifiedHandlerTypeName, StringComparer.Ordinal).ToArray();
                if (entries.Length <= 1) continue;
                duplicateOpCodes.Add(entries[0].OpCode);
            }

            inputs.RemoveAll(input => duplicateOpCodes.Contains(input.OpCode));
        }

        private static int CompareRoutes(RouteMapping left, RouteMapping right)
        {
            var kind = left.Kind.CompareTo(right.Kind);
            if (kind != 0) return kind;
            var opCode = left.OpCode.CompareTo(right.OpCode);
            return opCode != 0
                ? opCode
                : string.CompareOrdinal(left.QualifiedOwnerTypeName, right.QualifiedOwnerTypeName);
        }

        private static int CompareInputs(InputMapping left, InputMapping right)
        {
            var opCode = left.OpCode.CompareTo(right.OpCode);
            return opCode != 0
                ? opCode
                : string.CompareOrdinal(left.QualifiedHandlerTypeName, right.QualifiedHandlerTypeName);
        }

        private static string GenerateSource(
            IReadOnlyList<RouteMapping> routes,
            IReadOnlyList<InputMapping> inputs)
        {
            var source = new StringBuilder();
            source.AppendLine("// <auto-generated/>");
            source.AppendLine("namespace AbilityKit.Demo.Moba.Services");
            source.AppendLine("{");
            source.AppendLine("    internal static partial class MobaGeneratedBattleRouteManifest");
            source.AppendLine("    {");
            source.AppendLine("        static partial void AddGenerated(MobaBattleRouteRegistry registry, ref int count)");
            source.AppendLine("        {");
            foreach (var route in routes)
            {
                source.Append("            if (registry.Register(new MobaBattleRouteDescriptor(")
                    .Append(route.OpCode).Append(", (MobaBattleRouteKind)").Append(route.Kind)
                    .Append(", typeof(").Append(route.QualifiedOwnerTypeName).Append("), ")
                    .Append(FormatType(route.PayloadType)).Append(", ")
                    .Append(FormatType(route.HandlerType)).Append(", ")
                    .Append(FormatString(route.Name)).AppendLine("))) count++;");
            }

            source.AppendLine("        }");
            source.AppendLine("    }");
            source.AppendLine();
            source.AppendLine("    internal static partial class MobaGeneratedInputCommandHandlerManifest");
            source.AppendLine("    {");
            source.AppendLine("        static partial void AddGenerated(MobaInputCommandHandlerRegistry registry, ref int count)");
            source.AppendLine("        {");
            foreach (var input in inputs)
            {
                source.Append("            if (registry.TryRegisterGenerated(").Append(input.OpCode)
                    .Append(", typeof(").Append(input.QualifiedHandlerTypeName).AppendLine("))) count++;");
            }

            source.AppendLine("        }");
            source.AppendLine("    }");
            source.AppendLine("}");
            return source.ToString();
        }

        private static string FormatType(ITypeSymbol? type)
        {
            return type == null
                ? "null"
                : "typeof(" + type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")";
        }

        private static string FormatString(string? value)
        {
            return value == null ? "null" : SymbolDisplay.FormatLiteral(value, true);
        }

        private sealed class RouteMapping
        {
            public RouteMapping(
                INamedTypeSymbol ownerType,
                int opCode,
                int kind,
                ITypeSymbol? payloadType,
                ITypeSymbol? handlerType,
                string? name)
            {
                OwnerType = ownerType;
                OpCode = opCode;
                Kind = kind;
                PayloadType = payloadType;
                HandlerType = handlerType;
                Name = name;
                QualifiedOwnerTypeName = ownerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }

            public INamedTypeSymbol OwnerType { get; }
            public int OpCode { get; }
            public int Kind { get; }
            public ITypeSymbol? PayloadType { get; }
            public ITypeSymbol? HandlerType { get; }
            public string? Name { get; }
            public string QualifiedOwnerTypeName { get; }
        }

        private sealed class InputMapping
        {
            public InputMapping(INamedTypeSymbol handlerType, int opCode)
            {
                HandlerType = handlerType;
                OpCode = opCode;
                QualifiedHandlerTypeName = handlerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }

            public INamedTypeSymbol HandlerType { get; }
            public int OpCode { get; }
            public string QualifiedHandlerTypeName { get; }
        }
    }

    internal sealed class BattleRouteSyntaxReceiver : ISyntaxContextReceiver
    {
        public List<INamedTypeSymbol> Types { get; } = new List<INamedTypeSymbol>();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            if (context.Node is TypeDeclarationSyntax declaration && declaration.AttributeLists.Count > 0 &&
                context.SemanticModel.GetDeclaredSymbol(declaration) is INamedTypeSymbol type)
            {
                Types.Add(type);
            }
        }
    }
}
