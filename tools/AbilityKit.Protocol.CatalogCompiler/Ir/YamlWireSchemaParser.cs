using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AbilityKit.Protocol.CatalogCompiler.Ir;

/// <summary>Parses the deliberately small YAML wire schema document.</summary>
public sealed class YamlWireSchemaParser : IWireSchemaParser
{
    public WireSchemaIr Parse(string sourcePath, string sourceText)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var source = deserializer.Deserialize<WireSchemaSource>(sourceText)
            ?? throw Invalid(sourcePath, "document is empty");

        if (source.SchemaVersion != ProtocolCatalogConstants.SchemaVersion)
            throw Invalid(sourcePath, $"unsupported schemaVersion {source.SchemaVersion}");
        var type = source.Type?.Trim() ?? string.Empty;
        if (type.Length == 0)
            throw Invalid(sourcePath, "type is required");
        var projectId = source.ProjectId?.Trim() ?? string.Empty;
        var targetNamespace = source.Namespace?.Trim() ?? string.Empty;
        if ((projectId.Length == 0) != (targetNamespace.Length == 0))
            throw Invalid(sourcePath, "projectId and namespace must be specified together");
        var memoryPackMode = ParseMemoryPackMode(source.MemoryPackMode, sourcePath);
        var declarationKind = ParseDeclarationKind(source.Declaration, sourcePath);
        var memberStyle = ParseMemberStyle(source.MemberStyle, sourcePath);

        var fields = new List<WireFieldIr>(source.Fields?.Count ?? 0);
        var fieldIds = new HashSet<uint>();
        var fieldNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in source.Fields ?? Enumerable.Empty<WireFieldSource>())
        {
            var name = item.Name?.Trim() ?? string.Empty;
            var scalarType = item.ScalarType?.Trim().ToLowerInvariant() ?? string.Empty;
            var typeName = item.Type?.Trim() ?? string.Empty;
            if (name.Length == 0) throw Invalid(sourcePath, "field name is required");
            if ((scalarType.Length == 0) == (typeName.Length == 0))
                throw Invalid(sourcePath, $"field '{name}' must specify exactly one of scalarType or type");
            if (scalarType.Length > 0 && !WireScalarTypes.Known.Contains(scalarType))
                throw Invalid(sourcePath, $"field '{name}' has unsupported scalarType '{item.ScalarType}'");
            if (!fieldIds.Add(item.Id)) throw Invalid(sourcePath, $"duplicate field id {item.Id}");
            if (!fieldNames.Add(name)) throw Invalid(sourcePath, $"duplicate field name '{name}'");
            if (item.Optional.HasValue && item.Required.HasValue)
                throw Invalid(sourcePath, $"field '{name}' cannot specify both optional and required");

            var isOptional = item.Optional ?? (item.Required.HasValue ? !item.Required.Value : false);
            fields.Add(new WireFieldIr(item.Id, name, scalarType, item.Array, isOptional, typeName));
        }

        var reserved = new List<uint>(source.ReservedIds ?? new List<uint>());
        var reservedSet = new HashSet<uint>();
        foreach (var id in reserved)
        {
            if (!reservedSet.Add(id)) throw Invalid(sourcePath, $"duplicate reserved id {id}");
            if (fieldIds.Contains(id)) throw Invalid(sourcePath, $"reserved id {id} is used by a field");
        }

        if (memoryPackMode == WireMemoryPackMode.Sequential)
        {
            var orderedIds = fieldIds.OrderBy(value => value).ToArray();
            for (var i = 0; i < orderedIds.Length; i++)
            {
                if (orderedIds[i] != (uint)i)
                    throw Invalid(sourcePath, "sequential MemoryPack field ids must be contiguous and start at zero");
            }
            if (reserved.Count > 0)
                throw Invalid(sourcePath, "sequential MemoryPack schemas cannot reserve field ids");
        }

        return new WireSchemaIr(
            source.SchemaVersion,
            type,
            fields,
            reserved,
            projectId,
            targetNamespace,
            memoryPackMode,
            declarationKind,
            memberStyle);
    }

    private static WireMemoryPackMode ParseMemoryPackMode(string? value, string path) =>
        (value?.Trim().ToLowerInvariant() ?? "version-tolerant") switch
        {
            "version-tolerant" => WireMemoryPackMode.VersionTolerant,
            "sequential" => WireMemoryPackMode.Sequential,
            var invalid => throw Invalid(path, $"unsupported memoryPackMode '{invalid}'")
        };

    private static WireDeclarationKind ParseDeclarationKind(string? value, string path) =>
        (value?.Trim().ToLowerInvariant() ?? "class") switch
        {
            "class" => WireDeclarationKind.Class,
            "struct" => WireDeclarationKind.Struct,
            var invalid => throw Invalid(path, $"unsupported declaration '{invalid}'")
        };

    private static WireMemberStyle ParseMemberStyle(string? value, string path) =>
        (value?.Trim().ToLowerInvariant() ?? "property") switch
        {
            "property" => WireMemberStyle.Property,
            "field" => WireMemberStyle.Field,
            var invalid => throw Invalid(path, $"unsupported memberStyle '{invalid}'")
        };

    private static InvalidDataException Invalid(string path, string message) =>
        new($"Invalid wire schema '{path}': {message}.");

    private sealed class WireSchemaSource
    {
        public int SchemaVersion { get; set; }
        public string? ProjectId { get; set; }
        public string? Namespace { get; set; }
        public string? Type { get; set; }
        public string? MemoryPackMode { get; set; }
        public string? Declaration { get; set; }
        public string? MemberStyle { get; set; }
        public List<WireFieldSource>? Fields { get; set; }
        public List<uint>? ReservedIds { get; set; }
    }

    private sealed class WireFieldSource
    {
        public uint Id { get; set; }
        public string? Name { get; set; }
        public string? ScalarType { get; set; }
        public string? Type { get; set; }
        public bool Array { get; set; }
        public bool? Optional { get; set; }
        public bool? Required { get; set; }
    }
}
