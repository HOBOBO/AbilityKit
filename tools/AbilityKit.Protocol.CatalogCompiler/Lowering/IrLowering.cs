using AbilityKit.Protocol;
using AbilityKit.Protocol.Catalog;
using AbilityKit.Protocol.CatalogCompiler.Ir;

namespace AbilityKit.Protocol.CatalogCompiler.Lowering;

/// <summary>
/// Lowers the codec-neutral IR into the runtime <see cref="ProtocolCatalogDefinition"/> model.
/// This is the only place the compiler references the runtime catalog types; it exists so the
/// authoritative runtime validator (<c>ProtocolCatalogValidator</c>) can run against the exact
/// structures the generated C# will materialize, while emission stays on the IR.
/// </summary>
public static class IrLowering
{
    public static IReadOnlyList<ProtocolCatalogDefinition> ToRuntime(IReadOnlyList<ProtocolCatalogIr> catalogs)
    {
        var result = new ProtocolCatalogDefinition[catalogs.Count];
        for (var i = 0; i < catalogs.Count; i++)
            result[i] = ToCatalog(catalogs[i]);
        return result;
    }

    public static ProtocolCatalogDefinition ToCatalog(ProtocolCatalogIr catalog)
    {
        var messages = new ProtocolMessageDefinition[catalog.Messages.Count];
        for (var i = 0; i < catalog.Messages.Count; i++)
            messages[i] = ToMessage(catalog.Messages[i]);

        return new ProtocolCatalogDefinition(
            catalog.CatalogId,
            catalog.ProjectId,
            catalog.Domain,
            catalog.Revision,
            catalog.DefaultCodec,
            messages);
    }

    private static ProtocolMessageDefinition ToMessage(ProtocolMessageIr message) =>
        new(
            message.Id,
            message.OpCode,
            ToDirection(message.Direction),
            ToKind(message.Kind),
            message.PayloadType,
            message.Codec,
            ToReliability(message.Reliability),
            message.ResponseId,
            message.MinimumSchemaVersion,
            message.MaximumSchemaVersion,
            message.MaximumPayloadBytes,
            message.CaptureSampleRate,
            message.SensitiveFields);

    private static ProtocolDirection ToDirection(IrDirection direction) =>
        direction switch
        {
            IrDirection.ClientToServer => ProtocolDirection.ClientToServer,
            IrDirection.ServerToClient => ProtocolDirection.ServerToClient,
            _ => ProtocolDirection.Bidirectional
        };

    private static ProtocolPacketKind ToKind(IrPacketKind kind) =>
        kind switch
        {
            IrPacketKind.Request => ProtocolPacketKind.Request,
            IrPacketKind.Response => ProtocolPacketKind.Response,
            IrPacketKind.Push => ProtocolPacketKind.Push,
            _ => ProtocolPacketKind.Event
        };

    private static ProtocolReliability ToReliability(IrReliability reliability) =>
        reliability switch
        {
            IrReliability.Realtime => ProtocolReliability.Realtime,
            _ => ProtocolReliability.Reliable
        };
}
