using AbilityKit.Protocol.CatalogCompiler.Ir;

namespace AbilityKit.Protocol.CatalogCompiler.Emit;

/// <summary>
/// 协议编解码后端声明的能力。
/// </summary>
[Flags]
public enum ProtocolBackendCapabilities
{
    None = 0,
    StableFieldIds = 1 << 0,
    ReservedFieldIds = 1 << 1,
    OptionalFields = 1 << 2,
    RepeatedFields = 1 << 3,
    CustomTypes = 1 << 4,
    RuntimeCodecFacade = 1 << 5,
    DecoderRegistration = 1 << 6
}

/// <summary>
/// 后端生成单个特定编解码线协议契约时收到的上下文。
/// </summary>
public sealed record ProtocolBackendSchemaContext(
    WireSchemaIr Schema,
    ProtocolMessageIr? ProtocolMessage);

/// <summary>
/// 编解码后端生成的确定性文件。
/// </summary>
public sealed record ProtocolBackendOutput(string FileName, string Content);

/// <summary>
/// 编解码后端服务提供接口。实现消费编解码无关中间表示并返回确定文件，不向目录生成器引入其运行时依赖。
/// </summary>
public interface IProtocolBackend
{
    string Codec { get; }
    ProtocolBackendCapabilities Capabilities { get; }

    IReadOnlyList<ProtocolBackendOutput> EmitSchema(ProtocolBackendSchemaContext context);
}

/// <summary>
/// 按目录中的编解码器名称解析协议后端。
/// </summary>
public sealed class ProtocolBackendRegistry
{
    private readonly IReadOnlyDictionary<string, IProtocolBackend> _backends;

    public ProtocolBackendRegistry(IEnumerable<IProtocolBackend> backends)
    {
        ArgumentNullException.ThrowIfNull(backends);
        var index = new Dictionary<string, IProtocolBackend>(StringComparer.OrdinalIgnoreCase);
        foreach (var backend in backends)
        {
            ArgumentNullException.ThrowIfNull(backend);
            if (string.IsNullOrWhiteSpace(backend.Codec))
                throw new InvalidDataException("A protocol backend must declare a codec name.");
            if (!index.TryAdd(backend.Codec, backend))
                throw new InvalidDataException($"Protocol backend codec '{backend.Codec}' is registered more than once.");
        }

        _backends = index;
    }

    public IReadOnlyCollection<string> Codecs => _backends.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();

    public bool TryResolve(string codec, out IProtocolBackend? backend) =>
        _backends.TryGetValue(codec ?? string.Empty, out backend);

    public IProtocolBackend Resolve(string codec) =>
        TryResolve(codec, out var backend)
            ? backend!
            : throw new KeyNotFoundException($"No protocol backend is registered for codec '{codec}'.");
}
