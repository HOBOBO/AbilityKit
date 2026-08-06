using Microsoft.CodeAnalysis;

namespace AbilityKit.Demo.Moba.CodeGen
{
    internal static class GeneratedCodeSymbolRules
    {
        public static bool CanReferenceType(ITypeSymbol? type)
        {
            if (type == null || type.TypeKind == TypeKind.Error || type.TypeKind == TypeKind.TypeParameter)
            {
                return false;
            }

            if (type is IArrayTypeSymbol arrayType)
            {
                return CanReferenceType(arrayType.ElementType);
            }

            if (type is IPointerTypeSymbol pointerType)
            {
                return CanReferenceType(pointerType.PointedAtType);
            }

            if (!(type is INamedTypeSymbol namedType) || namedType.IsUnboundGenericType)
            {
                return false;
            }

            for (var current = namedType; current != null; current = current.ContainingType)
            {
                if (!IsAssemblyAccessible(current.DeclaredAccessibility))
                {
                    return false;
                }
            }

            foreach (var typeArgument in namedType.TypeArguments)
            {
                if (!CanReferenceType(typeArgument))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAssemblyAccessible(Accessibility accessibility)
        {
            return accessibility == Accessibility.Public ||
                   accessibility == Accessibility.Internal ||
                   accessibility == Accessibility.ProtectedOrInternal;
        }
    }
}
