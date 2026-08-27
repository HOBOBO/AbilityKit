using System.Text.Json;
using System.Text.Json.Serialization;
using AbilityKit.Protocol.CatalogCompiler.Emit;
using AbilityKit.Protocol.CatalogCompiler.Ir;

namespace AbilityKit.Protocol.CatalogCompiler.Compatibility;

/// <summary>
/// The committed compatibility baseline: a full snapshot of every wire-relevant fact of the
/// protocol catalogs and wire schemas at the moment it was captured. The breaking diff compares
/// the current sources against this document, so it must record everything that classifies as a
/// contract fact and nothing that is presentation or tooling state.
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
/// Captures the compatibility baseline from the compiler IR and (de)serializes the baseline
/// artifact. Serialization is byte-stable for a given IR (fixed formatting, camelCase, ordinal
/// ordering inherited from the caller's sorted sources), matching the manifest emitter contract.
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
            WireSchemas = wireSchemas.Select(ToBaselineWireSchema).ToList()
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
