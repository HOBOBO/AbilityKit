using System.Text.Json;
using System.Text.Json.Serialization;
using AbilityKit.Protocol.Catalog;
using AbilityKit.Protocol.CatalogCompiler.Ir;
using AbilityKit.Protocol.CatalogCompiler.Lowering;

namespace AbilityKit.Protocol.CatalogCompiler.Emit;

/// <summary>
/// Stable JSON projection shared by the command line compiler and the Unity editor. The editor
/// never parses YAML independently, so validation/defaulting stays identical in CLI, CI and Unity.
/// </summary>
public sealed class ProtocolWorkspaceDocument
{
    public int SchemaVersion { get; set; } = ProtocolCatalogConstants.SchemaVersion;
    public string GeneratorVersion { get; set; } = ProtocolCatalogConstants.GeneratorVersion;
    public string[] Projects { get; set; } = Array.Empty<string>();
    public ProtocolWorkspaceCatalog[] Catalogs { get; set; } = Array.Empty<ProtocolWorkspaceCatalog>();
    public ProtocolWorkspaceWireSchema[] WireSchemas { get; set; } = Array.Empty<ProtocolWorkspaceWireSchema>();
    public ProtocolWorkspaceDiagnostic[] Diagnostics { get; set; } = Array.Empty<ProtocolWorkspaceDiagnostic>();
}

public sealed class ProtocolWorkspaceCatalog
{
    public string SourcePath { get; set; } = string.Empty;
    public int SchemaVersion { get; set; } = ProtocolCatalogConstants.SchemaVersion;
    public string CatalogId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public int Revision { get; set; }
    public string DefaultCodec { get; set; } = string.Empty;
    public ProtocolWorkspaceMessage[] Messages { get; set; } = Array.Empty<ProtocolWorkspaceMessage>();
}

public sealed class ProtocolWorkspaceMessage
{
    public string Id { get; set; } = string.Empty;
    public uint OpCode { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string PayloadType { get; set; } = string.Empty;
    public string Codec { get; set; } = string.Empty;
    public string Reliability { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public int MinimumSchemaVersion { get; set; }
    public int MaximumSchemaVersion { get; set; }
    public int MaximumPayloadBytes { get; set; }
    public double CaptureSampleRate { get; set; }
    public string[] SensitiveFields { get; set; } = Array.Empty<string>();
}

public sealed class ProtocolWorkspaceWireSchema
{
    public string SourcePath { get; set; } = string.Empty;
    public int SchemaVersion { get; set; } = WireSchemaFormatVersions.Current;
    public string SourceType { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string MemoryPackMode { get; set; } = "version-tolerant";
    public string Declaration { get; set; } = "class";
    public string MemberStyle { get; set; } = "property";
    public ProtocolWorkspaceWireField[] Fields { get; set; } = Array.Empty<ProtocolWorkspaceWireField>();
    public uint[] ReservedIds { get; set; } = Array.Empty<uint>();
    public string QualifiedType => string.IsNullOrEmpty(Namespace) ? Type : Namespace + "." + Type;
}

public sealed class ProtocolWorkspaceWireField
{
    public uint Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ScalarType { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public bool External { get; set; }
    public bool Array { get; set; }
    public bool Optional { get; set; }
}

public sealed class ProtocolWorkspaceDiagnostic
{
    public string Severity { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string CatalogId { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public static class ProtocolWorkspaceEmitter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static ProtocolWorkspaceDocument Create(
        IReadOnlyList<ProtocolCatalogIr> catalogs,
        IReadOnlyList<string> catalogSources,
        IReadOnlyList<WireSchemaIr> wireSchemas,
        IReadOnlyList<string> wireSchemaSources)
    {
        if (catalogs.Count != catalogSources.Count)
            throw new ArgumentException("Catalog and source counts must match.", nameof(catalogSources));
        if (wireSchemas.Count != wireSchemaSources.Count)
            throw new ArgumentException("Wire schema and source counts must match.", nameof(wireSchemaSources));

        var validation = ProtocolCatalogValidator.Validate(IrLowering.ToRuntime(catalogs));
        var projects = catalogs.Select(value => value.ProjectId)
            .Concat(wireSchemas.Select(value => value.ProjectId))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        return new ProtocolWorkspaceDocument
        {
            Projects = projects,
            Catalogs = catalogs.Select((value, index) => ToCatalog(value, catalogSources[index])).ToArray(),
            WireSchemas = wireSchemas.Select((value, index) => ToWireSchema(value, wireSchemaSources[index])).ToArray(),
            Diagnostics = validation.Diagnostics.Select(value => new ProtocolWorkspaceDiagnostic
            {
                Severity = value.Severity.ToString().ToLowerInvariant(),
                Code = value.Code,
                CatalogId = value.CatalogId,
                MessageId = value.MessageId,
                Message = value.Detail
            }).ToArray()
        };
    }

    public static string Serialize(ProtocolWorkspaceDocument document) =>
        JsonSerializer.Serialize(document, JsonOptions) + Environment.NewLine;

    public static ProtocolWorkspaceCatalog DeserializeCatalog(string json) =>
        JsonSerializer.Deserialize<ProtocolWorkspaceCatalog>(json, JsonOptions)
        ?? throw new InvalidDataException("Catalog editor document is empty.");

    public static ProtocolWorkspaceWireSchema DeserializeWireSchema(string json) =>
        JsonSerializer.Deserialize<ProtocolWorkspaceWireSchema>(json, JsonOptions)
        ?? throw new InvalidDataException("Wire schema editor document is empty.");

    public static WireSchemaIr ToWireSchemaIr(ProtocolWorkspaceWireSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        return new WireSchemaIr(
            WireSchemaFormatVersions.Current,
            schema.Type?.Trim() ?? string.Empty,
            (schema.Fields ?? Array.Empty<ProtocolWorkspaceWireField>()).Select(field => new WireFieldIr(
                field.Id,
                field.Name?.Trim() ?? string.Empty,
                string.IsNullOrWhiteSpace(field.TypeName)
                    ? field.ScalarType?.Trim().ToLowerInvariant() ?? string.Empty
                    : string.Empty,
                field.Array,
                field.Optional,
                string.IsNullOrWhiteSpace(field.TypeName) ? null : field.TypeName.Trim(),
                field.External)).ToArray(),
            schema.ReservedIds ?? Array.Empty<uint>(),
            schema.ProjectId?.Trim(),
            schema.Namespace?.Trim(),
            ParseMemoryPackMode(schema.MemoryPackMode),
            ParseDeclaration(schema.Declaration),
            ParseMemberStyle(schema.MemberStyle),
            schema.GroupId?.Trim());
    }

    private static ProtocolWorkspaceCatalog ToCatalog(ProtocolCatalogIr value, string source) => new()
    {
        SourcePath = NormalizePath(source),
        CatalogId = value.CatalogId,
        ProjectId = value.ProjectId,
        Domain = value.Domain,
        Revision = value.Revision,
        DefaultCodec = value.DefaultCodec,
        Messages = value.Messages.Select(ToMessage).ToArray()
    };

    private static ProtocolWorkspaceMessage ToMessage(ProtocolMessageIr value) => new()
    {
        Id = value.Id,
        OpCode = value.OpCode,
        Direction = FormatDirection(value.Direction),
        Kind = value.Kind.ToString().ToLowerInvariant(),
        PayloadType = value.PayloadType,
        Codec = value.Codec,
        Reliability = value.Reliability.ToString().ToLowerInvariant(),
        Response = value.ResponseId,
        MinimumSchemaVersion = value.MinimumSchemaVersion,
        MaximumSchemaVersion = value.MaximumSchemaVersion,
        MaximumPayloadBytes = value.MaximumPayloadBytes,
        CaptureSampleRate = value.CaptureSampleRate,
        SensitiveFields = value.SensitiveFields.ToArray()
    };

    private static ProtocolWorkspaceWireSchema ToWireSchema(WireSchemaIr value, string source) => new()
    {
        SourcePath = NormalizePath(source),
        SchemaVersion = value.SchemaVersion,
        SourceType = value.Type,
        ProjectId = value.ProjectId,
        GroupId = value.GroupId,
        Namespace = value.TargetNamespace,
        Type = value.Type,
        MemoryPackMode = value.MemoryPackMode == WireMemoryPackMode.Sequential
            ? "sequential"
            : "version-tolerant",
        Declaration = value.DeclarationKind == WireDeclarationKind.Struct ? "struct" : "class",
        MemberStyle = value.MemberStyle == WireMemberStyle.Field ? "field" : "property",
        Fields = value.Fields.Select(field => new ProtocolWorkspaceWireField
        {
            Id = field.Id,
            Name = field.Name,
            ScalarType = field.ScalarType,
            TypeName = field.TypeName,
            External = field.IsExternalReference,
            Array = field.IsArray,
            Optional = field.IsOptional
        }).ToArray(),
        ReservedIds = value.ReservedIds.ToArray()
    };

    private static WireMemoryPackMode ParseMemoryPackMode(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "version-tolerant" => WireMemoryPackMode.VersionTolerant,
            "sequential" => WireMemoryPackMode.Sequential,
            _ => throw new InvalidDataException($"Unsupported memoryPackMode '{value}'.")
        };

    private static WireDeclarationKind ParseDeclaration(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "class" => WireDeclarationKind.Class,
            "struct" => WireDeclarationKind.Struct,
            _ => throw new InvalidDataException($"Unsupported declaration '{value}'.")
        };

    private static WireMemberStyle ParseMemberStyle(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "property" => WireMemberStyle.Property,
            "field" => WireMemberStyle.Field,
            _ => throw new InvalidDataException($"Unsupported memberStyle '{value}'.")
        };

    private static string FormatDirection(IrDirection value) => value switch
    {
        IrDirection.ClientToServer => "c2s",
        IrDirection.ServerToClient => "s2c",
        _ => "bidirectional"
    };

    private static string NormalizePath(string path) => Path.GetFullPath(path).Replace('\\', '/');
}
