using System;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    public interface IBattleDiagnosticArtifactSerializer
    {
        string FormatId { get; }
        string MediaType { get; }

        byte[] Serialize(BattleDiagnosticSessionSnapshot snapshot);

        BattleDiagnosticSessionSnapshot Deserialize(ReadOnlySpan<byte> artifact);
    }

    public interface IBattleDiagnosticDefinitionCatalogSerializer
    {
        string FormatId { get; }
        string MediaType { get; }

        byte[] Serialize(BattleDiagnosticDefinitionCatalogSnapshot catalog);

        BattleDiagnosticDefinitionCatalogSnapshot Deserialize(ReadOnlySpan<byte> artifact);
    }
}
