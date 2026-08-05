using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AbilityKit.Demo.Moba.CodeGen
{
    internal static class MobaPayloadFieldIdsContract
    {
        public const string AttributeMetadataName = "AbilityKit.Demo.Moba.GeneratePayloadFieldIdsAttribute";

        public static MobaPayloadFieldIdsValidation Validate(
            Compilation compilation,
            INamedTypeSymbol accessorType,
            IReadOnlyList<AttributeData> attributes)
        {
            var errors = new List<string>();
            var groups = new List<MobaPayloadFieldGroup>();
            if (accessorType.ContainingType != null || accessorType.IsGenericType || !IsClassDeclaration(accessorType))
            {
                errors.Add("the accessor must be a non-generic top-level class");
                return new MobaPayloadFieldIdsValidation(groups, errors);
            }

            if (!IsPartial(accessorType))
            {
                errors.Add("the accessor class must be partial");
                return new MobaPayloadFieldIdsValidation(groups, errors);
            }

            foreach (var attribute in attributes)
            {
                if (TryCreateGroup(compilation, accessorType, attribute, out var group, out var error))
                {
                    groups.Add(group);
                }
                else
                {
                    errors.Add(error);
                }
            }

            if (errors.Count == 0)
            {
                ValidateGeneratedMembers(accessorType, groups, errors);
            }

            return new MobaPayloadFieldIdsValidation(groups, errors);
        }

        private static bool TryCreateGroup(
            Compilation compilation,
            INamedTypeSymbol accessorType,
            AttributeData attribute,
            out MobaPayloadFieldGroup group,
            out string error)
        {
            group = null!;
            error = null!;
            if (attribute.ConstructorArguments.Length != 4 ||
                !(attribute.ConstructorArguments[0].Value is INamedTypeSymbol catalogType) ||
                !(attribute.ConstructorArguments[1].Value is string methodName) ||
                string.IsNullOrWhiteSpace(methodName) ||
                !(attribute.ConstructorArguments[2].Value is bool includeLegacyIds) ||
                attribute.ConstructorArguments[3].Kind != TypedConstantKind.Array)
            {
                error = "constructor arguments could not be resolved";
                return false;
            }

            if (!SyntaxFacts.IsValidIdentifier(methodName))
            {
                error = $"supports method '{methodName}' is not a valid identifier";
                return false;
            }

            if (HasNonPayloadGeneratedMember(accessorType, methodName))
            {
                error = $"supports method '{methodName}' is already declared";
                return false;
            }

            if (catalogType.IsGenericType ||
                !compilation.IsSymbolAccessibleWithin(catalogType, accessorType))
            {
                error = $"field catalog '{catalogType.Name}' must be non-generic and accessible from generated code";
                return false;
            }

            var fieldNames = attribute.ConstructorArguments[3].Values
                .Select(value => value.Value as string)
                .ToArray();
            if (fieldNames.Length == 0 || fieldNames.Any(string.IsNullOrWhiteSpace))
            {
                error = $"supports method '{methodName}' has no fields";
                return false;
            }

            var fields = new List<MobaPayloadFieldInfo>(fieldNames.Length);
            foreach (var fieldName in fieldNames)
            {
                if (!SyntaxFacts.IsValidIdentifier(fieldName!))
                {
                    error = $"'{catalogType.Name}.{fieldName}' must use a supported field identifier";
                    return false;
                }

                var field = catalogType.GetMembers(fieldName!).OfType<IFieldSymbol>().FirstOrDefault();
                if (field == null || !field.IsConst || field.Type.SpecialType != SpecialType.System_String)
                {
                    error = $"'{catalogType.Name}.{fieldName}' must be a const string field";
                    return false;
                }

                if (!compilation.IsSymbolAccessibleWithin(field, accessorType))
                {
                    error = $"'{catalogType.Name}.{fieldName}' must be accessible from generated code";
                    return false;
                }

                fields.Add(new MobaPayloadFieldInfo(fieldName!, includeLegacyIds));
            }

            if (!HasResolverMethod(compilation, accessorType, catalogType, "FieldId"))
            {
                error = $"field catalog '{catalogType.Name}' must declare FieldId(string)";
                return false;
            }

            if (includeLegacyIds &&
                !HasResolverMethod(compilation, accessorType, catalogType, "LegacyFieldId"))
            {
                error = $"field catalog '{catalogType.Name}' must declare LegacyFieldId(string)";
                return false;
            }

            group = new MobaPayloadFieldGroup(catalogType, methodName, fields);
            return true;
        }

        private static bool HasResolverMethod(
            Compilation compilation,
            INamedTypeSymbol accessorType,
            INamedTypeSymbol catalogType,
            string methodName)
        {
            return catalogType.GetMembers(methodName).OfType<IMethodSymbol>().Any(method =>
                method.IsStatic &&
                !method.IsGenericMethod &&
                method.ReturnType.SpecialType == SpecialType.System_Int32 &&
                method.Parameters.Length == 1 &&
                method.Parameters[0].RefKind == RefKind.None &&
                method.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                compilation.IsSymbolAccessibleWithin(method, accessorType));
        }

        private static void ValidateGeneratedMembers(
            INamedTypeSymbol accessorType,
            IReadOnlyList<MobaPayloadFieldGroup> groups,
            ICollection<string> errors)
        {
            foreach (var duplicateMethod in groups
                         .GroupBy(group => group.MethodName, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                errors.Add($"supports method '{duplicateMethod.Key}' is declared more than once");
            }

            foreach (var fieldGroup in groups
                         .SelectMany(group => group.Fields.Select(field => new { Group = group, Field = field }))
                         .GroupBy(item => item.Field.Name, StringComparer.Ordinal))
            {
                var catalogs = fieldGroup
                    .Select(item => item.Group.CatalogType)
                    .Distinct(SymbolEqualityComparer.Default)
                    .Take(2)
                    .ToArray();
                if (catalogs.Length > 1)
                {
                    errors.Add($"field '{fieldGroup.Key}' is declared by multiple field catalogs");
                }
            }

            var generatedNames = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var group in groups)
            {
                AddGeneratedName(
                    accessorType,
                    group.MethodName,
                    "method:" + group.MethodName,
                    generatedNames,
                    errors);
                foreach (var field in group.Fields)
                {
                    var fieldOrigin = field.Name;
                    AddGeneratedName(
                        accessorType,
                        field.Name + "Id",
                        "current:" + fieldOrigin,
                        generatedNames,
                        errors);
                    if (field.IncludeLegacyIds)
                    {
                        AddGeneratedName(
                            accessorType,
                            field.Name + "LegacyId",
                            "legacy:" + fieldOrigin,
                            generatedNames,
                            errors);
                    }
                }
            }
        }

        private static void AddGeneratedName(
            INamedTypeSymbol accessorType,
            string name,
            string origin,
            IDictionary<string, string> generatedNames,
            ICollection<string> errors)
        {
            if (generatedNames.TryGetValue(name, out var existingOrigin))
            {
                if (!string.Equals(existingOrigin, origin, StringComparison.Ordinal))
                {
                    errors.Add($"generated member '{name}' has conflicting declarations");
                }

                return;
            }

            generatedNames.Add(name, origin);

            if (HasNonPayloadGeneratedMember(accessorType, name))
            {
                errors.Add($"generated member '{name}' is already declared");
            }
        }

        private static bool HasNonPayloadGeneratedMember(INamedTypeSymbol accessorType, string name)
        {
            return accessorType.GetMembers(name).Any(member =>
                member.DeclaringSyntaxReferences.Length == 0 ||
                member.DeclaringSyntaxReferences.Any(reference =>
                    !reference.SyntaxTree.FilePath.EndsWith(
                        ".PayloadFieldIds.g.cs",
                        StringComparison.Ordinal)));
        }

        private static bool IsPartial(INamedTypeSymbol type)
        {
            return type.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax())
                .OfType<ClassDeclarationSyntax>()
                .Any(declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
        }

        private static bool IsClassDeclaration(INamedTypeSymbol type)
        {
            return type.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax())
                .Any(syntax => syntax is ClassDeclarationSyntax);
        }
    }

    internal sealed class MobaPayloadFieldIdsValidation
    {
        public MobaPayloadFieldIdsValidation(
            IReadOnlyList<MobaPayloadFieldGroup> groups,
            IReadOnlyList<string> errors)
        {
            Groups = groups;
            Errors = errors;
        }

        public IReadOnlyList<MobaPayloadFieldGroup> Groups { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool IsValid => Errors.Count == 0;
    }

    internal sealed class MobaPayloadFieldGroup
    {
        public MobaPayloadFieldGroup(
            INamedTypeSymbol catalogType,
            string methodName,
            IReadOnlyList<MobaPayloadFieldInfo> fields)
        {
            CatalogType = catalogType;
            CatalogTypeName = catalogType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            MethodName = methodName;
            Fields = fields;
        }

        public INamedTypeSymbol CatalogType { get; }
        public string CatalogTypeName { get; }
        public string MethodName { get; }
        public IReadOnlyList<MobaPayloadFieldInfo> Fields { get; }
    }

    internal sealed class MobaPayloadFieldInfo
    {
        public MobaPayloadFieldInfo(string name, bool includeLegacyIds)
        {
            Name = name;
            IncludeLegacyIds = includeLegacyIds;
        }

        public string Name { get; }
        public bool IncludeLegacyIds { get; }
    }
}
