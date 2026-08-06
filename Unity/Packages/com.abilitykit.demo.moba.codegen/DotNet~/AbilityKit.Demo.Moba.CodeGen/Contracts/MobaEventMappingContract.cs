using Microsoft.CodeAnalysis;

namespace AbilityKit.Demo.Moba.CodeGen
{
    internal static class MobaEventMappingContract
    {
        public const string AttributeMetadataName = "AbilityKit.Demo.Moba.Systems.MobaTriggerEventAttribute";

        public static bool TryCreateMapping(
            INamedTypeSymbol ownerType,
            AttributeData attribute,
            out MobaEventMapping mapping,
            out string error)
        {
            mapping = null!;
            error = null!;
            if (attribute.ConstructorArguments.Length != 3 ||
                !(attribute.ConstructorArguments[0].Value is string eventId) ||
                string.IsNullOrWhiteSpace(eventId) ||
                !(attribute.ConstructorArguments[1].Value is INamedTypeSymbol argsType) ||
                !(attribute.ConstructorArguments[2].Value is bool isPrefix))
            {
                error = "event ID/prefix, args type, and mapping kind must be compile-time constants";
                return false;
            }

            if (!GeneratedCodeSymbolRules.CanReferenceType(argsType))
            {
                error = $"args type '{argsType.Name}' must be a closed type accessible from generated code";
                return false;
            }

            mapping = new MobaEventMapping(ownerType, eventId, argsType, isPrefix);
            return true;
        }

        public static string GetDuplicateKey(MobaEventMapping mapping)
        {
            return (mapping.IsPrefix ? "prefix:" : "exact:") + mapping.EventId;
        }
    }

    internal sealed class MobaEventMapping
    {
        public MobaEventMapping(
            INamedTypeSymbol ownerType,
            string eventId,
            INamedTypeSymbol argsType,
            bool isPrefix)
        {
            OwnerType = ownerType;
            EventId = eventId;
            ArgsType = argsType;
            IsPrefix = isPrefix;
            QualifiedOwnerTypeName = ownerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        public INamedTypeSymbol OwnerType { get; }
        public string EventId { get; }
        public INamedTypeSymbol ArgsType { get; }
        public bool IsPrefix { get; }
        public string QualifiedOwnerTypeName { get; }
    }
}
