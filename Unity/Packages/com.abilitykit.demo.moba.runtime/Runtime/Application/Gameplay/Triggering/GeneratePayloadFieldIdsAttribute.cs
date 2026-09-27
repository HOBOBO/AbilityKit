using System;

namespace AbilityKit.Demo.Moba
{
    public enum PayloadAccessorValueKind
    {
        Int = 0,
        Double = 1,
        Enum = 2,
        Bool = 3,
        Fixed64 = 4,
        ClampMinOneInt = 5,
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class GeneratePayloadFieldIdsAttribute : Attribute
    {
        public GeneratePayloadFieldIdsAttribute(
            Type fieldCatalogType,
            string supportsMethodName,
            bool includeLegacyIds,
            params string[] fieldNames)
        {
            FieldCatalogType = fieldCatalogType;
            SupportsMethodName = supportsMethodName;
            IncludeLegacyIds = includeLegacyIds;
            FieldNames = fieldNames ?? Array.Empty<string>();
        }

        public Type FieldCatalogType { get; }
        public string SupportsMethodName { get; }
        public bool IncludeLegacyIds { get; }
        public string[] FieldNames { get; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class GeneratePayloadAccessorAttribute : Attribute
    {
        public Type PayloadType { get; }
        public Type FieldCatalogType { get; }
        public string FieldName { get; }
        public string SourceMember { get; }
        public PayloadAccessorValueKind ValueKind { get; }
        public bool IncludeLegacyId { get; }

        public GeneratePayloadAccessorAttribute(
            Type payloadType,
            Type fieldCatalogType,
            string fieldName,
            string sourceMember,
            PayloadAccessorValueKind valueKind,
            bool includeLegacyId = false)
        {
            PayloadType = payloadType;
            FieldCatalogType = fieldCatalogType;
            FieldName = fieldName;
            SourceMember = sourceMember;
            ValueKind = valueKind;
            IncludeLegacyId = includeLegacyId;
        }
    }
}
