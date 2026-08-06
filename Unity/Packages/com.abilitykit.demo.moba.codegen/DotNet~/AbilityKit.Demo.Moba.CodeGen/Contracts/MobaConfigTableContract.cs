using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace AbilityKit.Demo.Moba.CodeGen
{
    internal static class MobaConfigTableContract
    {
        public const string AttributeMetadataName =
            "AbilityKit.Demo.Moba.Config.Core.MobaConfigTableAttribute";
        public const string ManifestMetadataName =
            "AbilityKit.Demo.Moba.Config.Core.MobaGeneratedConfigTableManifest";

        public static bool TryCreateSpec(
            AttributeData attribute,
            out MobaConfigTableSpec spec,
            out string error)
        {
            spec = null!;
            if (attribute.ConstructorArguments.Length != 5 ||
                !(attribute.ConstructorArguments[0].Value is string filePath) ||
                !(attribute.ConstructorArguments[1].Value is INamedTypeSymbol dtoType) ||
                !(attribute.ConstructorArguments[2].Value is INamedTypeSymbol moType) ||
                !(attribute.ConstructorArguments[3].Value is string groupName) ||
                !(attribute.ConstructorArguments[4].Value is int order))
            {
                error = "the declaration must provide constant file path, DTO type, MO type, group name, and order";
                return false;
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                error = "file path must not be empty";
                return false;
            }

            if (string.IsNullOrWhiteSpace(groupName))
            {
                error = "group name must not be empty";
                return false;
            }

            if (dtoType.TypeKind != TypeKind.Class || dtoType.IsAbstract || dtoType.IsGenericType ||
                !GeneratedCodeSymbolRules.CanReferenceType(dtoType))
            {
                error = $"DTO type '{dtoType.Name}' must be a concrete, non-generic class accessible from generated code";
                return false;
            }

            if (moType.TypeKind != TypeKind.Class || moType.IsAbstract || moType.IsGenericType ||
                !GeneratedCodeSymbolRules.CanReferenceType(moType))
            {
                error = $"MO type '{moType.Name}' must be a concrete, non-generic class accessible from generated code";
                return false;
            }

            if (!TryGetPublicIntKey(dtoType, out var keyMemberName))
            {
                error = $"DTO type '{dtoType.Name}' must expose a public int Id or Code field/property";
                return false;
            }

            if (!HasPublicDtoConstructor(moType, dtoType))
            {
                error = $"MO type '{moType.Name}' must expose a public constructor accepting exactly '{dtoType.Name}'";
                return false;
            }

            spec = new MobaConfigTableSpec(
                filePath,
                dtoType,
                moType,
                groupName,
                order,
                keyMemberName);
            error = null!;
            return true;
        }

        public static bool IsSourceManifest(INamedTypeSymbol? manifestType)
        {
            return manifestType != null && manifestType.Locations.Any(location => location.IsInSource);
        }

        private static bool TryGetPublicIntKey(INamedTypeSymbol dtoType, out string keyMemberName)
        {
            foreach (var candidateName in new[] { "Id", "Code" })
            {
                for (var current = dtoType; current != null; current = current.BaseType)
                {
                    var members = current.GetMembers(candidateName);
                    foreach (var member in members)
                    {
                        if (member.DeclaredAccessibility != Accessibility.Public || member.IsStatic) continue;
                        if (member is IFieldSymbol field &&
                            field.Type.SpecialType == SpecialType.System_Int32)
                        {
                            keyMemberName = candidateName;
                            return true;
                        }

                        if (member is IPropertySymbol property &&
                            property.Type.SpecialType == SpecialType.System_Int32 &&
                            property.GetMethod != null &&
                            property.GetMethod.DeclaredAccessibility == Accessibility.Public)
                        {
                            keyMemberName = candidateName;
                            return true;
                        }
                    }

                    if (members.Length > 0) break;
                }
            }

            keyMemberName = null!;
            return false;
        }

        private static bool HasPublicDtoConstructor(INamedTypeSymbol moType, INamedTypeSymbol dtoType)
        {
            return moType.InstanceConstructors.Any(constructor =>
                constructor.DeclaredAccessibility == Accessibility.Public &&
                constructor.Parameters.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, dtoType));
        }
    }

    internal sealed class MobaConfigTableSpec
    {
        public MobaConfigTableSpec(
            string filePath,
            INamedTypeSymbol dtoType,
            INamedTypeSymbol moType,
            string groupName,
            int order,
            string keyMemberName)
        {
            FilePath = filePath;
            DtoType = dtoType;
            MoType = moType;
            GroupName = groupName;
            Order = order;
            KeyMemberName = keyMemberName;
            QualifiedDtoTypeName = dtoType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            QualifiedMoTypeName = moType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        public string FilePath { get; }
        public INamedTypeSymbol DtoType { get; }
        public INamedTypeSymbol MoType { get; }
        public string GroupName { get; }
        public int Order { get; }
        public string KeyMemberName { get; }
        public string QualifiedDtoTypeName { get; }
        public string QualifiedMoTypeName { get; }
    }

    internal sealed class MobaConfigTableUniquenessTracker
    {
        private readonly HashSet<string> _paths = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<INamedTypeSymbol> _dtoTypes =
            new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        private readonly HashSet<INamedTypeSymbol> _moTypes =
            new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        public bool TryAdd(MobaConfigTableSpec spec, out string duplicateKind, out string duplicateKey)
        {
            if (_paths.Contains(spec.FilePath))
            {
                duplicateKind = "file path";
                duplicateKey = spec.FilePath;
                return false;
            }

            if (_dtoTypes.Contains(spec.DtoType))
            {
                duplicateKind = "DTO type";
                duplicateKey = spec.QualifiedDtoTypeName;
                return false;
            }

            if (_moTypes.Contains(spec.MoType))
            {
                duplicateKind = "MO type";
                duplicateKey = spec.QualifiedMoTypeName;
                return false;
            }

            _paths.Add(spec.FilePath);
            _dtoTypes.Add(spec.DtoType);
            _moTypes.Add(spec.MoType);
            duplicateKind = null!;
            duplicateKey = null!;
            return true;
        }
    }
}
