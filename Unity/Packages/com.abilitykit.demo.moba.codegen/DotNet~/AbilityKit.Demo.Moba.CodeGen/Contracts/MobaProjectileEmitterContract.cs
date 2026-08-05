using System.Linq;
using Microsoft.CodeAnalysis;

namespace AbilityKit.Demo.Moba.CodeGen
{
    internal static class MobaProjectileEmitterContract
    {
        public const string AttributeMetadataName =
            "AbilityKit.Demo.Moba.Services.Projectile.Launch.MobaProjectileEmitterAttribute";
        public const string InterfaceMetadataName =
            "AbilityKit.Demo.Moba.Services.Projectile.Launch.IMobaProjectileLaunchSequence";
        public const string EmitterTypeMetadataName = "AbilityKit.Demo.Moba.ProjectileEmitterType";

        public static bool TryCreateMapping(
            INamedTypeSymbol ownerType,
            INamedTypeSymbol interfaceType,
            AttributeData attribute,
            out MobaProjectileEmitterMapping mapping,
            out string error)
        {
            mapping = null!;
            if (!TryValidateType(ownerType, interfaceType, out error))
            {
                return false;
            }

            if (attribute.ConstructorArguments.Length != 1 ||
                !(attribute.ConstructorArguments[0].Value is int emitterValue))
            {
                error = "the emitter type must be a compile-time enum value";
                return false;
            }

            var priority = 0;
            var isDefault = false;
            foreach (var argument in attribute.NamedArguments)
            {
                if (argument.Key == "Priority" && argument.Value.Value is int value) priority = value;
                if (argument.Key == "IsDefault" && argument.Value.Value is bool flag) isDefault = flag;
            }

            mapping = new MobaProjectileEmitterMapping(ownerType, emitterValue, priority, isDefault);
            error = null!;
            return true;
        }

        public static string GetAmbiguityKey(MobaProjectileEmitterMapping mapping)
        {
            return mapping.EmitterValue + ":" + mapping.Priority;
        }

        private static bool TryValidateType(
            INamedTypeSymbol type,
            INamedTypeSymbol interfaceType,
            out string error)
        {
            if (type.IsAbstract || type.IsGenericType)
            {
                error = "the type must be concrete and non-generic";
                return false;
            }

            if (!type.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, interfaceType)))
            {
                error = $"the type must implement {interfaceType.Name}";
                return false;
            }

            if (!IsAccessibleFromGeneratedCode(type))
            {
                error = "the type must be accessible from generated code";
                return false;
            }

            if (!type.InstanceConstructors.Any(IsAccessibleParameterlessConstructor))
            {
                error = "the type must have a parameterless constructor accessible from generated code";
                return false;
            }

            error = null!;
            return true;
        }

        private static bool IsAccessibleParameterlessConstructor(IMethodSymbol constructor)
        {
            return constructor.Parameters.Length == 0 &&
                   (constructor.DeclaredAccessibility == Accessibility.Public ||
                    constructor.DeclaredAccessibility == Accessibility.Internal ||
                    constructor.DeclaredAccessibility == Accessibility.ProtectedOrInternal);
        }

        private static bool IsAccessibleFromGeneratedCode(INamedTypeSymbol type)
        {
            for (var current = type; current != null; current = current.ContainingType)
            {
                if (current.DeclaredAccessibility != Accessibility.Public &&
                    current.DeclaredAccessibility != Accessibility.Internal &&
                    current.DeclaredAccessibility != Accessibility.ProtectedOrInternal)
                {
                    return false;
                }
            }

            return true;
        }
    }

    internal sealed class MobaProjectileEmitterMapping
    {
        public MobaProjectileEmitterMapping(
            INamedTypeSymbol ownerType,
            int emitterValue,
            int priority,
            bool isDefault)
        {
            OwnerType = ownerType;
            EmitterValue = emitterValue;
            Priority = priority;
            IsDefault = isDefault;
            QualifiedTypeName = ownerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        public INamedTypeSymbol OwnerType { get; }
        public int EmitterValue { get; }
        public int Priority { get; }
        public bool IsDefault { get; }
        public string QualifiedTypeName { get; }
    }
}
