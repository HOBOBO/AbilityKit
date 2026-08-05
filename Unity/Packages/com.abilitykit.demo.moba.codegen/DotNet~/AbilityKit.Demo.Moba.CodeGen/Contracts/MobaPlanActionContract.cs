using System.Linq;
using Microsoft.CodeAnalysis;

namespace AbilityKit.Demo.Moba.CodeGen
{
    internal static class MobaPlanActionContract
    {
        public const string AttributeMetadataName =
            "AbilityKit.Demo.Moba.Services.Triggering.PlanActions.PlanActionModuleAttribute";
        public const string ModuleBaseMetadataName =
            "AbilityKit.Demo.Moba.Services.Triggering.PlanActions.MobaPlanActionModuleBase`2";
        public const string SchemaBaseMetadataName =
            "AbilityKit.Demo.Moba.Services.Triggering.PlanActions.MobaPlanActionSchemaBase`1";
        public const string ManifestMetadataName =
            "AbilityKit.Demo.Moba.Services.Triggering.PlanActions.MobaGeneratedPlanActionManifest";

        public static INamedTypeSymbol? FindConstructedBase(
            INamedTypeSymbol type,
            INamedTypeSymbol baseType)
        {
            for (var current = type.BaseType; current != null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseType))
                {
                    return current;
                }
            }

            return null;
        }

        public static bool HasValidShape(INamedTypeSymbol type)
        {
            return !type.IsAbstract && !type.IsGenericType;
        }

        public static bool HasValidSelfType(
            INamedTypeSymbol type,
            INamedTypeSymbol constructedBase)
        {
            return constructedBase.TypeArguments.Length == 2 &&
                   SymbolEqualityComparer.Default.Equals(constructedBase.TypeArguments[1], type);
        }

        public static bool HasAccessibleParameterlessConstructor(INamedTypeSymbol type)
        {
            return type.InstanceConstructors.Any(constructor =>
                constructor.Parameters.Length == 0 &&
                (constructor.DeclaredAccessibility == Accessibility.Public ||
                 constructor.DeclaredAccessibility == Accessibility.Internal ||
                 constructor.DeclaredAccessibility == Accessibility.ProtectedOrInternal));
        }

        public static bool IsValidForGeneration(
            INamedTypeSymbol type,
            INamedTypeSymbol moduleBaseType)
        {
            var constructedBase = FindConstructedBase(type, moduleBaseType);
            return constructedBase != null &&
                   HasValidShape(type) &&
                   HasValidSelfType(type, constructedBase) &&
                   HasAccessibleParameterlessConstructor(type);
        }

        public static int ResolveOrder(AttributeData attribute)
        {
            return attribute.ConstructorArguments.Length > 0 &&
                   attribute.ConstructorArguments[0].Value is int value
                ? value
                : 0;
        }
    }
}
