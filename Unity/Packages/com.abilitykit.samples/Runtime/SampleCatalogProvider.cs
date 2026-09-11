#nullable enable

using System;
using System.Linq;
using System.Reflection;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic
{
    /// <summary>
    /// Builds the reusable sample catalog for console, UI, Unity, MonoGame, or custom hosts.
    /// </summary>
    public static class SampleCatalogProvider
    {
        public static SampleCatalog CreateCatalog()
        {
            return CreateCatalog(includeAttributeOnly: false, includeLegacy: false);
        }

        public static SampleCatalog CreateDevelopmentCatalog()
        {
            return CreateCatalog(includeAttributeOnly: true, includeLegacy: true);
        }

        public static SampleCatalog CreateCatalog(bool includeAttributeOnly, bool includeLegacy)
        {
            SampleRegistry.Instance.Initialize();

            var manifest = SampleManifest.LoadDefault();
            var catalog = new SampleCatalog();
            var sampleTypes = SampleRegistry.Instance.GetAllSampleTypes()
                .Select(type =>
                {
                    var attr = type.GetCustomAttribute<SampleAttribute>();
                    var item = manifest.Find(type);
                    return new
                    {
                        Type = type,
                        Attribute = attr,
                        Manifest = item,
                        Order = item?.Order ?? attr?.Priority ?? 100
                    };
                })
                .Where(x => ShouldInclude(x.Manifest, includeAttributeOnly, includeLegacy))
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Type.Name);

            foreach (var sample in sampleTypes)
            {
                catalog.Register(
                    sample.Type,
                    sample.Order,
                    sample.Manifest?.Tags?.Length > 0 ? sample.Manifest.Tags : sample.Attribute?.Tags ?? Array.Empty<string>(),
                    id: sample.Manifest?.Id,
                    title: sample.Manifest?.Title,
                    description: sample.Manifest?.Description,
                    status: sample.Manifest?.Status,
                    level: sample.Manifest?.Level,
                    modules: sample.Manifest?.Modules,
                    next: sample.Manifest?.Next,
                    guide: sample.Manifest?.Guide,
                    codeWalkthrough: sample.Manifest?.CodeWalkthrough,
                    learningContract: sample.Manifest?.LearningContract,
                    visualFrames: sample.Manifest?.VisualFrames,
                    inputFields: sample.Manifest?.InputFields,
                    learningCheckpoints: sample.Manifest?.LearningCheckpoints,
                    visualTemplate: sample.Manifest?.VisualTemplate,
                    visualModel: sample.Manifest?.VisualModel,
                    isManifestEntry: sample.Manifest != null);
            }

            return catalog;
        }

        private static bool ShouldInclude(SampleManifestItem? manifest, bool includeAttributeOnly, bool includeLegacy)
        {
            if (manifest == null)
                return includeAttributeOnly;

            if (IsStatus(manifest, "Stable") || IsStatus(manifest, "Candidate"))
                return true;

            return includeLegacy && (IsStatus(manifest, "Legacy") || IsStatus(manifest, "Deprecated"));
        }

        private static bool IsStatus(SampleManifestItem manifest, string status)
        {
            return string.Equals(manifest.Status, status, StringComparison.OrdinalIgnoreCase);
        }

    }
}
