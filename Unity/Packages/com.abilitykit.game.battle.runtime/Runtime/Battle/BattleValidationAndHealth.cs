using System;
using System.Collections.Generic;
using AbilityKit.Core.Collections;

namespace AbilityKit.Game.Battle
{
    public enum BattleValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
    }

    public readonly struct BattleValidationFinding
    {
        public BattleValidationFinding(
            string source,
            string code,
            BattleValidationSeverity severity,
            string message,
            bool blocksStartup = false)
        {
            if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Validation source is required.", nameof(source));
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Validation code is required.", nameof(code));

            Source = source;
            Code = code;
            Severity = severity;
            Message = message ?? string.Empty;
            BlocksStartup = blocksStartup;
        }

        public string Source { get; }

        public string Code { get; }

        public BattleValidationSeverity Severity { get; }

        public string Message { get; }

        public bool BlocksStartup { get; }
    }

    public sealed class BattleValidationReport
    {
        private readonly List<BattleValidationFinding> _findings = new List<BattleValidationFinding>();

        public IReadOnlyList<BattleValidationFinding> Findings => _findings;

        public int ErrorCount { get; private set; }

        public int WarningCount { get; private set; }

        public int InfoCount { get; private set; }

        public bool BlocksStartup { get; private set; }

        public bool IsValid => ErrorCount == 0;

        public void Add(in BattleValidationFinding finding)
        {
            _findings.Add(finding);
            switch (finding.Severity)
            {
                case BattleValidationSeverity.Error:
                    ErrorCount++;
                    break;
                case BattleValidationSeverity.Warning:
                    WarningCount++;
                    break;
                default:
                    InfoCount++;
                    break;
            }

            BlocksStartup |= finding.BlocksStartup;
        }
    }

    public interface IBattleValidator<in TContext>
    {
        string Name { get; }

        int Order { get; }

        void Validate(TContext context, BattleValidationReport report);
    }

    public sealed class BattleValidationRegistry<TContext>
    {
        private readonly StablePriorityList<IBattleValidator<TContext>> _validators =
            new StablePriorityList<IBattleValidator<TContext>>();
        private readonly HashSet<string> _names = new HashSet<string>(StringComparer.Ordinal);

        public void Register(IBattleValidator<TContext> validator)
        {
            if (validator == null) throw new ArgumentNullException(nameof(validator));
            if (string.IsNullOrWhiteSpace(validator.Name)) throw new InvalidOperationException("Battle validator name is required.");
            if (!_names.Add(validator.Name))
            {
                throw new InvalidOperationException($"Duplicate battle validator name '{validator.Name}'.");
            }

            _validators.Add(validator, validator.Order);
        }

        public BattleValidationReport Validate(TContext context)
        {
            var report = new BattleValidationReport();
            foreach (var validator in _validators)
            {
                validator.Validate(context, report);
            }

            return report;
        }

    }

    public enum BattleHealthLevel
    {
        Healthy = 0,
        Degraded = 1,
        Unhealthy = 2,
        Unknown = 3,
    }

    public readonly struct BattleHealthEntry
    {
        public BattleHealthEntry(
            string source,
            BattleHealthLevel level,
            string message,
            IReadOnlyDictionary<string, double> metrics = null)
        {
            if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Health source is required.", nameof(source));

            Source = source;
            Level = level;
            Message = message ?? string.Empty;
            Metrics = metrics ?? EmptyMetrics;
        }

        private static IReadOnlyDictionary<string, double> EmptyMetrics { get; } =
            new Dictionary<string, double>();

        public string Source { get; }

        public BattleHealthLevel Level { get; }

        public string Message { get; }

        public IReadOnlyDictionary<string, double> Metrics { get; }
    }

    public interface IBattleHealthProvider
    {
        string Name { get; }

        BattleHealthEntry CollectHealth();
    }

    public sealed class BattleHealthReport
    {
        internal BattleHealthReport(BattleHealthLevel level, IReadOnlyList<BattleHealthEntry> entries)
        {
            Level = level;
            Entries = entries;
        }

        public BattleHealthLevel Level { get; }

        public IReadOnlyList<BattleHealthEntry> Entries { get; }

        public bool IsHealthy => Level == BattleHealthLevel.Healthy;
    }

    public static class BattleHealthReporter
    {
        public static BattleHealthReport Collect(IEnumerable<IBattleHealthProvider> providers)
        {
            if (providers == null) throw new ArgumentNullException(nameof(providers));

            var entries = new List<BattleHealthEntry>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            var level = BattleHealthLevel.Healthy;
            foreach (var provider in providers)
            {
                if (provider == null) continue;
                if (string.IsNullOrWhiteSpace(provider.Name))
                {
                    throw new InvalidOperationException("Battle health provider name is required.");
                }

                if (!names.Add(provider.Name))
                {
                    throw new InvalidOperationException($"Duplicate battle health provider name '{provider.Name}'.");
                }

                var entry = provider.CollectHealth();
                entries.Add(entry);
                level = Merge(level, entry.Level);
            }

            return new BattleHealthReport(level, entries);
        }

        private static BattleHealthLevel Merge(BattleHealthLevel current, BattleHealthLevel candidate)
        {
            if (current == BattleHealthLevel.Unhealthy || candidate == BattleHealthLevel.Unhealthy)
            {
                return BattleHealthLevel.Unhealthy;
            }

            if (current == BattleHealthLevel.Unknown || candidate == BattleHealthLevel.Unknown)
            {
                return BattleHealthLevel.Unknown;
            }

            return current == BattleHealthLevel.Degraded || candidate == BattleHealthLevel.Degraded
                ? BattleHealthLevel.Degraded
                : BattleHealthLevel.Healthy;
        }
    }
}
