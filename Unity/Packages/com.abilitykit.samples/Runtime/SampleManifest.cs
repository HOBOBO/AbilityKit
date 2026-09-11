#nullable enable

using System;
using AbilityKit.Samples.Logic.Infrastructure.Config;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.Samples.Abstractions;
using Newtonsoft.Json;

namespace AbilityKit.Samples.Logic
{
    /// <summary>
    /// Manifest file that defines stable sample entries for hosts and validation tools.
    /// </summary>
    public sealed class SampleManifest
    {
        public List<SampleManifestItem> Samples { get; set; } = new();

        public SampleManifestItem? Find(Type type)
        {
            return Samples.FirstOrDefault(x =>
                string.Equals(x.Type, type.FullName, StringComparison.Ordinal) ||
                string.Equals(x.Type, type.AssemblyQualifiedName, StringComparison.Ordinal));
        }

        public static SampleManifest LoadDefault()
        {
            return Load(Path.Combine(AppContext.BaseDirectory, "sample-manifest.json"));
        }

        public static SampleManifest Load(string path)
        {
            if (!ResourceProviders.Current.TryLoadText(path, out var json))
                return new SampleManifest();

            try
            {
                // Newtonsoft matches property names case-insensitively by default.
                return JsonConvert.DeserializeObject<SampleManifest>(json) ?? new SampleManifest();
            }
            catch
            {
                return new SampleManifest();
            }
        }
    }

    /// <summary>
    /// Stable metadata for one sample entry.
    /// </summary>
    public sealed class SampleManifestItem
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Order { get; set; } = 100;
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string Status { get; set; } = "Candidate";
        public string Level { get; set; } = string.Empty;
        public string[] Modules { get; set; } = Array.Empty<string>();
        public string[] Tags { get; set; } = Array.Empty<string>();
        public string[] Next { get; set; } = Array.Empty<string>();
        public SampleGuideContent Guide { get; set; } = new();
        public SampleCodeWalkthroughStep[] CodeWalkthrough { get; set; } = Array.Empty<SampleCodeWalkthroughStep>();
        public SampleLearningContract LearningContract { get; set; } = new();
        public SampleVisualFrame[] VisualFrames { get; set; } = Array.Empty<SampleVisualFrame>();
        public SampleInputField[] InputFields { get; set; } = Array.Empty<SampleInputField>();
        public SampleLearningCheckpoint[] LearningCheckpoints { get; set; } = Array.Empty<SampleLearningCheckpoint>();
        public string VisualTemplate { get; set; } = "timeline";
        public SampleVisualModel VisualModel { get; set; } = new();
    }
}
