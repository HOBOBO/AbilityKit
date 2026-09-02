using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AbilityKit.Protocol.CatalogCompiler.Ir;

/// <summary>Parses the canonical grouped YAML wire schema document.</summary>
public sealed class YamlWireSchemaParser : IWireSchemaParser
{
    private static readonly Regex GroupIdPattern = new(
        "^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$",
        RegexOptions.CultureInvariant);

    public WireSchemaIr Parse(string sourcePath, string sourceText)
    {
        var document = ParseDocument(sourcePath, sourceText);
        if (document.Schemas.Count != 1)
            throw Invalid(
                sourcePath,
                $"document contains {document.Schemas.Count} types; use ParseDocument for grouped wire schemas");
        return document.Schemas[0];
    }

    public WireSchemaDocumentIr ParseDocument(string sourcePath, string sourceText)
    {
        WireSchemaDocumentSource source;
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            source = deserializer.Deserialize<WireSchemaDocumentSource>(sourceText)
                ?? throw Invalid(sourcePath, "document is empty");
        }
        catch (YamlException exception)
        {
            throw Invalid(sourcePath, exception.Message);
        }

        if (source.SchemaVersion != WireSchemaFormatVersions.Current)
            throw Invalid(
                sourcePath,
                $"unsupported schemaVersion {source.SchemaVersion}; expected {WireSchemaFormatVersions.Current}");

        var projectId = source.ProjectId?.Trim() ?? string.Empty;
        var targetNamespace = source.Namespace?.Trim() ?? string.Empty;
        var groupId = source.GroupId?.Trim() ?? string.Empty;
        if (projectId.Length == 0) throw Invalid(sourcePath, "projectId is required");
        if (targetNamespace.Length == 0) throw Invalid(sourcePath, "namespace is required");
        if (!GroupIdPattern.IsMatch(groupId))
            throw Invalid(
                sourcePath,
                "groupId must be a lower-case domain id using letters, digits, dots or hyphens");
        if (source.Types == null || source.Types.Count == 0)
            throw Invalid(sourcePath, "types must contain at least one wire type");

        var defaultMemoryPackMode = ParseMemoryPackMode(source.Defaults?.MemoryPackMode, sourcePath);
        var defaultDeclarationKind = ParseDeclarationKind(source.Defaults?.Declaration, sourcePath);
        var defaultMemberStyle = ParseMemberStyle(source.Defaults?.MemberStyle, sourcePath);
        var schemas = new List<WireSchemaIr>(source.Types.Count);
        var typeNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in source.Types)
        {
            if (item == null)
                throw Invalid(sourcePath, "types cannot contain an empty item");
            var typeName = item.Name?.Trim() ?? string.Empty;
            if (typeName.Length == 0)
                throw Invalid(sourcePath, "type name is required");
            if (!typeNames.Add(typeName))
                throw Invalid(sourcePath, $"duplicate wire type name '{typeName}'");
            if (item.Fields == null)
                throw Invalid(sourcePath, $"type '{typeName}' must define fields");
            schemas.Add(ParseType(
                sourcePath,
                typeName,
                item.Fields,
                item.ReservedIds,
                projectId,
                targetNamespace,
                groupId,
                item.MemoryPackMode == null
                    ? defaultMemoryPackMode
                    : ParseMemoryPackMode(item.MemoryPackMode, sourcePath),
                item.Declaration == null
                    ? defaultDeclarationKind
                    : ParseDeclarationKind(item.Declaration, sourcePath),
                item.MemberStyle == null
                    ? defaultMemberStyle
                    : ParseMemberStyle(item.MemberStyle, sourcePath)));
        }

        return new WireSchemaDocumentIr(
            source.SchemaVersion,
            projectId,
            targetNamespace,
            groupId,
            defaultMemoryPackMode,
            defaultDeclarationKind,
            defaultMemberStyle,
            schemas);
    }

    private static WireSchemaIr ParseType(
        string sourcePath,
        string type,
        List<WireFieldSource?> sourceFields,
        List<uint>? sourceReservedIds,
        string projectId,
        string targetNamespace,
        string groupId,
        WireMemoryPackMode memoryPackMode,
        WireDeclarationKind declarationKind,
        WireMemberStyle memberStyle)
    {
        var fields = new List<WireFieldIr>(sourceFields.Count);
        var fieldIds = new HashSet<uint>();
        var fieldNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in sourceFields)
        {
            if (item == null) throw Invalid(sourcePath, $"type '{type}' contains an empty field");
            var name = item.Name?.Trim() ?? string.Empty;
            var scalarType = item.ScalarType?.Trim().ToLowerInvariant() ?? string.Empty;
            var typeName = item.Type?.Trim() ?? string.Empty;
            if (name.Length == 0) throw Invalid(sourcePath, $"type '{type}' has a field without a name");
            if ((scalarType.Length == 0) == (typeName.Length == 0))
                throw Invalid(sourcePath, $"field '{type}.{name}' must specify exactly one of scalarType or type");
            if (item.External && typeName.Length == 0)
                throw Invalid(sourcePath, $"field '{type}.{name}' can only set external for a custom type");
            if (scalarType.Length > 0 && !WireScalarTypes.Known.Contains(scalarType))
                throw Invalid(sourcePath, $"field '{type}.{name}' has unsupported scalarType '{item.ScalarType}'");
            if (!fieldIds.Add(item.Id)) throw Invalid(sourcePath, $"type '{type}' has duplicate field id {item.Id}");
            if (!fieldNames.Add(name)) throw Invalid(sourcePath, $"type '{type}' has duplicate field name '{name}'");
            if (item.Optional.HasValue && item.Required.HasValue)
                throw Invalid(sourcePath, $"field '{type}.{name}' cannot specify both optional and required");

            var isOptional = item.Optional ?? (item.Required.HasValue ? !item.Required.Value : false);
            fields.Add(new WireFieldIr(
                item.Id,
                name,
                scalarType,
                item.Array,
                isOptional,
                typeName,
                item.External));
        }

        var reserved = new List<uint>(sourceReservedIds ?? new List<uint>());
        var reservedSet = new HashSet<uint>();
        foreach (var id in reserved)
        {
            if (!reservedSet.Add(id)) throw Invalid(sourcePath, $"type '{type}' has duplicate reserved id {id}");
            if (fieldIds.Contains(id)) throw Invalid(sourcePath, $"type '{type}' uses reserved id {id}");
        }

        if (memoryPackMode == WireMemoryPackMode.Sequential)
        {
            var orderedIds = fieldIds.OrderBy(value => value).ToArray();
            for (var i = 0; i < orderedIds.Length; i++)
            {
                if (orderedIds[i] != (uint)i)
                    throw Invalid(
                        sourcePath,
                        $"type '{type}' uses sequential MemoryPack but its field ids are not contiguous from zero");
            }
            if (reserved.Count > 0)
                throw Invalid(sourcePath, $"type '{type}' uses sequential MemoryPack and cannot reserve field ids");
        }

        return new WireSchemaIr(
            WireSchemaFormatVersions.Current,
            type,
            fields,
            reserved,
            projectId,
            targetNamespace,
            memoryPackMode,
            declarationKind,
            memberStyle,
            groupId);
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

    private sealed class WireSchemaDocumentSource
    {
        public int SchemaVersion { get; set; }
        public string? ProjectId { get; set; }
        public string? GroupId { get; set; }
        public string? Namespace { get; set; }
        public WireDefaultsSource? Defaults { get; set; }
        public List<WireTypeSource?>? Types { get; set; }
    }

    private sealed class WireDefaultsSource
    {
        public string? MemoryPackMode { get; set; }
        public string? Declaration { get; set; }
        public string? MemberStyle { get; set; }
    }

    private sealed class WireTypeSource
    {
        public string? Name { get; set; }
        public string? MemoryPackMode { get; set; }
        public string? Declaration { get; set; }
        public string? MemberStyle { get; set; }
        public List<WireFieldSource?>? Fields { get; set; }
        public List<uint>? ReservedIds { get; set; }
    }

    private sealed class WireFieldSource
    {
        public uint Id { get; set; }
        public string? Name { get; set; }
        public string? ScalarType { get; set; }
        public string? Type { get; set; }
        public bool External { get; set; }
        public bool Array { get; set; }
        public bool? Optional { get; set; }
        public bool? Required { get; set; }
    }
}
