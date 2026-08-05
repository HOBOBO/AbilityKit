using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace AbilityKit.Demo.Moba.CodeGen
{
    internal static class MobaTargetQueryFactoryContract
    {
        private static readonly MobaTargetQueryFactoryDefinition[] Definitions =
        {
            new MobaTargetQueryFactoryDefinition(
                "Source",
                "AbilityKit.Demo.Moba.Services.Search.MobaTargetSourceProviderAttribute",
                "AbilityKit.Demo.Moba.Services.Search.IMobaTargetSourceFactory",
                "RegisterSource"),
            new MobaTargetQueryFactoryDefinition(
                "Filter",
                "AbilityKit.Demo.Moba.Services.Search.MobaTargetFilterAttribute",
                "AbilityKit.Demo.Moba.Services.Search.IMobaTargetFilterFactory",
                "RegisterFilter"),
            new MobaTargetQueryFactoryDefinition(
                "Order",
                "AbilityKit.Demo.Moba.Services.Search.MobaTargetOrderAttribute",
                "AbilityKit.Demo.Moba.Services.Search.IMobaTargetOrderFactory",
                "RegisterOrder"),
            new MobaTargetQueryFactoryDefinition(
                "Select",
                "AbilityKit.Demo.Moba.Services.Search.MobaTargetSelectAttribute",
                "AbilityKit.Demo.Moba.Services.Search.IMobaTargetSelectFactory",
                "RegisterSelect")
        };

        public static IReadOnlyList<MobaResolvedTargetQueryFactoryDefinition> ResolveDefinitions(
            Compilation compilation)
        {
            return Definitions
                .Select(definition => definition.Resolve(compilation))
                .OfType<MobaResolvedTargetQueryFactoryDefinition>()
                .ToArray();
        }

        public static bool TryCreateMapping(
            INamedTypeSymbol type,
            MobaResolvedTargetQueryFactoryDefinition definition,
            AttributeData attribute,
            out MobaTargetQueryFactoryMapping mapping,
            out string error)
        {
            mapping = null!;
            if (!TryValidateFactory(type, definition.InterfaceType, out error))
            {
                return false;
            }

            if (attribute.ConstructorArguments.Length != 1 ||
                !(attribute.ConstructorArguments[0].Value is int code))
            {
                error = "the factory code is not a constant int";
                return false;
            }

            mapping = new MobaTargetQueryFactoryMapping(definition, type, code);
            error = null!;
            return true;
        }

        public static string GetDuplicateKey(MobaTargetQueryFactoryMapping mapping)
        {
            return mapping.Definition.Kind + ":" + mapping.Code;
        }

        private static bool TryValidateFactory(
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

    internal sealed class MobaTargetQueryFactoryDefinition
    {
        public MobaTargetQueryFactoryDefinition(
            string kind,
            string attributeName,
            string interfaceName,
            string registerMethod)
        {
            Kind = kind;
            AttributeName = attributeName;
            InterfaceName = interfaceName;
            RegisterMethod = registerMethod;
        }

        public string Kind { get; }
        public string AttributeName { get; }
        public string InterfaceName { get; }
        public string RegisterMethod { get; }

        public MobaResolvedTargetQueryFactoryDefinition? Resolve(Compilation compilation)
        {
            var attributeType = compilation.GetTypeByMetadataName(AttributeName);
            var interfaceType = compilation.GetTypeByMetadataName(InterfaceName);
            return attributeType == null || interfaceType == null
                ? null
                : new MobaResolvedTargetQueryFactoryDefinition(
                    Kind,
                    RegisterMethod,
                    attributeType,
                    interfaceType);
        }
    }

    internal sealed class MobaResolvedTargetQueryFactoryDefinition
    {
        public MobaResolvedTargetQueryFactoryDefinition(
            string kind,
            string registerMethod,
            INamedTypeSymbol attributeType,
            INamedTypeSymbol interfaceType)
        {
            Kind = kind;
            RegisterMethod = registerMethod;
            AttributeType = attributeType;
            InterfaceType = interfaceType;
        }

        public string Kind { get; }
        public string RegisterMethod { get; }
        public INamedTypeSymbol AttributeType { get; }
        public INamedTypeSymbol InterfaceType { get; }
    }

    internal sealed class MobaTargetQueryFactoryMapping
    {
        public MobaTargetQueryFactoryMapping(
            MobaResolvedTargetQueryFactoryDefinition definition,
            INamedTypeSymbol factoryType,
            int code)
        {
            Definition = definition;
            FactoryType = factoryType;
            Code = code;
            QualifiedTypeName = factoryType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        public MobaResolvedTargetQueryFactoryDefinition Definition { get; }
        public INamedTypeSymbol FactoryType { get; }
        public int Code { get; }
        public string QualifiedTypeName { get; }
    }
}
