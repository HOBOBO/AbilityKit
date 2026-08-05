using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AbilityKit.Demo.Moba.CodeGen
{
    internal static class MobaBootstrapStageContract
    {
        public static bool TryValidate(
            INamedTypeSymbol type,
            INamedTypeSymbol baseType,
            out string error)
        {
            if (!InheritsFrom(type, baseType))
            {
                error = $"the type must derive from {baseType.Name}";
                return false;
            }

            if (type.IsAbstract || type.IsGenericType)
            {
                error = "the type must be concrete and non-generic";
                return false;
            }

            if (!IsAccessibleFromGeneratedCode(type))
            {
                error = "the type must be accessible from generated code";
                return false;
            }

            if (!type.InstanceConstructors.Any(constructor =>
                    constructor.Parameters.Length == 0 &&
                    (constructor.DeclaredAccessibility == Accessibility.Public ||
                     constructor.DeclaredAccessibility == Accessibility.Internal ||
                     constructor.DeclaredAccessibility == Accessibility.ProtectedOrInternal)))
            {
                error = "the type must have a parameterless constructor accessible from generated code";
                return false;
            }

            error = null!;
            return true;
        }

        public static string? TryResolveStageName(INamedTypeSymbol type, Compilation compilation)
        {
            var property = type.GetMembers("Name").OfType<IPropertySymbol>().FirstOrDefault(member =>
                member.IsOverride &&
                SymbolEqualityComparer.Default.Equals(
                    member.Type,
                    compilation.GetSpecialType(SpecialType.System_String)));
            if (property == null) return type.Name;

            foreach (var syntaxReference in property.DeclaringSyntaxReferences)
            {
                if (!(syntaxReference.GetSyntax() is PropertyDeclarationSyntax declaration)) continue;
                ExpressionSyntax? expression = declaration.ExpressionBody?.Expression;
                if (expression == null && declaration.AccessorList != null)
                {
                    expression = declaration.AccessorList.Accessors
                        .SelectMany(accessor => accessor.Body?.Statements ?? default)
                        .OfType<ReturnStatementSyntax>()
                        .Select(statement => statement.Expression)
                        .FirstOrDefault(candidate => candidate != null);
                }

                if (expression == null) continue;
                var constant = compilation.GetSemanticModel(expression.SyntaxTree).GetConstantValue(expression);
                if (constant.HasValue && constant.Value is string value) return value;
            }

            return null;
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
