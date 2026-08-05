using System;
using Microsoft.CodeAnalysis;

namespace AbilityKit.Demo.Moba.CodeGen
{
    internal static class MobaBTreeNodeContract
    {
        public const string TargetNamespace = "AbilityKit.Demo.Moba.Services.Behavior.BTree";

        public static bool IsCandidate(INamedTypeSymbol type, INamedTypeSymbol baseType)
        {
            return !type.IsAbstract &&
                   string.Equals(
                       type.ContainingNamespace?.ToDisplayString(),
                       TargetNamespace,
                       StringComparison.Ordinal) &&
                   InheritsFrom(type, baseType);
        }

        public static bool TryValidate(INamedTypeSymbol type, out string error)
        {
            if (type.IsGenericType || !IsAccessibleFromGeneratedCode(type))
            {
                error = "the type must be non-generic and accessible from generated code";
                return false;
            }

            error = null!;
            return true;
        }

        private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
        {
            for (var current = type.BaseType; current != null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, baseType)) return true;
            }

            return false;
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
}
