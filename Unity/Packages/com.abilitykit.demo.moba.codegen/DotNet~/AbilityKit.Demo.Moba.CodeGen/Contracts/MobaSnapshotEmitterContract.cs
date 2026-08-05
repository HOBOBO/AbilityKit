using System.Linq;
using Microsoft.CodeAnalysis;

namespace AbilityKit.Demo.Moba.CodeGen
{
    internal static class MobaSnapshotEmitterContract
    {
        public const string AttributeMetadataName = "AbilityKit.Demo.Moba.Services.MobaSnapshotEmitterAttribute";
        public const string InterfaceMetadataName = "AbilityKit.Demo.Moba.Services.IMobaSnapshotEmitter";
        public const string ManifestMetadataName =
            "AbilityKit.Demo.Moba.Services.MobaGeneratedSnapshotEmitterManifest";

        public static bool TryCreateMapping(
            INamedTypeSymbol emitterType,
            INamedTypeSymbol interfaceType,
            AttributeData attribute,
            out MobaSnapshotEmitterMapping mapping,
            out string error)
        {
            mapping = null!;
            if (emitterType.IsAbstract || emitterType.IsGenericType ||
                !emitterType.AllInterfaces.Any(candidate =>
                    SymbolEqualityComparer.Default.Equals(candidate, interfaceType)) ||
                !IsAccessibleFromGeneratedCode(emitterType))
            {
                error = $"the type must be concrete, non-generic, accessible, and implement {interfaceType.Name}";
                return false;
            }

            if (attribute.ConstructorArguments.Length != 1 ||
                !(attribute.ConstructorArguments[0].Value is int priority))
            {
                error = "priority must be a compile-time int value";
                return false;
            }

            mapping = new MobaSnapshotEmitterMapping(emitterType, priority);
            error = null!;
            return true;
        }

        public static bool IsSourceManifest(INamedTypeSymbol? manifestType)
        {
            return manifestType != null && manifestType.Locations.Any(location => location.IsInSource);
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

    internal sealed class MobaSnapshotEmitterMapping
    {
        public MobaSnapshotEmitterMapping(INamedTypeSymbol emitterType, int priority)
        {
            EmitterType = emitterType;
            Priority = priority;
            QualifiedTypeName = emitterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        public INamedTypeSymbol EmitterType { get; }
        public int Priority { get; }
        public string QualifiedTypeName { get; }
    }
}
