using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Services
{
    public readonly struct MobaOptionalHealthFinding
    {
        public MobaOptionalHealthFinding(
            MobaRuntimeValidationSeverity severity,
            string path,
            string message,
            string code)
        {
            Severity = severity;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
            Code = code ?? string.Empty;
        }

        public MobaRuntimeValidationSeverity Severity { get; }
        public string Path { get; }
        public string Message { get; }
        public string Code { get; }
    }

    public readonly struct MobaOptionalHealthContribution
    {
        private static readonly IReadOnlyDictionary<string, double> EmptyMetrics =
            new Dictionary<string, double>();
        private static readonly IReadOnlyList<MobaOptionalHealthFinding> EmptyFindings =
            Array.Empty<MobaOptionalHealthFinding>();

        public MobaOptionalHealthContribution(
            string source,
            IReadOnlyDictionary<string, double> metrics,
            IReadOnlyList<MobaOptionalHealthFinding> findings)
        {
            Source = source ?? string.Empty;
            Metrics = metrics ?? EmptyMetrics;
            Findings = findings ?? EmptyFindings;
        }

        public string Source { get; }
        public IReadOnlyDictionary<string, double> Metrics { get; }
        public IReadOnlyList<MobaOptionalHealthFinding> Findings { get; }
        public bool IsAvailable => !string.IsNullOrEmpty(Source);
        public int WarningCount => Count(MobaRuntimeValidationSeverity.Warning);
        public int ErrorCount => Count(MobaRuntimeValidationSeverity.Error);

        private int Count(MobaRuntimeValidationSeverity severity)
        {
            var findings = Findings;
            if (findings == null) return 0;
            var count = 0;
            for (var i = 0; i < findings.Count; i++)
                if (findings[i].Severity == severity) count++;
            return count;
        }
    }

    /// <summary>
    /// Optional runtime capability that contributes diagnostics without participating in
    /// business execution or startup validity.
    /// </summary>
    public interface IMobaOptionalHealthContributor
    {
        MobaOptionalHealthContribution CollectHealth(
            IMobaBattleDiagnosticsService diagnostics,
            int currentFrame,
            string warningKeyPrefix);
    }
}
