using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using AbilityKit.Protocol.CatalogCompiler.Ir;

namespace AbilityKit.Protocol.CatalogCompiler.Emit;

/// <summary>Writes canonical YAML documents from the editor workspace contract.</summary>
public static class ProtocolYamlEmitter
{
    private const string CatalogSchemaHeader =
        "# yaml-language-server: $schema=../protocol-catalog.schema.json\n";
    private const string WireSchemaHeader =
        "# yaml-language-server: $schema=../wire-schema.schema.json\n";

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .DisableAliases()
        .Build();

    public static string EmitCatalog(ProtocolWorkspaceCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var source = new CatalogSource
        {
            SchemaVersion = ProtocolCatalogConstants.SchemaVersion,
            CatalogId = catalog.CatalogId?.Trim() ?? string.Empty,
            ProjectId = catalog.ProjectId?.Trim() ?? string.Empty,
            Domain = catalog.Domain?.Trim() ?? string.Empty,
            Revision = catalog.Revision,
            DefaultCodec = catalog.DefaultCodec?.Trim() ?? string.Empty,
            Messages = (catalog.Messages ?? Array.Empty<ProtocolWorkspaceMessage>())
                .Select(message => ToMessage(message, catalog.DefaultCodec ?? string.Empty))
                .ToArray()
        };
        return Normalize(CatalogSchemaHeader + Serializer.Serialize(source));
    }

    public static string EmitWireSchema(ProtocolWorkspaceWireSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var source = new WireSchemaSource
        {
            SchemaVersion = ProtocolCatalogConstants.SchemaVersion,
            ProjectId = EmptyToNull(schema.ProjectId),
            Namespace = EmptyToNull(schema.Namespace),
            Type = schema.Type?.Trim() ?? string.Empty,
            MemoryPackMode = string.Equals(
                schema.MemoryPackMode,
                "version-tolerant",
                StringComparison.OrdinalIgnoreCase)
                ? null
                : EmptyToNull(schema.MemoryPackMode),
            Declaration = string.Equals(schema.Declaration, "class", StringComparison.OrdinalIgnoreCase)
                ? null
                : EmptyToNull(schema.Declaration),
            MemberStyle = string.Equals(schema.MemberStyle, "property", StringComparison.OrdinalIgnoreCase)
                ? null
                : EmptyToNull(schema.MemberStyle),
            Fields = (schema.Fields ?? Array.Empty<ProtocolWorkspaceWireField>()).Select(field => new WireFieldSource
            {
                Id = field.Id,
                Name = field.Name?.Trim() ?? string.Empty,
                ScalarType = string.IsNullOrWhiteSpace(field.TypeName)
                    ? field.ScalarType?.Trim().ToLowerInvariant()
                    : null,
                Type = EmptyToNull(field.TypeName),
                Array = field.Array ? true : null,
                Optional = field.Optional ? true : null,
                Required = field.Optional ? null : true
            }).ToArray(),
            ReservedIds = schema.ReservedIds is { Length: > 0 } ? schema.ReservedIds : null
        };
        return Normalize(WireSchemaHeader + Serializer.Serialize(source));
    }

    private static MessageSource ToMessage(ProtocolWorkspaceMessage message, string defaultCodec) => new()
    {
        Id = message.Id?.Trim() ?? string.Empty,
        OpCode = message.OpCode,
        Direction = message.Direction?.Trim().ToLowerInvariant() ?? string.Empty,
        Kind = message.Kind?.Trim().ToLowerInvariant() ?? string.Empty,
        PayloadType = message.PayloadType?.Trim() ?? string.Empty,
        Codec = string.Equals(message.Codec, defaultCodec, StringComparison.Ordinal) ? null : EmptyToNull(message.Codec),
        Reliability = string.Equals(message.Reliability, "reliable", StringComparison.OrdinalIgnoreCase)
            ? null
            : EmptyToNull(message.Reliability),
        Response = EmptyToNull(message.Response),
        MinimumSchemaVersion = message.MinimumSchemaVersion == 1 ? null : message.MinimumSchemaVersion,
        MaximumSchemaVersion = message.MaximumSchemaVersion == message.MinimumSchemaVersion
            ? null
            : message.MaximumSchemaVersion,
        MaximumPayloadBytes = message.MaximumPayloadBytes == 1048576 ? null : message.MaximumPayloadBytes,
        CaptureSampleRate = Math.Abs(message.CaptureSampleRate - 1d) < double.Epsilon
            ? null
            : message.CaptureSampleRate,
        SensitiveFields = message.SensitiveFields is { Length: > 0 } ? message.SensitiveFields : null
    };

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Normalize(string yaml) =>
        yaml.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";

    private sealed class CatalogSource
    {
        public int SchemaVersion { get; set; }
        public string CatalogId { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public int Revision { get; set; }
        public string DefaultCodec { get; set; } = string.Empty;
        public MessageSource[] Messages { get; set; } = Array.Empty<MessageSource>();
    }

    private sealed class MessageSource
    {
        public string Id { get; set; } = string.Empty;
        public uint OpCode { get; set; }
        public string Direction { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string PayloadType { get; set; } = string.Empty;
        public string? Codec { get; set; }
        public string? Reliability { get; set; }
        public string? Response { get; set; }
        public int? MinimumSchemaVersion { get; set; }
        public int? MaximumSchemaVersion { get; set; }
        public int? MaximumPayloadBytes { get; set; }
        public double? CaptureSampleRate { get; set; }
        public string[]? SensitiveFields { get; set; }
    }

    private sealed class WireSchemaSource
    {
        public int SchemaVersion { get; set; }
        public string? ProjectId { get; set; }
        public string? Namespace { get; set; }
        public string Type { get; set; } = string.Empty;
        public string? MemoryPackMode { get; set; }
        public string? Declaration { get; set; }
        public string? MemberStyle { get; set; }
        public WireFieldSource[] Fields { get; set; } = Array.Empty<WireFieldSource>();
        public uint[]? ReservedIds { get; set; }
    }

    private sealed class WireFieldSource
    {
        public uint Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ScalarType { get; set; }
        public string? Type { get; set; }
        public bool? Array { get; set; }
        public bool? Optional { get; set; }
        public bool? Required { get; set; }
    }
}
