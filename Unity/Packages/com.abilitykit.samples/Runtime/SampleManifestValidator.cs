#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic
{
    /// <summary>
    /// Validates sample-manifest.json against scanned sample types.
    /// </summary>
    public static class SampleManifestValidator
    {
        public static SampleManifestValidationReport Validate()
        {
            SampleRegistry.Instance.Initialize();

            var manifest = SampleManifest.LoadDefault();
            var sampleTypes = SampleRegistry.Instance.GetAllSampleTypes();
            var sampleTypeNames = new HashSet<string>(sampleTypes.Select(x => x.FullName ?? x.Name), StringComparer.Ordinal);
            var formalManifestEntries = manifest.Samples.Where(IsFormalStatus).ToList();
            var catalog = SampleCatalogProvider.CreateCatalog();
            var report = new SampleManifestValidationReport
            {
                ManifestEntries = manifest.Samples.Count,
                ScannedSampleTypes = sampleTypes.Count,
                StableEntries = manifest.Samples.Count(IsStatusStable),
                CandidateEntries = manifest.Samples.Count(x => IsStatus(x, "Candidate")),
                LegacyEntries = manifest.Samples.Count(x => IsStatus(x, "Legacy")),
                DeprecatedEntries = manifest.Samples.Count(x => IsStatus(x, "Deprecated")),
                FormalManifestEntries = formalManifestEntries.Count,
                FormalCatalogEntries = catalog.Entries.Count,
                OutputSchemaVersion = SampleOutputContract.SchemaVersion
            };

            foreach (var group in manifest.Samples.Where(x => !string.IsNullOrWhiteSpace(x.Id)).GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase))
            {
                if (group.Count() > 1)
                    report.DuplicateIds.Add(group.Key);
            }

            foreach (var group in manifest.Samples.GroupBy(x => x.Order))
            {
                if (group.Count() > 1)
                    report.DuplicateOrders.Add($"{group.Key}: {string.Join(", ", group.Select(x => x.Id))}");
            }

            var manifestIds = new HashSet<string>(manifest.Samples.Select(x => x.Id).Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);
            foreach (var item in manifest.Samples)
            {
                ValidateManifestItem(item, sampleTypeNames, manifestIds, report);
            }

            ValidateCatalog(catalog, formalManifestEntries, report);
            ValidateOutputContract(report);

            foreach (var type in sampleTypes)
            {
                if (manifest.Find(type) == null)
                    report.AttributeOnlySamples.Add(type.FullName ?? type.Name);
            }

            return report;
        }

        private static void ValidateManifestItem(SampleManifestItem item, HashSet<string> sampleTypeNames, HashSet<string> manifestIds, SampleManifestValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
                report.MissingRequiredMetadata.Add($"{item.Type}: missing id");

            if (string.IsNullOrWhiteSpace(item.Type))
            {
                report.MissingRequiredMetadata.Add($"{item.Id}: missing type");
            }
            else if (!sampleTypeNames.Contains(item.Type))
            {
                report.MissingTypeEntries.Add($"{item.Id} -> {item.Type}");
            }

            if (string.IsNullOrWhiteSpace(item.Title))
                report.MissingRequiredMetadata.Add($"{item.Id}: missing title");

            if (string.IsNullOrWhiteSpace(item.Status))
                report.MissingRequiredMetadata.Add($"{item.Id}: missing status");
            else if (!IsKnownStatus(item))
                report.InvalidStatuses.Add($"{item.Id}: {item.Status}");

            if (string.IsNullOrWhiteSpace(item.Level))
                report.MissingRecommendedMetadata.Add($"{item.Id}: missing level");

            if (item.Modules.Length == 0)
                report.MissingRecommendedMetadata.Add($"{item.Id}: missing modules");

            if (item.Tags.Length == 0)
                report.MissingRequiredMetadata.Add($"{item.Id}: missing tags");

            if (HasTag(item, "web") && !HasTag(item, "deterministic"))
                report.WebEntriesWithoutDeterministicTag.Add(item.Id);

            ValidateGuideContent(item, report);
            ValidateCodeWalkthrough(item, report);
            ValidateLearningContract(item, report);
            ValidateLearningCheckpoints(item, report);
            ValidateVisualFrames(item, report);
            ValidateInputFields(item, report);
            ValidateVisualTemplate(item, report);
            ValidateVisualModel(item, report);

            foreach (var next in item.Next.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (!manifestIds.Contains(next))
                    report.MissingNextReferences.Add($"{item.Id} -> {next}");
            }
        }

        private static void ValidateGuideContent(SampleManifestItem item, SampleManifestValidationReport report)
        {
            var guide = item.Guide;
            var hasAnyGuide = !string.IsNullOrWhiteSpace(guide.Purpose) ||
                              !string.IsNullOrWhiteSpace(guide.Observe) ||
                              !string.IsNullOrWhiteSpace(guide.Takeaway) ||
                              !string.IsNullOrWhiteSpace(guide.VisualKind) ||
                              guide.VisualSteps.Length > 0;

            if (!hasAnyGuide)
                return;

            if (string.IsNullOrWhiteSpace(guide.Purpose))
                report.MissingRecommendedMetadata.Add($"{item.Id}: guide missing purpose");

            if (string.IsNullOrWhiteSpace(guide.Observe))
                report.MissingRecommendedMetadata.Add($"{item.Id}: guide missing observe");

            if (string.IsNullOrWhiteSpace(guide.Takeaway))
                report.MissingRecommendedMetadata.Add($"{item.Id}: guide missing takeaway");

            if (!string.IsNullOrWhiteSpace(guide.VisualKind) && guide.VisualSteps.Length < 2)
                report.MissingRecommendedMetadata.Add($"{item.Id}: guide visual requires at least two steps");
        }

        private static void ValidateCodeWalkthrough(SampleManifestItem item, SampleManifestValidationReport report)
        {
            for (var i = 0; i < item.CodeWalkthrough.Length; i++)
            {
                var step = item.CodeWalkthrough[i];
                var label = $"{item.Id}: codeWalkthrough[{i}]";

                if (string.IsNullOrWhiteSpace(step.Title))
                    report.MissingRecommendedMetadata.Add($"{label} missing title");

                if (string.IsNullOrWhiteSpace(step.SourceFile))
                    report.MissingRecommendedMetadata.Add($"{label} missing sourceFile");

                if (string.IsNullOrWhiteSpace(step.Explanation))
                    report.MissingRecommendedMetadata.Add($"{label} missing explanation");

                if (step.StartLine <= 0 || step.EndLine < step.StartLine)
                    report.MissingRecommendedMetadata.Add($"{label} invalid line range");
            }
        }

        private static void ValidateLearningContract(SampleManifestItem item, SampleManifestValidationReport report)
        {
            var contract = item.LearningContract;
            var hasAnyLearning = !string.IsNullOrWhiteSpace(contract.Summary) ||
                                 contract.Capabilities.Length > 0 ||
                                 contract.ApiHighlights.Length > 0 ||
                                 contract.Concepts.Length > 0 ||
                                 contract.InputHints.Length > 0 ||
                                 contract.OutputHints.Length > 0 ||
                                 !string.IsNullOrWhiteSpace(contract.ExecutionHint);

            if (!hasAnyLearning)
                return;

            if (string.IsNullOrWhiteSpace(contract.Summary))
                report.MissingRecommendedMetadata.Add($"{item.Id}: learningContract missing summary");

            if (contract.Capabilities.Length == 0)
                report.MissingRecommendedMetadata.Add($"{item.Id}: learningContract missing capabilities");

            if (contract.ApiHighlights.Length == 0)
                report.MissingRecommendedMetadata.Add($"{item.Id}: learningContract missing apiHighlights");

            if (contract.Concepts.Length == 0)
                report.MissingRecommendedMetadata.Add($"{item.Id}: learningContract missing concepts");

            if (contract.OutputHints.Length == 0)
                report.MissingRecommendedMetadata.Add($"{item.Id}: learningContract missing outputHints");
        }

        private static void ValidateLearningCheckpoints(SampleManifestItem item, SampleManifestValidationReport report)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < item.LearningCheckpoints.Length; i++)
            {
                var checkpoint = item.LearningCheckpoints[i];
                var label = $"{item.Id}: learningCheckpoints[{i}]";

                if (string.IsNullOrWhiteSpace(checkpoint.Id))
                    report.MissingRecommendedMetadata.Add($"{label} missing id");
                else if (!ids.Add(checkpoint.Id))
                    report.MissingRecommendedMetadata.Add($"{label} duplicate id: {checkpoint.Id}");

                if (string.IsNullOrWhiteSpace(checkpoint.Title))
                    report.MissingRecommendedMetadata.Add($"{label} missing title");

                if (string.IsNullOrWhiteSpace(checkpoint.Goal))
                    report.MissingRecommendedMetadata.Add($"{label} missing goal");

                if (string.IsNullOrWhiteSpace(checkpoint.Question))
                    report.MissingRecommendedMetadata.Add($"{label} missing question");

                if (string.IsNullOrWhiteSpace(checkpoint.ExpectedAnswer))
                    report.MissingRecommendedMetadata.Add($"{label} missing expectedAnswer");
            }
        }

        private static void ValidateVisualFrames(SampleManifestItem item, SampleManifestValidationReport report)
        {
            for (var i = 0; i < item.VisualFrames.Length; i++)
            {
                var frame = item.VisualFrames[i];
                var label = $"{item.Id}: visualFrames[{i}]";

                if (frame.Index < 0)
                    report.MissingRecommendedMetadata.Add($"{label} invalid index");

                if (string.IsNullOrWhiteSpace(frame.Title))
                    report.MissingRecommendedMetadata.Add($"{label} missing title");

                if (string.IsNullOrWhiteSpace(frame.VisualStep))
                    report.MissingRecommendedMetadata.Add($"{label} missing visualStep");

                if (string.IsNullOrWhiteSpace(frame.Description))
                    report.MissingRecommendedMetadata.Add($"{label} missing description");
            }
        }

        private static void ValidateInputFields(SampleManifestItem item, SampleManifestValidationReport report)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < item.InputFields.Length; i++)
            {
                var field = item.InputFields[i];
                var label = $"{item.Id}: inputFields[{i}]";

                if (string.IsNullOrWhiteSpace(field.Key))
                    report.MissingRecommendedMetadata.Add($"{label} missing key");
                else if (!keys.Add(field.Key))
                    report.MissingRecommendedMetadata.Add($"{label} duplicate key: {field.Key}");

                if (string.IsNullOrWhiteSpace(field.Label))
                    report.MissingRecommendedMetadata.Add($"{label} missing label");

                if (string.IsNullOrWhiteSpace(field.Type))
                    report.MissingRecommendedMetadata.Add($"{label} missing type");

                if (string.Equals(field.Type, "select", StringComparison.OrdinalIgnoreCase) && field.Options.Length == 0)
                    report.MissingRecommendedMetadata.Add($"{label} select input missing options");
            }
        }

        private static void ValidateVisualTemplate(SampleManifestItem item, SampleManifestValidationReport report)
        {
            if (item.VisualFrames.Length > 0 && string.IsNullOrWhiteSpace(item.VisualTemplate))
                report.MissingRecommendedMetadata.Add($"{item.Id}: visualTemplate missing for visualFrames");
        }

        private static void ValidateVisualModel(SampleManifestItem item, SampleManifestValidationReport report)
        {
            var model = item.VisualModel;
            var hasAnyModel = !string.IsNullOrWhiteSpace(model.Title) ||
                              !string.IsNullOrWhiteSpace(model.Description) ||
                              model.Nodes.Length > 0 ||
                              model.Edges.Length > 0 ||
                              model.Metrics.Length > 0;

            if (!hasAnyModel)
                return;

            var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < model.Nodes.Length; i++)
            {
                var node = model.Nodes[i];
                var label = $"{item.Id}: visualModel.nodes[{i}]";
                if (string.IsNullOrWhiteSpace(node.Id))
                    report.MissingRecommendedMetadata.Add($"{label} missing id");
                else if (!nodeIds.Add(node.Id))
                    report.MissingRecommendedMetadata.Add($"{label} duplicate id: {node.Id}");

                if (string.IsNullOrWhiteSpace(node.Label))
                    report.MissingRecommendedMetadata.Add($"{label} missing label");
            }

            for (var i = 0; i < model.Edges.Length; i++)
            {
                var edge = model.Edges[i];
                var label = $"{item.Id}: visualModel.edges[{i}]";
                if (string.IsNullOrWhiteSpace(edge.From) || !nodeIds.Contains(edge.From))
                    report.MissingRecommendedMetadata.Add($"{label} invalid from: {edge.From}");

                if (string.IsNullOrWhiteSpace(edge.To) || !nodeIds.Contains(edge.To))
                    report.MissingRecommendedMetadata.Add($"{label} invalid to: {edge.To}");
            }
        }

        private static void ValidateCatalog(SampleCatalog catalog, IReadOnlyCollection<SampleManifestItem> formalManifestEntries, SampleManifestValidationReport report)
        {
            var formalIds = new HashSet<string>(formalManifestEntries.Select(x => x.Id), StringComparer.OrdinalIgnoreCase);
            var catalogIds = new HashSet<string>(catalog.Entries.Select(x => x.Id), StringComparer.OrdinalIgnoreCase);

            foreach (var id in formalIds)
            {
                if (!catalogIds.Contains(id))
                    report.FormalManifestEntriesMissingFromCatalog.Add(id);
            }

            foreach (var entry in catalog.Entries)
            {
                if (!entry.IsManifestEntry)
                    report.CatalogEntriesNotManifestBacked.Add($"{entry.Id} -> {entry.SampleType.FullName ?? entry.SampleType.Name}");

                if (!formalIds.Contains(entry.Id))
                    report.NonFormalEntriesInDefaultCatalog.Add($"{entry.Id}: {entry.Status}");
            }
        }

        private static void ValidateOutputContract(SampleManifestValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(SampleOutputContract.SchemaVersion))
                report.OutputContractIssues.Add("missing output schema version");

            var entries = new BufferedSampleLogger();
            entries.Section("contract");
            entries.KeyValue("schema", SampleOutputContract.SchemaVersion);

            if (entries.Entries.Count != 2)
                report.OutputContractIssues.Add("buffered logger did not capture structured entries");

            if (entries.Entries.Count > 0 && entries.Entries[0].Sequence != 0)
                report.OutputContractIssues.Add("first structured output sequence must be 0");

            if (entries.Entries.Count > 1 && entries.Entries[1].Sequence != 1)
                report.OutputContractIssues.Add("structured output sequence must increment by 1");
        }

        private static bool IsStatusStable(SampleManifestItem item)
        {
            return IsStatus(item, "Stable");
        }

        private static bool IsFormalStatus(SampleManifestItem item)
        {
            return IsStatus(item, "Stable") || IsStatus(item, "Candidate");
        }

        private static bool IsKnownStatus(SampleManifestItem item)
        {
            return IsStatus(item, "Stable") ||
                   IsStatus(item, "Candidate") ||
                   IsStatus(item, "Legacy") ||
                   IsStatus(item, "Deprecated");
        }

        private static bool IsStatus(SampleManifestItem item, string status)
        {
            return string.Equals(item.Status, status, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasTag(SampleManifestItem item, string tag)
        {
            return item.Tags.Any(x => string.Equals(x, tag, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Validation result for sample-manifest.json.
    /// </summary>
    public sealed class SampleManifestValidationReport
    {
        public int ManifestEntries { get; init; }
        public int ScannedSampleTypes { get; init; }
        public int StableEntries { get; init; }
        public int CandidateEntries { get; init; }
        public int LegacyEntries { get; init; }
        public int DeprecatedEntries { get; init; }
        public int FormalManifestEntries { get; init; }
        public int FormalCatalogEntries { get; init; }
        public string OutputSchemaVersion { get; init; } = string.Empty;
        public List<string> AttributeOnlySamples { get; } = new();
        public List<string> MissingTypeEntries { get; } = new();
        public List<string> DuplicateIds { get; } = new();
        public List<string> DuplicateOrders { get; } = new();
        public List<string> MissingRequiredMetadata { get; } = new();
        public List<string> MissingRecommendedMetadata { get; } = new();
        public List<string> InvalidStatuses { get; } = new();
        public List<string> MissingNextReferences { get; } = new();
        public List<string> WebEntriesWithoutDeterministicTag { get; } = new();
        public List<string> FormalManifestEntriesMissingFromCatalog { get; } = new();
        public List<string> CatalogEntriesNotManifestBacked { get; } = new();
        public List<string> NonFormalEntriesInDefaultCatalog { get; } = new();
        public List<string> OutputContractIssues { get; } = new();

        public bool HasErrors =>
            MissingTypeEntries.Count > 0 ||
            DuplicateIds.Count > 0 ||
            DuplicateOrders.Count > 0 ||
            MissingRequiredMetadata.Count > 0 ||
            InvalidStatuses.Count > 0 ||
            MissingNextReferences.Count > 0 ||
            FormalManifestEntriesMissingFromCatalog.Count > 0 ||
            CatalogEntriesNotManifestBacked.Count > 0 ||
            NonFormalEntriesInDefaultCatalog.Count > 0 ||
            OutputContractIssues.Count > 0;

        public IReadOnlyList<string> ToLines()
        {
            var lines = new List<string>
            {
                "Sample Manifest Validation",
                $"- Manifest entries: {ManifestEntries}",
                $"- Scanned sample types: {ScannedSampleTypes}",
                $"- Stable entries: {StableEntries}",
                $"- Candidate entries: {CandidateEntries}",
                $"- Legacy entries: {LegacyEntries}",
                $"- Deprecated entries: {DeprecatedEntries}",
                $"- Formal manifest entries: {FormalManifestEntries}",
                $"- Formal catalog entries: {FormalCatalogEntries}",
                $"- Output schema version: {OutputSchemaVersion}",
                $"- Attribute-only samples: {AttributeOnlySamples.Count}",
                $"- Missing type entries: {MissingTypeEntries.Count}",
                $"- Duplicate ids: {DuplicateIds.Count}",
                $"- Duplicate orders: {DuplicateOrders.Count}",
                $"- Missing required metadata: {MissingRequiredMetadata.Count}",
                $"- Missing recommended metadata: {MissingRecommendedMetadata.Count}",
                $"- Invalid statuses: {InvalidStatuses.Count}",
                $"- Missing next references: {MissingNextReferences.Count}",
                $"- Web entries without deterministic tag: {WebEntriesWithoutDeterministicTag.Count}",
                $"- Formal entries missing from catalog: {FormalManifestEntriesMissingFromCatalog.Count}",
                $"- Catalog entries not manifest-backed: {CatalogEntriesNotManifestBacked.Count}",
                $"- Non-formal entries in default catalog: {NonFormalEntriesInDefaultCatalog.Count}",
                $"- Output contract issues: {OutputContractIssues.Count}"
            };

            AppendDetails(lines, "Attribute-only samples", AttributeOnlySamples);
            AppendDetails(lines, "Missing type entries", MissingTypeEntries);
            AppendDetails(lines, "Duplicate ids", DuplicateIds);
            AppendDetails(lines, "Duplicate orders", DuplicateOrders);
            AppendDetails(lines, "Missing required metadata", MissingRequiredMetadata);
            AppendDetails(lines, "Missing recommended metadata", MissingRecommendedMetadata);
            AppendDetails(lines, "Invalid statuses", InvalidStatuses);
            AppendDetails(lines, "Missing next references", MissingNextReferences);
            AppendDetails(lines, "Web entries without deterministic tag", WebEntriesWithoutDeterministicTag);
            AppendDetails(lines, "Formal entries missing from catalog", FormalManifestEntriesMissingFromCatalog);
            AppendDetails(lines, "Catalog entries not manifest-backed", CatalogEntriesNotManifestBacked);
            AppendDetails(lines, "Non-formal entries in default catalog", NonFormalEntriesInDefaultCatalog);
            AppendDetails(lines, "Output contract issues", OutputContractIssues);
            return lines;
        }

        private static void AppendDetails(List<string> lines, string title, IReadOnlyList<string> values)
        {
            if (values.Count == 0)
                return;

            lines.Add(string.Empty);
            lines.Add($"{title}:");
            foreach (var value in values.OrderBy(x => x, StringComparer.Ordinal))
            {
                lines.Add($"  - {value}");
            }
        }
    }
}
