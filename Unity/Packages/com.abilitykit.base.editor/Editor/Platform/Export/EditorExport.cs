#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace AbilityKit.Editor.Platform.Export
{
    public enum EditorExportStatus
    {
        Exported = 0,
        Unchanged = 1,
        Skipped = 2,
        Failed = 3
    }

    public sealed class EditorExportArtifact
    {
        public EditorExportArtifact(string path, string format = null)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Export artifact path must not be empty.", nameof(path));

            Path = path;
            Format = string.IsNullOrWhiteSpace(format) ? string.Empty : format;
        }

        public string Path { get; }
        public string Format { get; }
    }

    public sealed class EditorExportReportEntry
    {
        private readonly IReadOnlyList<EditorExportArtifact> _artifacts;
        private readonly IReadOnlyList<string> _messages;

        public EditorExportReportEntry(
            string jobId,
            string target,
            EditorExportStatus status,
            IEnumerable<EditorExportArtifact> artifacts = null,
            IEnumerable<string> messages = null)
        {
            if (string.IsNullOrWhiteSpace(jobId))
                throw new ArgumentException("Export job id must not be empty.", nameof(jobId));

            JobId = jobId;
            Target = string.IsNullOrWhiteSpace(target) ? jobId : target;
            Status = status;
            _artifacts = CopyArtifacts(artifacts);
            _messages = CopyMessages(messages);
        }

        public string JobId { get; }
        public string Target { get; }
        public EditorExportStatus Status { get; }
        public IReadOnlyList<EditorExportArtifact> Artifacts => _artifacts;
        public IReadOnlyList<string> Messages => _messages;
        public bool Success => Status != EditorExportStatus.Failed;

        public static EditorExportReportEntry Failed(
            string jobId,
            string target,
            params string[] messages)
        {
            return new EditorExportReportEntry(
                jobId,
                target,
                EditorExportStatus.Failed,
                messages: messages);
        }

        private static IReadOnlyList<EditorExportArtifact> CopyArtifacts(
            IEnumerable<EditorExportArtifact> artifacts)
        {
            if (artifacts == null)
                return Array.Empty<EditorExportArtifact>();

            var values = artifacts.ToArray();
            if (values.Any(value => value == null))
                throw new ArgumentException("Export artifacts must not contain null values.", nameof(artifacts));
            return values;
        }

        private static IReadOnlyList<string> CopyMessages(IEnumerable<string> messages)
        {
            return messages == null
                ? Array.Empty<string>()
                : messages.Where(message => !string.IsNullOrWhiteSpace(message)).ToArray();
        }
    }

    public sealed class EditorExportReport
    {
        private readonly IReadOnlyList<EditorExportReportEntry> _entries;

        public EditorExportReport(IEnumerable<EditorExportReportEntry> entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            var values = entries.ToArray();
            if (values.Any(value => value == null))
                throw new ArgumentException("Export report entries must not contain null values.", nameof(entries));
            _entries = values;
        }

        public IReadOnlyList<EditorExportReportEntry> Entries => _entries;
        public bool Success => _entries.Count > 0 && !HasFailures;
        public bool HasFailures => FailedCount > 0;
        public int ExportedCount => Count(EditorExportStatus.Exported);
        public int UnchangedCount => Count(EditorExportStatus.Unchanged);
        public int SkippedCount => Count(EditorExportStatus.Skipped);
        public int FailedCount => Count(EditorExportStatus.Failed);
        public IEnumerable<EditorExportArtifact> Artifacts =>
            _entries.SelectMany(entry => entry.Artifacts);
        public IEnumerable<string> Messages =>
            _entries.SelectMany(entry => entry.Messages);

        public static EditorExportReport Single(EditorExportReportEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            return new EditorExportReport(new[] { entry });
        }

        private int Count(EditorExportStatus status)
        {
            return _entries.Count(entry => entry.Status == status);
        }
    }

    public sealed class EditorExportJob
    {
        private readonly Func<EditorExportReportEntry> _execute;

        public EditorExportJob(
            string id,
            string target,
            string format,
            Func<EditorExportReportEntry> execute)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Export job id must not be empty.", nameof(id));

            Id = id;
            Target = string.IsNullOrWhiteSpace(target) ? id : target;
            Format = string.IsNullOrWhiteSpace(format) ? string.Empty : format;
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public string Id { get; }
        public string Target { get; }
        public string Format { get; }

        public EditorExportReportEntry Execute()
        {
            try
            {
                return _execute() ?? EditorExportReportEntry.Failed(
                    Id,
                    Target,
                    "The export job returned no result.");
            }
            catch (Exception ex)
            {
                return EditorExportReportEntry.Failed(Id, Target, ex.Message);
            }
        }
    }

    public static class EditorExportExecutor
    {
        public static EditorExportReport Execute(IEnumerable<EditorExportJob> jobs)
        {
            if (jobs == null)
                throw new ArgumentNullException(nameof(jobs));

            return new EditorExportReport(jobs.Select(job =>
            {
                if (job == null)
                    throw new ArgumentException("Export jobs must not contain null values.", nameof(jobs));
                return job.Execute();
            }));
        }
    }
}
#endif
