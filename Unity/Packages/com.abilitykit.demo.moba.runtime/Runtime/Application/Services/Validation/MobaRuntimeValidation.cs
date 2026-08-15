using System;
using System.Collections.Generic;
using System.Text;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Gameplay.Triggering;

namespace AbilityKit.Demo.Moba.Services
{
    public enum MobaRuntimeValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
    }

    public enum MobaRuntimeValidationMode
    {
        Disabled = 0,
        BootstrapStrict = 1,
        EditorFull = 2,
        RuntimeSampled = 3,
        ManualOnly = 4,
    }

    public enum MobaRuntimeValidationInvocation
    {
        Bootstrap = 0,
        Editor = 1,
        Runtime = 2,
        Manual = 3,
    }

    public enum MobaRuntimeValidationCategory
    {
        General = 0,
        RuntimeContract = 1,
        Config = 2,
        Input = 3,
        Snapshot = 4,
        Skill = 5,
        Lifecycle = 6,
        Diagnostics = 7,
    }

    public readonly struct MobaRuntimeValidationEntry
    {
        public readonly MobaRuntimeValidationSeverity Severity;
        public readonly MobaRuntimeValidationCategory Category;
        public readonly string Code;
        public readonly string Source;
        public readonly string Path;
        public readonly string Message;
        public readonly string BusinessId;
        public readonly long BusinessNumericId;
        public readonly bool BlocksStartup;

        public MobaRuntimeValidationEntry(
            MobaRuntimeValidationSeverity severity,
            string source,
            string path,
            string message,
            string businessId = null,
            bool blocksStartup = true,
            string code = null,
            MobaRuntimeValidationCategory category = MobaRuntimeValidationCategory.General,
            long businessNumericId = 0L)
        {
            Severity = severity;
            Category = category;
            Source = string.IsNullOrEmpty(source) ? "unknown" : source;
            Path = string.IsNullOrEmpty(path) ? "runtime" : path;
            Message = message ?? string.Empty;
            BusinessId = businessId;
            BusinessNumericId = businessNumericId;
            BlocksStartup = blocksStartup;
            Code = string.IsNullOrEmpty(code) ? CreateDefaultCode(severity, Source, Path) : code;
        }

        public MobaRuntimeValidationEntryDto ToDto()
        {
            return new MobaRuntimeValidationEntryDto(
                Severity,
                Severity.ToString(),
                Category,
                Category.ToString(),
                Code,
                Source,
                Path,
                Message,
                BusinessId,
                BusinessNumericId,
                BlocksStartup);
        }

        private static string CreateDefaultCode(MobaRuntimeValidationSeverity severity, string source, string path)
        {
            return "moba.validation." + NormalizeToken(severity.ToString()) + "." + NormalizeToken(source) + "." + NormalizeToken(path);
        }

        private static string NormalizeToken(string value)
        {
            if (string.IsNullOrEmpty(value)) return "unknown";

            var sb = new StringBuilder(value.Length);
            var previousWasSeparator = false;
            for (int i = 0; i < value.Length; i++)
            {
                var ch = char.ToLowerInvariant(value[i]);
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
                {
                    sb.Append(ch);
                    previousWasSeparator = false;
                    continue;
                }

                if (previousWasSeparator || sb.Length == 0) continue;
                sb.Append('_');
                previousWasSeparator = true;
            }

            if (sb.Length == 0) return "unknown";
            if (sb[sb.Length - 1] == '_') sb.Length--;
            return sb.Length == 0 ? "unknown" : sb.ToString();
        }
    }

    public readonly struct MobaRuntimeValidationEntryDto
    {
        public readonly MobaRuntimeValidationSeverity Severity;
        public readonly string SeverityName;
        public readonly MobaRuntimeValidationCategory Category;
        public readonly string CategoryName;
        public readonly string Code;
        public readonly string Source;
        public readonly string Path;
        public readonly string Message;
        public readonly string BusinessId;
        public readonly long BusinessNumericId;
        public readonly bool BlocksStartup;

        public MobaRuntimeValidationEntryDto(
            MobaRuntimeValidationSeverity severity,
            string severityName,
            MobaRuntimeValidationCategory category,
            string categoryName,
            string code,
            string source,
            string path,
            string message,
            string businessId,
            long businessNumericId,
            bool blocksStartup)
        {
            Severity = severity;
            SeverityName = severityName ?? string.Empty;
            Category = category;
            CategoryName = categoryName ?? string.Empty;
            Code = code ?? string.Empty;
            Source = source ?? string.Empty;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
            BusinessId = businessId;
            BusinessNumericId = businessNumericId;
            BlocksStartup = blocksStartup;
        }
    }

    public readonly struct MobaRuntimeValidationReportDto
    {
        public readonly int ErrorCount;
        public readonly int WarningCount;
        public readonly int InfoCount;
        public readonly bool ShouldBlockStartup;
        public readonly MobaRuntimeValidationEntryDto[] Entries;

        public MobaRuntimeValidationReportDto(
            int errorCount,
            int warningCount,
            int infoCount,
            bool shouldBlockStartup,
            MobaRuntimeValidationEntryDto[] entries)
        {
            ErrorCount = errorCount;
            WarningCount = warningCount;
            InfoCount = infoCount;
            ShouldBlockStartup = shouldBlockStartup;
            Entries = entries ?? Array.Empty<MobaRuntimeValidationEntryDto>();
        }
    }

    public sealed class MobaRuntimeValidationReport
    {
        private readonly List<MobaRuntimeValidationEntry> _entries = new List<MobaRuntimeValidationEntry>(64);

        public IReadOnlyList<MobaRuntimeValidationEntry> Entries => _entries;
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        public int InfoCount { get; private set; }
        public bool HasErrors => ErrorCount > 0;
        public bool ShouldBlockStartup { get; private set; }

        public void Info(string source, string path, string message, string businessId = null, string code = null, MobaRuntimeValidationCategory category = MobaRuntimeValidationCategory.General, long businessNumericId = 0L)
        {
            Add(new MobaRuntimeValidationEntry(MobaRuntimeValidationSeverity.Info, source, path, message, businessId, blocksStartup: false, code: code, category: category, businessNumericId: businessNumericId));
        }

        public void Warning(string source, string path, string message, string businessId = null, string code = null, MobaRuntimeValidationCategory category = MobaRuntimeValidationCategory.General, long businessNumericId = 0L)
        {
            Add(new MobaRuntimeValidationEntry(MobaRuntimeValidationSeverity.Warning, source, path, message, businessId, blocksStartup: false, code: code, category: category, businessNumericId: businessNumericId));
        }

        public void Error(string source, string path, string message, string businessId = null, bool blocksStartup = true, string code = null, MobaRuntimeValidationCategory category = MobaRuntimeValidationCategory.General, long businessNumericId = 0L)
        {
            Add(new MobaRuntimeValidationEntry(MobaRuntimeValidationSeverity.Error, source, path, message, businessId, blocksStartup, code, category, businessNumericId));
        }

        public void Add(in MobaRuntimeValidationEntry entry)
        {
            _entries.Add(entry);
            switch (entry.Severity)
            {
                case MobaRuntimeValidationSeverity.Error:
                    ErrorCount++;
                    if (entry.BlocksStartup) ShouldBlockStartup = true;
                    break;
                case MobaRuntimeValidationSeverity.Warning:
                    WarningCount++;
                    break;
                default:
                    InfoCount++;
                    break;
            }
        }

        public MobaRuntimeValidationReportDto ToDto()
        {
            var entries = _entries.Count == 0 ? Array.Empty<MobaRuntimeValidationEntryDto>() : new MobaRuntimeValidationEntryDto[_entries.Count];
            for (int i = 0; i < _entries.Count; i++)
            {
                entries[i] = _entries[i].ToDto();
            }

            return new MobaRuntimeValidationReportDto(ErrorCount, WarningCount, InfoCount, ShouldBlockStartup, entries);
        }

        public string FormatSummary()
        {
            return $"errors={ErrorCount}, warnings={WarningCount}, infos={InfoCount}, blockStartup={ShouldBlockStartup}";
        }

        public string FormatEntry(in MobaRuntimeValidationEntry entry)
        {
            var business = string.IsNullOrEmpty(entry.BusinessId) ? string.Empty : $" businessId={entry.BusinessId}";
            var businessNumeric = entry.BusinessNumericId == 0L ? string.Empty : $" businessNumericId={entry.BusinessNumericId}";
            return $"[MobaValidation] {entry.Severity} code={entry.Code} category={entry.Category} source={entry.Source} path={entry.Path}{business}{businessNumeric} {entry.Message}";
        }

        public string FormatAllEntries(int maxEntries = 32)
        {
            if (_entries.Count == 0) return string.Empty;

            var limit = maxEntries <= 0 ? _entries.Count : Math.Min(maxEntries, _entries.Count);
            var sb = new StringBuilder(limit * 96);
            for (int i = 0; i < limit; i++)
            {
                if (i > 0) sb.AppendLine();
                sb.Append(FormatEntry(_entries[i]));
            }

            if (limit < _entries.Count)
            {
                sb.AppendLine();
                sb.Append("[MobaValidation] further entries suppressed. remaining=").Append(_entries.Count - limit);
            }

            return sb.ToString();
        }
    }

    public readonly struct MobaRuntimeValidationContext
    {
        public readonly IWorldResolver Services;
        public readonly string StageName;
        public readonly MobaRuntimeValidationInvocation Invocation;

        public MobaRuntimeValidationContext(
            IWorldResolver services,
            string stageName,
            MobaRuntimeValidationInvocation invocation = MobaRuntimeValidationInvocation.Runtime)
        {
            Services = services;
            StageName = string.IsNullOrEmpty(stageName) ? "runtime" : stageName;
            Invocation = invocation;
        }

        public bool TryResolve<T>(out T service) where T : class
        {
            service = null;
            return Services != null && Services.TryResolve(out service) && service != null;
        }
    }

    public interface IMobaRuntimeValidator
    {
        string Name { get; }
        void Validate(in MobaRuntimeValidationContext context, MobaRuntimeValidationReport report);
    }

    public interface IMobaRuntimeValidationRegistry
    {
        void Register(IMobaRuntimeValidator validator);
        IReadOnlyList<IMobaRuntimeValidator> Validators { get; }
    }

    public interface IMobaRuntimeValidationRunner
    {
        MobaRuntimeValidationReport ValidateAll(in MobaRuntimeValidationContext context);
    }

    [WorldService(typeof(MobaRuntimeValidationOptions), WorldLifetime.Scoped)]
    public sealed class MobaRuntimeValidationOptions : IService
    {
        public bool Enabled = true;
        public MobaRuntimeValidationMode Mode = MobaRuntimeValidationMode.BootstrapStrict;
        public bool WriteReportEntries = true;
        public bool WriteSummary = true;
        public int MaxLoggedEntries = 32;
        public int RuntimeSampleInterval = 60;

        public void Dispose()
        {
            Enabled = true;
            Mode = MobaRuntimeValidationMode.BootstrapStrict;
            WriteReportEntries = true;
            WriteSummary = true;
            MaxLoggedEntries = 32;
            RuntimeSampleInterval = 60;
        }
    }

    public readonly struct MobaRequiredRuntimeValidatorContract
    {
        public readonly string Name;
        public readonly Type ValidatorType;
        public readonly Func<IMobaRuntimeValidator> Factory;

        public MobaRequiredRuntimeValidatorContract(string name, Type validatorType, Func<IMobaRuntimeValidator> factory)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name is required.", nameof(name));
            ValidatorType = validatorType ?? throw new ArgumentNullException(nameof(validatorType));
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            Name = name;
        }
    }

    public sealed class MobaRuntimeValidatorContractValidationResult
    {
        private readonly List<string> _errors = new List<string>(8);

        public IReadOnlyList<string> Errors => _errors;
        public bool Succeeded => _errors.Count == 0;

        public void AddError(string error)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                _errors.Add(error);
            }
        }
    }

    public sealed class MobaRuntimeValidatorContract
    {
        private static readonly MobaRequiredRuntimeValidatorContract[] s_defaultRequiredValidators =
        {
            CreateRequired<MobaRuntimeCoreDependencyValidator>("runtime.dependencies.core"),
            CreateRequired<MobaRuntimeSkillDependencyValidator>("runtime.dependencies.skill"),
            CreateRequired<MobaRuntimeContinuousDependencyValidator>("runtime.dependencies.continuous"),
            CreateRequired<MobaRuntimeCombatDependencyValidator>("runtime.dependencies.combat"),
            CreateRequired<MobaRuntimeTemporaryEntityDependencyValidator>("runtime.dependencies.temp_entity"),
            CreateRequired<MobaRuntimeOutputDependencyValidator>("runtime.dependencies.output"),
            CreateRequired<MobaRuntimeDiagnosticsDependencyValidator>("runtime.dependencies.diagnostics"),
            CreateRequired<MobaBattleMainFlowHealthValidator>("battle.main_flow"),
            CreateRequired<MobaBattleRuntimeReadinessValidator>("runtime.readiness"),
            CreateRequired<MobaRuntimeHealthSummaryValidator>(MobaRuntimeHealthSummaryValidator.SourceName),
            CreateRequired<MobaTemporaryEntityLifecycleReadinessValidator>("temp_entity.lifecycle.readiness"),
            CreateRequired<MobaBattleConfigReferenceValidator>("battle.config.references"),
            CreateRequired<MobaTriggerPlanIntegrityValidator>(MobaTriggerPlanIntegrityValidator.SourceName),
            CreateRequired<MobaGameplayTriggerRuntimeValidator>("gameplay.trigger.runtime"),
            CreateRequired<MobaContextIntegrityRuntimeValidator>(MobaContextIntegrityRuntimeValidator.SourceName),
        };

        private readonly List<MobaRequiredRuntimeValidatorContract> _requiredValidators = new List<MobaRequiredRuntimeValidatorContract>(8);

        public IReadOnlyList<MobaRequiredRuntimeValidatorContract> RequiredValidators => _requiredValidators;

        public static MobaRuntimeValidatorContract CreateDefault()
        {
            var contract = new MobaRuntimeValidatorContract();
            for (int i = 0; i < s_defaultRequiredValidators.Length; i++)
            {
                contract._requiredValidators.Add(s_defaultRequiredValidators[i]);
            }

            return contract;
        }

        public void Require<TValidator>(string name)
            where TValidator : IMobaRuntimeValidator, new()
        {
            _requiredValidators.Add(CreateRequired<TValidator>(name));
        }

        private static MobaRequiredRuntimeValidatorContract CreateRequired<TValidator>(string name)
            where TValidator : IMobaRuntimeValidator, new()
        {
            return new MobaRequiredRuntimeValidatorContract(name, typeof(TValidator), static () => new TValidator());
        }

        public void RegisterInto(IMobaRuntimeValidationRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            for (int i = 0; i < _requiredValidators.Count; i++)
            {
                registry.Register(_requiredValidators[i].Factory());
            }
        }

        public MobaRuntimeValidatorContractValidationResult Validate(IMobaRuntimeValidationRegistry registry)
        {
            var result = new MobaRuntimeValidatorContractValidationResult();
            if (registry == null)
            {
                result.AddError("runtime validator registry is missing.");
                return result;
            }

            for (int i = 0; i < _requiredValidators.Count; i++)
            {
                var required = _requiredValidators[i];
                if (HasValidator(registry.Validators, required)) continue;

                result.AddError($"missing required runtime validator. name={required.Name}, expected={required.ValidatorType.Name}.");
            }

            return result;
        }

        private static bool HasValidator(IReadOnlyList<IMobaRuntimeValidator> validators, in MobaRequiredRuntimeValidatorContract required)
        {
            if (validators == null) return false;

            for (int i = 0; i < validators.Count; i++)
            {
                var validator = validators[i];
                if (validator == null) continue;
                if (validator.GetType() == required.ValidatorType) return true;
                if (string.Equals(validator.Name, required.Name, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }
    }

    public interface IMobaRuntimeValidationHistory
    {
        long RunCount { get; }
        string LastStageName { get; }
        MobaRuntimeValidationReport LastReport { get; }
        bool HasLastReport { get; }
        bool TryGetLastReport(out MobaRuntimeValidationReport report);
    }

    [WorldService(typeof(IMobaRuntimeValidationRegistry), WorldLifetime.Scoped)]
    [WorldService(typeof(IMobaRuntimeValidationRunner), WorldLifetime.Scoped)]
    [WorldService(typeof(IMobaRuntimeValidationHistory), WorldLifetime.Scoped)]
    [WorldService(typeof(MobaRuntimeValidationService), WorldLifetime.Scoped)]
    public sealed class MobaRuntimeValidationService : IMobaRuntimeValidationRegistry, IMobaRuntimeValidationRunner, IMobaRuntimeValidationHistory, IService
    {
        private const string MetricRun = "moba.validation.run";
        private const string MetricBlocked = "moba.validation.blocked";
        private const string MetricErrors = "moba.validation.errors";
        private const string MetricWarnings = "moba.validation.warnings";
        private const string MetricInfos = "moba.validation.infos";

        private static readonly MobaRuntimeValidationOptions s_defaultOptions = new MobaRuntimeValidationOptions();

        private readonly List<IMobaRuntimeValidator> _validators = new List<IMobaRuntimeValidator>(16);
        private readonly HashSet<string> _validatorNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private long _runCount;
        private string _lastStageName;
        private MobaRuntimeValidationReport _lastReport;

        public IReadOnlyList<IMobaRuntimeValidator> Validators => _validators;
        public long RunCount => _runCount;
        public string LastStageName => _lastStageName;
        public MobaRuntimeValidationReport LastReport => _lastReport;
        public bool HasLastReport => _lastReport != null;

        public void Register(IMobaRuntimeValidator validator)
        {
            if (validator == null) return;

            var name = string.IsNullOrEmpty(validator.Name) ? validator.GetType().Name : validator.Name;
            if (!_validatorNames.Add(name)) return;
            _validators.Add(validator);
        }

        public bool TryGetLastReport(out MobaRuntimeValidationReport report)
        {
            report = _lastReport;
            return report != null;
        }

        public MobaRuntimeValidationReport ValidateAll(in MobaRuntimeValidationContext context)
        {
            var options = ResolveOptions(in context);
            var report = new MobaRuntimeValidationReport();
            var runIndex = _runCount + 1L;
            var shouldRun = ShouldRun(in context, options, runIndex);

            if (shouldRun)
            {
                for (int i = 0; i < _validators.Count; i++)
                {
                    var validator = _validators[i];
                    try
                    {
                        validator.Validate(in context, report);
                    }
                    catch (Exception ex)
                    {
                        ReportValidatorException(in context, report, validator, ex);
                    }
                }
            }

            _runCount = runIndex;
            _lastStageName = context.StageName;
            _lastReport = report;

            if (shouldRun)
            {
                RecordDiagnostics(in context, report);
            }

            WriteReport(report, options, shouldRun, context.Invocation);
            return report;
        }

        public void Dispose()
        {
            _validators.Clear();
            _validatorNames.Clear();
            _lastStageName = null;
            _lastReport = null;
            _runCount = 0L;
        }

        private static void ReportValidatorException(
            in MobaRuntimeValidationContext context,
            MobaRuntimeValidationReport report,
            IMobaRuntimeValidator validator,
            Exception exception)
        {
            var validatorName = validator == null || string.IsNullOrEmpty(validator.Name)
                ? "unknown"
                : validator.Name;

            report.Error(validatorName, context.StageName, "validator exception: " + exception.Message);

            if (context.TryResolve<IMobaBattleExceptionPolicy>(out var policy) && policy != null)
            {
                policy.TryHandle(
                    exception,
                    new MobaBattleExceptionContext(
                        MobaBattleExceptionDomain.Bootstrap,
                        "RuntimeValidation",
                        detail: $"stage={context.StageName} validator={validatorName}"),
                    MobaBattleExceptionSeverity.Critical);
                return;
            }

            MobaRuntimeLog.Exception(exception, MobaRuntimeLogModule.Diagnostics, MobaRuntimeLogPurpose.Exception, nameof(MobaRuntimeValidationService), $"Validator failed. name={validatorName}");
        }

        private static void RecordDiagnostics(in MobaRuntimeValidationContext context, MobaRuntimeValidationReport report)
        {
            if (report == null) return;
            if (!context.TryResolve<IMobaBattleDiagnosticsService>(out var diagnostics) || diagnostics == null) return;

            diagnostics.Counter(MetricRun);
            diagnostics.Gauge(MetricErrors, report.ErrorCount);
            diagnostics.Gauge(MetricWarnings, report.WarningCount);
            diagnostics.Gauge(MetricInfos, report.InfoCount);

            if (report.ShouldBlockStartup)
            {
                diagnostics.Counter(MetricBlocked);
            }
        }

        private static MobaRuntimeValidationOptions ResolveOptions(in MobaRuntimeValidationContext context)
        {
            return context.TryResolve<MobaRuntimeValidationOptions>(out var options) && options != null
                ? options
                : s_defaultOptions;
        }

        private static bool ShouldRun(in MobaRuntimeValidationContext context, MobaRuntimeValidationOptions options, long runIndex)
        {
            if (options == null || !options.Enabled) return false;

            switch (options.Mode)
            {
                case MobaRuntimeValidationMode.Disabled:
                    return false;
                case MobaRuntimeValidationMode.BootstrapStrict:
                    return context.Invocation == MobaRuntimeValidationInvocation.Bootstrap;
                case MobaRuntimeValidationMode.EditorFull:
                    return context.Invocation == MobaRuntimeValidationInvocation.Editor
                        || context.Invocation == MobaRuntimeValidationInvocation.Bootstrap
                        || context.Invocation == MobaRuntimeValidationInvocation.Manual;
                case MobaRuntimeValidationMode.RuntimeSampled:
                    if (context.Invocation == MobaRuntimeValidationInvocation.Bootstrap || context.Invocation == MobaRuntimeValidationInvocation.Manual) return true;
                    if (context.Invocation != MobaRuntimeValidationInvocation.Runtime) return false;
                    var interval = options.RuntimeSampleInterval <= 0 ? 1 : options.RuntimeSampleInterval;
                    return runIndex == 1L || runIndex % interval == 0L;
                case MobaRuntimeValidationMode.ManualOnly:
                    return context.Invocation == MobaRuntimeValidationInvocation.Manual;
                default:
                    return false;
            }
        }

        private static void WriteReport(MobaRuntimeValidationReport report, MobaRuntimeValidationOptions options, bool executed, MobaRuntimeValidationInvocation invocation)
        {
            if (report == null) return;
            if (options == null || !options.WriteSummary) return;

            if (!executed)
            {
                if (invocation != MobaRuntimeValidationInvocation.Runtime)
                {
                    MobaRuntimeLog.Info(
                        MobaRuntimeLogModule.Diagnostics,
                        MobaRuntimeLogPurpose.Validation,
                        nameof(MobaRuntimeValidationService),
                        () => "Runtime validation skipped. mode=" + options.Mode + " invocation=" + invocation);
                }

                return;
            }

            if (report.Entries.Count == 0)
            {
                MobaRuntimeLog.Info(MobaRuntimeLogModule.Diagnostics, MobaRuntimeLogPurpose.Validation, nameof(MobaRuntimeValidationService), "Runtime validation completed. no issues.");
                return;
            }

            if (options.WriteReportEntries)
            {
                var maxEntries = options.MaxLoggedEntries <= 0 ? report.Entries.Count : Math.Min(options.MaxLoggedEntries, report.Entries.Count);
                for (int i = 0; i < maxEntries; i++)
                {
                    var entry = report.Entries[i];
                    switch (entry.Severity)
                    {
                        case MobaRuntimeValidationSeverity.Error:
                            MobaRuntimeLog.Error(
                                MobaRuntimeLogModule.Diagnostics,
                                MobaRuntimeLogPurpose.Validation,
                                nameof(MobaRuntimeValidationService),
                                () => report.FormatEntry(entry));
                            break;
                        case MobaRuntimeValidationSeverity.Warning:
                            MobaRuntimeLog.Warning(
                                MobaRuntimeLogModule.Diagnostics,
                                MobaRuntimeLogPurpose.Validation,
                                nameof(MobaRuntimeValidationService),
                                () => report.FormatEntry(entry));
                            break;
                        default:
                            MobaRuntimeLog.Info(
                                MobaRuntimeLogModule.Diagnostics,
                                MobaRuntimeLogPurpose.Validation,
                                nameof(MobaRuntimeValidationService),
                                () => report.FormatEntry(entry));
                            break;
                    }
                }

                if (maxEntries < report.Entries.Count)
                {
                    MobaRuntimeLog.Warning(
                        MobaRuntimeLogModule.Diagnostics,
                        MobaRuntimeLogPurpose.Validation,
                        nameof(MobaRuntimeValidationService),
                        () => "Runtime validation entries suppressed. remaining=" + (report.Entries.Count - maxEntries));
                }
            }

            MobaRuntimeLog.Warning(
                MobaRuntimeLogModule.Diagnostics,
                MobaRuntimeLogPurpose.Validation,
                nameof(MobaRuntimeValidationService),
                () => "Runtime validation completed. " + report.FormatSummary());
        }
    }
}
