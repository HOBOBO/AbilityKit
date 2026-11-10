using System;
using System.Globalization;
using System.IO;
using System.Text;
using AbilityKit.Diagnostics.Analysis;
using Newtonsoft.Json;

namespace AbilityKit.Diagnostics.Exporters
{
    /// <summary>
    /// Exports the stable Web-facing AbilityKit analysis artifact as JSON.
    /// </summary>
    public interface IAnalysisArtifactExporter
    {
        string Extension { get; }
        void Export(AbilityKitAnalysisArtifact artifact, string filePath);
        string ExportToString(AbilityKitAnalysisArtifact artifact);
    }

    public sealed class AnalysisArtifactJsonExporter : IAnalysisArtifactExporter
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Include
        };

        public string Extension => ".analysis.json";

        public void Export(AbilityKitAnalysisArtifact artifact, string filePath)
        {
            var value = PrepareArtifact(artifact);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var textWriter = new StreamWriter(filePath, false, new UTF8Encoding(false), 4096))
            using (var jsonWriter = new JsonTextWriter(textWriter)
            {
                Formatting = Formatting.Indented,
                Culture = CultureInfo.InvariantCulture,
                CloseOutput = false
            })
            {
                JsonSerializer.Create(Settings).Serialize(jsonWriter, value);
            }
        }

        public string ExportToString(AbilityKitAnalysisArtifact artifact)
        {
            return JsonConvert.SerializeObject(PrepareArtifact(artifact), Settings);
        }

        private static AbilityKitAnalysisArtifact PrepareArtifact(AbilityKitAnalysisArtifact artifact)
        {
            if (artifact == null) throw new ArgumentNullException(nameof(artifact));
            if (string.IsNullOrEmpty(artifact.SchemaVersion))
            {
                artifact.SchemaVersion = AbilityKitAnalysisSchema.Version;
            }

            return artifact;
        }
    }
}
