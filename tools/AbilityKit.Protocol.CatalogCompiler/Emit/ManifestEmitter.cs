using System.Text.Json;
using System.Text.Json.Serialization;
using AbilityKit.Protocol.CatalogCompiler.Ir;

namespace AbilityKit.Protocol.CatalogCompiler.Emit;

/// <summary>
/// Emits the JSON protocol manifest from the codec-neutral IR. Output formatting is fixed so the
/// emitted document is byte-stable for a given IR (and therefore deterministic for a given set of
/// source files in their sorted order).
/// </summary>
public static class ManifestEmitter
{
    public static string Emit(IReadOnlyList<ProtocolCatalogIr> catalogs, IReadOnlyList<string> sources)
    {
        var document = new ManifestDocument
        {
            SchemaVersion = ProtocolCatalogConstants.SchemaVersion,
            GeneratorVersion = ProtocolCatalogConstants.GeneratorVersion,
            Sources = sources,
            Catalogs = catalogs.Select(ToManifest).ToArray()
        };

        return JsonSerializer.Serialize(document, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }) + Environment.NewLine;
    }

    private static ManifestCatalog ToManifest(ProtocolCatalogIr catalog) =>
        new()
        {
            CatalogId = catalog.CatalogId,
            ProjectId = catalog.ProjectId,
            Domain = catalog.Domain,
            Revision = catalog.Revision,
            DefaultCodec = catalog.DefaultCodec,
            Messages = catalog.Messages.Select(message => new ManifestMessage
            {
                Id = message.Id,
                OpCode = message.OpCode,
                Direction = FormatDirection(message.Direction),
                Kind = message.Kind.ToString().ToLowerInvariant(),
                PayloadType = message.PayloadType,
                Codec = message.Codec,
                Reliability = message.Reliability.ToString().ToLowerInvariant(),
                Response = string.IsNullOrEmpty(message.ResponseId) ? null : message.ResponseId,
                MinimumSchemaVersion = message.MinimumSchemaVersion,
                MaximumSchemaVersion = message.MaximumSchemaVersion,
                MaximumPayloadBytes = message.MaximumPayloadBytes,
                CaptureSampleRate = message.CaptureSampleRate,
                SensitiveFields = message.SensitiveFields.Count == 0 ? null : message.SensitiveFields
            }).ToArray()
        };

    private static string FormatDirection(IrDirection direction) =>
        direction switch
        {
            IrDirection.ClientToServer => "c2s",
            IrDirection.ServerToClient => "s2c",
            _ => "bidirectional"
        };

    private sealed class ManifestDocument
    {
        public int SchemaVersion { get; set; }
        public string GeneratorVersion { get; set; } = string.Empty;
        public IReadOnlyList<string> Sources { get; set; } = Array.Empty<string>();
        public IReadOnlyList<ManifestCatalog> Catalogs { get; set; } = Array.Empty<ManifestCatalog>();
    }

    private sealed class ManifestCatalog
    {
        public string CatalogId { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public int Revision { get; set; }
        public string DefaultCodec { get; set; } = string.Empty;
        public IReadOnlyList<ManifestMessage> Messages { get; set; } = Array.Empty<ManifestMessage>();
    }

    private sealed class ManifestMessage
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
        public IReadOnlyList<string>? SensitiveFields { get; set; }
    }
}
