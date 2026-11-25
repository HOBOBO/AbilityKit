using System.Text.Json;
using System.Text.Json.Serialization;
using AbilityKit.Protocol.CatalogCompiler.Emit;
using AbilityKit.Protocol.CatalogCompiler.Ir;

namespace AbilityKit.Protocol.CatalogCompiler.Compatibility;

/// <summary>
/// 已入库的兼容性基线，完整保存捕获时协议目录及线协议结构中与传输相关的契约事实。破坏性差异检查将当前源与该基线比较，因此必须记录全部契约事实，不包含表现或工具状态。
/// </summary>
public sealed class ProtocolCompatibilityBaselineDocument
{
    public int SchemaVersion { get; set; }
    public string GeneratorVersion { get; set; } = string.Empty;
    public List<ProtocolCompatibilityBaselineCatalog> Catalogs { get; set; } = new();
    public List<ProtocolCompatibilityBaselineWireSchema> WireSchemas { get; set; } = new();
}

public sealed class ProtocolCompatibilityBaselineCatalog
{
    public string CatalogId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public int Revision { get; set; }
    public string DefaultCodec { get; set; } = string.Empty;
    public List<ProtocolCompatibilityBaselineMessage> Messages { get; set; } = new();
}

public sealed class ProtocolCompatibilityBaselineMessage
{
    public string Id { get; set; } = string.Empty;
    public uint OpCode { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string PayloadType { get; set; } = string.Empty;
    public string Codec { get; set; } = string.Empty;
    public string Reliability { get; set; } = string.Empty;
    public string? Response { get; set; }
    public int MinimumSchemaVersion { get; set; }
    public int MaximumSchemaVersion { get; set; }
    public int MaximumPayloadBytes { get; set; }
    public double CaptureSampleRate { get; set; }
    public List<string>? SensitiveFields { get; set; }
}

public sealed class ProtocolCompatibilityBaselineWireSchema
{
    public string QualifiedType { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string MemoryPackMode { get; set; } = string.Empty;
    public string DeclarationKind { get; set; } = string.Empty;
    public string MemberStyle { get; set; } = string.Empty;
    public List<ProtocolCompatibilityBaselineWireField> Fields { get; set; } = new();
    public List<uint> ReservedIds { get; set; } = new();
}

public sealed class ProtocolCompatibilityBaselineWireField
{
    public uint Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ScalarType { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public bool IsArray { get; set; }
    public bool IsOptional { get; set; }
}

/// <summary>
/// 从编译器中间表示捕获兼容性基线，并序列化或反序列化基线产物。相同中间表示生成稳定字节：固定格式、camelCase，并按完整类型名排序线协议类型，不依赖源文件分组。
/// </summary>
public static class ProtocolCompatibilityBaseline
{
    public const string ArtifactFileName = "protocol-compatibility-baseline.json";

    private static readonly JsonSerializerOptions SerializationOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static ProtocolCompatibilityBaselineDocument Capture(
        IReadOnlyList<ProtocolCatalogIr> catalogs,
        IReadOnlyList<WireSchemaIr> wireSchemas)
    {
        ArgumentNullException.ThrowIfNull(catalogs);
        ArgumentNullException.ThrowIfNull(wireSchemas);

        return new ProtocolCompatibilityBaselineDocument
        {
            SchemaVersion = ProtocolCatalogConstants.SchemaVersion,
            GeneratorVersion = ProtocolCatalogConstants.GeneratorVersion,
            Catalogs = catalogs.Select(ToBaselineCatalog).ToList(),
            WireSchemas = wireSchemas
                .OrderBy(MemoryPackExportPlanner.QualifiedType, StringComparer.Ordinal)
                .Select(ToBaselineWireSchema)
                .ToList()
        };
    }

    public static string Serialize(ProtocolCompatibilityBaselineDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.Serialize(document, SerializationOptions) + Environment.NewLine;
    }

    public static ProtocolCompatibilityBaselineDocument Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidDataException("Compatibility baseline artifact is empty.");

        var document = JsonSerializer.Deserialize<ProtocolCompatibilityBaselineDocument>(json, SerializationOptions);
        if (document == null)
            throw new InvalidDataException("Compatibility baseline artifact is not a valid JSON object.");

        if (document.SchemaVersion != ProtocolCatalogConstants.SchemaVersion)
            throw new InvalidDataException(
                $"Compatibility baseline schema version {document.SchemaVersion} is not supported " +
                $"(expected {ProtocolCatalogConstants.SchemaVersion}).");

        return document;
    }

    private static ProtocolCompatibilityBaselineCatalog ToBaselineCatalog(ProtocolCatalogIr catalog) => new()
    {
        CatalogId = catalog.CatalogId,
        ProjectId = catalog.ProjectId,
        Domain = catalog.Domain,
        Revision = catalog.Revision,
        DefaultCodec = catalog.DefaultCodec,
        Messages = catalog.Messages.Select(ToBaselineMessage).ToList()
    };

    private static ProtocolCompatibilityBaselineMessage ToBaselineMessage(ProtocolMessageIr message) => new()
    {
        Id = message.Id,
        OpCode = message.OpCode,
        Direction = ProtocolCompatibilityNames.Direction(message.Direction),
        Kind = ProtocolCompatibilityNames.Kind(message.Kind),
        PayloadType = message.PayloadType,
        Codec = message.Codec,
        Reliability = ProtocolCompatibilityNames.Reliability(message.Reliability),
        Response = string.IsNullOrEmpty(message.ResponseId) ? null : message.ResponseId,
        MinimumSchemaVersion = message.MinimumSchemaVersion,
        MaximumSchemaVersion = message.MaximumSchemaVersion,
        MaximumPayloadBytes = message.MaximumPayloadBytes,
        CaptureSampleRate = message.CaptureSampleRate,
        SensitiveFields = message.SensitiveFields.Count == 0 ? null : message.SensitiveFields.ToList()
    };

    private static ProtocolCompatibilityBaselineWireSchema ToBaselineWireSchema(WireSchemaIr schema) => new()
    {
        QualifiedType = MemoryPackExportPlanner.QualifiedType(schema),
        ProjectId = schema.ProjectId,
        MemoryPackMode = ProtocolCompatibilityNames.MemoryPackMode(schema.MemoryPackMode),
        DeclarationKind = ProtocolCompatibilityNames.Declaration(schema.DeclarationKind),
        MemberStyle = ProtocolCompatibilityNames.MemberStyle(schema.MemberStyle),
        Fields = schema.Fields.Select(ToBaselineWireField).ToList(),
        ReservedIds = schema.ReservedIds.ToList()
    };

    private static ProtocolCompatibilityBaselineWireField ToBaselineWireField(WireFieldIr field) => new()
    {
        Id = field.Id,
        Name = field.Name,
        ScalarType = field.ScalarType,
        TypeName = field.TypeName,
        IsArray = field.IsArray,
        IsOptional = field.IsOptional
    };
}
