using System;
using System.Text;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaBattleDiagnosticJsonArtifactSerializer :
        IBattleDiagnosticArtifactSerializer
    {
        public const string JsonFormatId = "abilitykit-battle-diagnostics.json.v1";

        public string FormatId => JsonFormatId;
        public string MediaType => "application/json";

        public byte[] Serialize(BattleDiagnosticSessionSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return Encoding.UTF8.GetBytes(
                MobaBattleDiagnosticArtifactCodec.ExportSnapshotToString(snapshot));
        }

        public BattleDiagnosticSessionSnapshot Deserialize(ReadOnlySpan<byte> artifact)
        {
            if (artifact.IsEmpty) throw new MobaBattleDiagnosticArtifactException(
                "Artifact.Empty",
                "Analysis artifact payload is empty.");
            return MobaBattleDiagnosticArtifactCodec.ImportSnapshot(
                Encoding.UTF8.GetString(artifact));
        }
    }
}
