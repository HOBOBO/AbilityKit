using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AbilityKit.Protocol.CatalogCompiler.Ir;

/// <summary>
/// 最小 YAML <see cref="IProtocolSourceParser"/> 实现。反序列化 <c>*.protocol.yaml</c> 文档并转换为编解码无关中间表示，在此应用各消息默认值（目录编解码器、结构版本、载荷预算、采样），使下游均收到完全解析的节点。
/// </summary>
public sealed class YamlProtocolSourceParser : IProtocolSourceParser
{
    public ProtocolCatalogIr Parse(string sourcePath, string sourceText)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var source = deserializer.Deserialize<CatalogSource>(sourceText)
            ?? throw new InvalidDataException($"Catalog '{sourcePath}' is empty.");
        if (source.SchemaVersion != ProtocolCatalogConstants.SchemaVersion)
            throw new InvalidDataException($"Catalog '{sourcePath}' uses unsupported schemaVersion {source.SchemaVersion}.");

        var defaultCodec = source.DefaultCodec?.Trim() ?? string.Empty;
        var messages = new List<ProtocolMessageIr>(source.Messages?.Count ?? 0);
        foreach (var item in source.Messages ?? Enumerable.Empty<MessageSource>())
        {
            messages.Add(new ProtocolMessageIr(
                item.Id?.Trim() ?? string.Empty,
                item.OpCode,
                ParseDirection(item.Direction, sourcePath, item.Id),
                ParseKind(item.Kind, sourcePath, item.Id),
                item.PayloadType?.Trim() ?? string.Empty,
                string.IsNullOrWhiteSpace(item.Codec) ? defaultCodec : item.Codec.Trim(),
                ParseReliability(item.Reliability, sourcePath, item.Id),
                item.Response?.Trim(),
                item.MinimumSchemaVersion ?? 1,
                item.MaximumSchemaVersion ?? item.MinimumSchemaVersion ?? 1,
                item.MaximumPayloadBytes ?? 1048576,
                item.CaptureSampleRate ?? 1d,
                item.SensitiveFields?.Select(value => value?.Trim() ?? string.Empty).ToArray()));
        }

        return new ProtocolCatalogIr(
            source.CatalogId?.Trim() ?? string.Empty,
            source.ProjectId?.Trim() ?? string.Empty,
            source.Domain?.Trim() ?? string.Empty,
            source.Revision,
            defaultCodec,
            messages);
    }

    private static IrDirection ParseDirection(string? value, string path, string? messageId) =>
        Normalize(value) switch
        {
            "c2s" or "clienttoserver" => IrDirection.ClientToServer,
            "s2c" or "servertoclient" => IrDirection.ServerToClient,
            "bidirectional" or "both" => IrDirection.Bidirectional,
            _ => throw InvalidValue("direction", value, path, messageId)
        };

    private static IrPacketKind ParseKind(string? value, string path, string? messageId) =>
        Normalize(value) switch
        {
            "request" => IrPacketKind.Request,
            "response" => IrPacketKind.Response,
            "push" => IrPacketKind.Push,
            "event" => IrPacketKind.Event,
            _ => throw InvalidValue("kind", value, path, messageId)
        };

    private static IrReliability ParseReliability(string? value, string path, string? messageId) =>
        Normalize(value ?? "reliable") switch
        {
            "reliable" => IrReliability.Reliable,
            "realtime" => IrReliability.Realtime,
            _ => throw InvalidValue("reliability", value, path, messageId)
        };

    private static InvalidDataException InvalidValue(string field, string? value, string path, string? messageId) =>
        new InvalidDataException($"Invalid {field} '{value}' in '{path}', message '{messageId}'.");

    private static string Normalize(string? value) =>
        (value ?? string.Empty).Replace("-", string.Empty, StringComparison.Ordinal).Trim().ToLowerInvariant();

    private sealed class CatalogSource
    {
        public int SchemaVersion { get; set; }
        public string? CatalogId { get; set; }
        public string? ProjectId { get; set; }
        public string? Domain { get; set; }
        public int Revision { get; set; }
        public string? DefaultCodec { get; set; }
        public List<MessageSource>? Messages { get; set; }
    }

    private sealed class MessageSource
    {
        public string? Id { get; set; }
        public uint OpCode { get; set; }
        public string? Direction { get; set; }
        public string? Kind { get; set; }
        public string? PayloadType { get; set; }
        public string? Codec { get; set; }
        public string? Reliability { get; set; }
        public string? Response { get; set; }
        public int? MinimumSchemaVersion { get; set; }
        public int? MaximumSchemaVersion { get; set; }
        public int? MaximumPayloadBytes { get; set; }
        public double? CaptureSampleRate { get; set; }
        public List<string?>? SensitiveFields { get; set; }
    }
}
