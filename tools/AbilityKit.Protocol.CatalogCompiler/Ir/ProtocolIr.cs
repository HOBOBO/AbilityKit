namespace AbilityKit.Protocol.CatalogCompiler.Ir;

/// <summary>
/// 目录编译管线共享、与源格式及编解码无关的常量。
/// </summary>
public static class ProtocolCatalogConstants
{
    /// <summary>
    /// 此编译器唯一接受和生成的目录源结构版本。
    /// </summary>
    public const int SchemaVersion = 1;

    /// <summary>
    /// 写入生成清单的生成器版本。
    /// </summary>
    public const string GeneratorVersion = "1.0";
}

/// <summary>
/// 协议消息的传输方向。复现运行时 <c>ProtocolDirection</c> 语义但不引用运行时程序集，保持中间表示与编解码无关。
/// </summary>
public enum IrDirection
{
    ClientToServer,
    ServerToClient,
    Bidirectional
}

/// <summary>
/// 协议消息的包种类。
/// </summary>
public enum IrPacketKind
{
    Request,
    Response,
    Push,
    Event
}

/// <summary>
/// 协议消息的交付可靠性。
/// </summary>
public enum IrReliability
{
    Reliable,
    Realtime
}

/// <summary>
/// 单条协议消息的编解码无关中间表示，携带目录声明的传输事实：身份、键、载荷引用、结构及载荷预算、采样与脱敏策略。编解码器仅表示为不透明名称。
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
/// 单个协议目录的编解码无关中间表示。目录按单个项目及业务域修订版组织消息，并指定默认编解码器名称。
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
