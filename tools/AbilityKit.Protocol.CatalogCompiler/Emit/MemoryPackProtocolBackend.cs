using AbilityKit.Protocol.CatalogCompiler.Ir;

namespace AbilityKit.Protocol.CatalogCompiler.Emit;

/// <summary>Adapts the established MemoryPack emitters to the codec backend SPI.</summary>
public sealed class MemoryPackProtocolBackend : IProtocolBackend
{
    public string Codec => "memorypack";

    public ProtocolBackendCapabilities Capabilities =>
        ProtocolBackendCapabilities.StableFieldIds |
        ProtocolBackendCapabilities.OptionalFields |
        ProtocolBackendCapabilities.RepeatedFields |
        ProtocolBackendCapabilities.CustomTypes |
        ProtocolBackendCapabilities.RuntimeCodecFacade |
        ProtocolBackendCapabilities.DecoderRegistration;

    public IReadOnlyList<ProtocolBackendOutput> EmitSchema(ProtocolBackendSchemaContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var schema = context.Schema;
        return new[]
        {
            new ProtocolBackendOutput(
                schema.Type + ".MemoryPack.g.cs",
                MemoryPackWireEmitter.Emit(schema, context.ProtocolMessage))
        };
    }
}
