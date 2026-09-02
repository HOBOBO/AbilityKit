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

    public static string EmitWireSchemaDocument(WireSchemaDocumentIr document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.SchemaVersion != WireSchemaFormatVersions.Current)
            throw new ArgumentException(
                $"Only schemaVersion {WireSchemaFormatVersions.Current} wire documents can be emitted.",
                nameof(document));
        if (document.Schemas.Count == 0)
            throw new InvalidDataException("A wire schema document must contain at least one type.");
        foreach (var schema in document.Schemas)
        {
            if (!string.Equals(schema.ProjectId, document.ProjectId, StringComparison.Ordinal) ||
                !string.Equals(schema.TargetNamespace, document.TargetNamespace, StringComparison.Ordinal) ||
                !string.Equals(schema.GroupId, document.GroupId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Wire type '{schema.Type}' ownership does not match its document projectId, groupId and namespace.");
            }
        }
        var duplicateType = document.Schemas
            .GroupBy(schema => schema.Type, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateType != null)
            throw new InvalidDataException($"Wire document contains duplicate type '{duplicateType.Key}'.");

        var defaults = new WireDefaultsSource
        {
            MemoryPackMode = document.DefaultMemoryPackMode == WireMemoryPackMode.VersionTolerant
                ? null
                : FormatMemoryPackMode(document.DefaultMemoryPackMode),
            Declaration = document.DefaultDeclarationKind == WireDeclarationKind.Class
                ? null
                : FormatDeclaration(document.DefaultDeclarationKind),
            MemberStyle = document.DefaultMemberStyle == WireMemberStyle.Property
                ? null
                : FormatMemberStyle(document.DefaultMemberStyle)
        };
        var source = new WireSchemaGroupSource
        {
            SchemaVersion = WireSchemaFormatVersions.Current,
            ProjectId = document.ProjectId,
            GroupId = document.GroupId,
            Namespace = document.TargetNamespace,
            Defaults = IsEmpty(defaults) ? null : defaults,
            Types = document.Schemas.Select(schema => new WireTypeSource
            {
                Name = schema.Type,
                MemoryPackMode = schema.MemoryPackMode == document.DefaultMemoryPackMode
                    ? null
                    : FormatMemoryPackMode(schema.MemoryPackMode),
                Declaration = schema.DeclarationKind == document.DefaultDeclarationKind
                    ? null
                    : FormatDeclaration(schema.DeclarationKind),
                MemberStyle = schema.MemberStyle == document.DefaultMemberStyle
                    ? null
                    : FormatMemberStyle(schema.MemberStyle),
                Fields = ToFields(schema.Fields),
                ReservedIds = schema.ReservedIds.Count > 0 ? schema.ReservedIds.ToArray() : null
            }).ToArray()
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

    private static WireFieldSource[] ToFields(IReadOnlyList<WireFieldIr> fields) =>
        fields.Select(field => new WireFieldSource
        {
            Id = field.Id,
            Name = field.Name,
            ScalarType = field.IsCustomType ? null : field.ScalarType,
            Type = field.IsCustomType ? field.TypeName : null,
            External = field.IsExternalReference ? true : null,
            Array = field.IsArray ? true : null,
            Optional = field.IsOptional ? true : null,
            Required = field.IsOptional ? null : true
        }).ToArray();

    private static string FormatMemoryPackMode(WireMemoryPackMode value) =>
        value == WireMemoryPackMode.Sequential ? "sequential" : "version-tolerant";

    private static string FormatDeclaration(WireDeclarationKind value) =>
        value == WireDeclarationKind.Struct ? "struct" : "class";

    private static string FormatMemberStyle(WireMemberStyle value) =>
        value == WireMemberStyle.Field ? "field" : "property";

    private static bool IsEmpty(WireDefaultsSource defaults) =>
        defaults.MemoryPackMode == null && defaults.Declaration == null && defaults.MemberStyle == null;

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

    private sealed class WireSchemaGroupSource
    {
        public int SchemaVersion { get; set; }
        public string ProjectId { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public WireDefaultsSource? Defaults { get; set; }
        public WireTypeSource[] Types { get; set; } = Array.Empty<WireTypeSource>();
    }

    private sealed class WireDefaultsSource
    {
        public string? MemoryPackMode { get; set; }
        public string? Declaration { get; set; }
        public string? MemberStyle { get; set; }
    }

    private sealed class WireTypeSource
    {
        public string Name { get; set; } = string.Empty;
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
        public bool? External { get; set; }
        public bool? Array { get; set; }
        public bool? Optional { get; set; }
        public bool? Required { get; set; }
    }
}
