using AbilityKit.Protocol.CatalogCompiler.Ir;

namespace AbilityKit.Protocol.CatalogCompiler.Emit;

/// <summary>Capabilities declared by a protocol codec backend.</summary>
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

/// <summary>Context supplied when a backend emits one codec-specific wire contract.</summary>
public sealed record ProtocolBackendSchemaContext(
    WireSchemaIr Schema,
    ProtocolMessageIr? ProtocolMessage);

/// <summary>A deterministic file emitted by a codec backend.</summary>
public sealed record ProtocolBackendOutput(string FileName, string Content);

/// <summary>
/// Codec backend service-provider interface. Implementations consume codec-neutral IR and return
/// deterministic files without introducing their runtime dependency into the catalog emitter.
/// </summary>
public interface IProtocolBackend
{
    string Codec { get; }
    ProtocolBackendCapabilities Capabilities { get; }

    IReadOnlyList<ProtocolBackendOutput> EmitSchema(ProtocolBackendSchemaContext context);
}

/// <summary>Resolves protocol backends by their catalog codec name.</summary>
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
