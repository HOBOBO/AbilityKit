using System;

namespace AbilityKit.Demo.Moba
{
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
}
