using System.Linq;
using Microsoft.CodeAnalysis;

namespace AbilityKit.Demo.Moba.CodeGen
{
    internal static class MobaBattleRouteContract
    {
        public const string RouteAttributeMetadataName =
            "AbilityKit.Demo.Moba.Services.MobaBattleRouteAttribute";
        public const string InputAttributeMetadataName =
            "AbilityKit.Demo.Moba.Services.MobaInputCommandHandlerAttribute";
        public const string InputHandlerMetadataName =
            "AbilityKit.Demo.Moba.Services.IMobaInputCommandHandler";
        public const string RouteManifestMetadataName =
            "AbilityKit.Demo.Moba.Services.MobaGeneratedBattleRouteManifest";
        public const string InputManifestMetadataName =
            "AbilityKit.Demo.Moba.Services.MobaGeneratedInputCommandHandlerManifest";
        public const int RuntimeInputKind = 1;

        public static bool IsOrDerivesFrom(INamedTypeSymbol? candidate, INamedTypeSymbol baseType)
        {
            for (var current = candidate; current != null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, baseType)) return true;
            }

            return false;
        }

        public static bool TryValidateInputHandler(
            INamedTypeSymbol type,
            INamedTypeSymbol inputHandlerType,
            out string error)
        {
            if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.IsGenericType ||
                !type.AllInterfaces.Any(candidate =>
                    SymbolEqualityComparer.Default.Equals(candidate, inputHandlerType)) ||
                !GeneratedCodeSymbolRules.CanReferenceType(type))
            {
                error = $"the type must be a concrete non-generic class accessible from generated code and implementing {inputHandlerType.Name}";
                return false;
            }

            error = null!;
            return true;
        }

        public static bool HasPublicParameterlessConstructor(INamedTypeSymbol type)
        {
            return type.InstanceConstructors.Any(constructor =>
                constructor.Parameters.Length == 0 &&
                constructor.DeclaredAccessibility == Accessibility.Public);
        }

        public static bool TryGetDirectRouteIdentity(
            AttributeData attribute,
            out int opCode,
            out int kind)
        {
            opCode = default;
            kind = default;
            if (attribute.ConstructorArguments.Length != 2 ||
                !(attribute.ConstructorArguments[0].Value is int resolvedOpCode) ||
                !(attribute.ConstructorArguments[1].Value is int resolvedKind))
            {
                return false;
            }

            opCode = resolvedOpCode;
            kind = resolvedKind;
            return true;
        }

        public static bool TryGetInputRouteIdentity(AttributeData attribute, out int opCode)
        {
            opCode = default;
            if (attribute.ConstructorArguments.Length != 1 ||
                !(attribute.ConstructorArguments[0].Value is int resolvedOpCode))
            {
                return false;
            }

            opCode = resolvedOpCode;
            return true;
        }

        public static bool IsValidRouteIdentity(int opCode, int kind)
        {
            return opCode != 0 && kind != 0;
        }

        public static ITypeSymbol? GetNamedType(AttributeData attribute, string name)
        {
            foreach (var argument in attribute.NamedArguments)
            {
                if (argument.Key == name) return argument.Value.Value as ITypeSymbol;
            }

            return null;
        }

        public static string? GetNamedString(AttributeData attribute, string name)
        {
            foreach (var argument in attribute.NamedArguments)
            {
                if (argument.Key == name) return argument.Value.Value as string;
            }

            return null;
        }

        public static string GetRouteKey(int kind, int opCode)
        {
            return kind + ":" + opCode;
        }

        public static bool TryValidateGeneratedRouteTypes(
            INamedTypeSymbol ownerType,
            ITypeSymbol? payloadType,
            ITypeSymbol? handlerType,
            out string error)
        {
            if (!GeneratedCodeSymbolRules.CanReferenceType(ownerType))
            {
                error = $"owner type '{ownerType.Name}' must be accessible from generated code";
                return false;
            }

            if (payloadType != null && !GeneratedCodeSymbolRules.CanReferenceType(payloadType))
            {
                error = $"payload type '{payloadType.Name}' must be a closed type accessible from generated code";
                return false;
            }

            if (handlerType != null && !GeneratedCodeSymbolRules.CanReferenceType(handlerType))
            {
                error = $"handler type '{handlerType.Name}' must be a closed type accessible from generated code";
                return false;
            }

            error = null!;
            return true;
        }
    }
}
