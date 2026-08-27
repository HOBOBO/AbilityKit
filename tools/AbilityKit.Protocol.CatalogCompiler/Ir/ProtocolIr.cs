namespace AbilityKit.Protocol.CatalogCompiler.Ir;

/// <summary>
/// Shared, source- and codec-independent constants for the catalog compiler pipeline.
/// </summary>
public static class ProtocolCatalogConstants
{
    /// <summary>The only catalog source schema version this compiler accepts and emits.</summary>
    public const int SchemaVersion = 1;

    /// <summary>Generator version stamped into the emitted manifest.</summary>
    public const string GeneratorVersion = "1.0";
}

/// <summary>
/// Wire direction of a protocol message. Mirrors the runtime <c>ProtocolDirection</c>
/// semantics without referencing the runtime assembly, keeping the IR codec-neutral.
/// </summary>
public enum IrDirection
{
    ClientToServer,
    ServerToClient,
    Bidirectional
}

/// <summary>Packet kind of a protocol message.</summary>
public enum IrPacketKind
{
    Request,
    Response,
    Push,
    Event
}

/// <summary>Delivery reliability of a protocol message.</summary>
public enum IrReliability
{
    Reliable,
    Realtime
}

/// <summary>
/// Codec-neutral intermediate representation of a single protocol message. It carries the
/// transport facts a catalog declares (identity, key, payload reference, schema and payload
/// budgets, sampling and redaction policy) with the codec represented only as an opaque name.
/// </summary>
public sealed class ProtocolMessageIr
{
    public ProtocolMessageIr(
        string id,
        uint opCode,
        IrDirection direction,
        IrPacketKind kind,
        string payloadType,
        string codec,
        IrReliability reliability,
        string? responseId,
        int minimumSchemaVersion,
        int maximumSchemaVersion,
        int maximumPayloadBytes,
        double captureSampleRate,
        IReadOnlyList<string>? sensitiveFields = null)
    {
        Id = id ?? string.Empty;
        OpCode = opCode;
        Direction = direction;
        Kind = kind;
        PayloadType = payloadType ?? string.Empty;
        Codec = codec ?? string.Empty;
        Reliability = reliability;
        ResponseId = responseId ?? string.Empty;
        MinimumSchemaVersion = minimumSchemaVersion;
        MaximumSchemaVersion = maximumSchemaVersion;
        MaximumPayloadBytes = maximumPayloadBytes;
        CaptureSampleRate = captureSampleRate;
        SensitiveFields = sensitiveFields ?? Array.Empty<string>();
    }

    public string Id { get; }
    public uint OpCode { get; }
    public IrDirection Direction { get; }
    public IrPacketKind Kind { get; }
    public string PayloadType { get; }
    public string Codec { get; }
    public IrReliability Reliability { get; }
    public string ResponseId { get; }
    public int MinimumSchemaVersion { get; }
    public int MaximumSchemaVersion { get; }
    public int MaximumPayloadBytes { get; }
    public double CaptureSampleRate { get; }
    public IReadOnlyList<string> SensitiveFields { get; }
}

/// <summary>
/// Codec-neutral intermediate representation of a single protocol catalog. One catalog groups
/// the messages a single project/domain revision defines and names the codec they default to.
/// </summary>
public sealed class ProtocolCatalogIr
{
    public ProtocolCatalogIr(
        string catalogId,
        string projectId,
        string domain,
        int revision,
        string defaultCodec,
        IReadOnlyList<ProtocolMessageIr> messages)
    {
        CatalogId = catalogId ?? string.Empty;
        ProjectId = projectId ?? string.Empty;
        Domain = domain ?? string.Empty;
        Revision = revision;
        DefaultCodec = defaultCodec ?? string.Empty;
        Messages = messages ?? Array.Empty<ProtocolMessageIr>();
    }

    public string CatalogId { get; }
    public string ProjectId { get; }
    public string Domain { get; }
    public int Revision { get; }
    public string DefaultCodec { get; }
    public IReadOnlyList<ProtocolMessageIr> Messages { get; }
}
