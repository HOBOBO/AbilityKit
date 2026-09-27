using AbilityKit.Ability.FrameSync;
using AbilityKit.Game.Battle;

namespace AbilityKit.Demo.Moba.Services
{
    public readonly struct MobaRuntimeHealthSummary
    {
        public readonly bool HasSkillRuntime;
        public readonly int ActiveSkillRuntimes;
        public readonly int WaitingSkillRuntimes;
        public readonly int PendingSkillChildren;
        public readonly MobaOptionalHealthContribution OptionalHealth;
        public readonly bool HasValidationHistory;
        public readonly bool ValidationBlocksStartup;
        public readonly int ValidationErrors;
        public readonly int ValidationWarnings;
        public readonly int ValidationInfos;

        public MobaRuntimeHealthSummary(
            bool hasSkillRuntime,
            int activeSkillRuntimes,
            int waitingSkillRuntimes,
            int pendingSkillChildren,
            MobaOptionalHealthContribution optionalHealth,
            bool hasValidationHistory,
            bool validationBlocksStartup,
            int validationErrors,
            int validationWarnings,
            int validationInfos)
        {
            HasSkillRuntime = hasSkillRuntime;
            ActiveSkillRuntimes = activeSkillRuntimes;
            WaitingSkillRuntimes = waitingSkillRuntimes;
            PendingSkillChildren = pendingSkillChildren;
            OptionalHealth = optionalHealth;
            HasValidationHistory = hasValidationHistory;
            ValidationBlocksStartup = validationBlocksStartup;
            ValidationErrors = validationErrors;
            ValidationWarnings = validationWarnings;
            ValidationInfos = validationInfos;
        }

        public bool HasRuntimeWarnings => WaitingSkillRuntimes > 0 || PendingSkillChildren > 0 || OptionalHealth.WarningCount > 0 || ValidationWarnings > 0;
        public bool HasRuntimeErrors => ValidationBlocksStartup || OptionalHealth.ErrorCount > 0 || ValidationErrors > 0;
        public bool HasObservabilityIssues => HasRuntimeWarnings || HasRuntimeErrors;
        public bool IsHealthy => !HasObservabilityIssues;

        public override string ToString()
        {
            return $"healthy={IsHealthy}, skillRuntime={HasSkillRuntime}, activeSkills={ActiveSkillRuntimes}, waitingSkills={WaitingSkillRuntimes}, pendingSkillChildren={PendingSkillChildren}, optionalHealth={OptionalHealth.Source}, optionalWarnings={OptionalHealth.WarningCount}, optionalErrors={OptionalHealth.ErrorCount}, validation={HasValidationHistory}, validationErrors={ValidationErrors}, validationWarnings={ValidationWarnings}, validationInfos={ValidationInfos}, validationBlocksStartup={ValidationBlocksStartup}";
        }
    }

    public interface IMobaRuntimeHealthSummaryProvider
    {
        MobaRuntimeHealthSummary CollectHealth();
    }

    public sealed class MobaRuntimeHealthSummaryValidator : IMobaRuntimeValidator, IMobaRuntimeHealthSummaryProvider, IBattleHealthProvider
    {
        public const string SourceName = "runtime.health.summary";
        private const string MetricHealthy = "moba.runtime.health.healthy";
        private const string MetricSkillActive = "moba.runtime.health.skill.active";
        private const string MetricSkillWaiting = "moba.runtime.health.skill.waiting";
        private const string MetricSkillPendingChildren = "moba.runtime.health.skill.pending.children";
        private const string MetricValidationErrors = "moba.runtime.health.validation.errors";
        private const string MetricValidationWarnings = "moba.runtime.health.validation.warnings";

        private MobaRuntimeValidationContext _lastContext;
        private bool _hasContext;
        private MobaRuntimeHealthSummary _lastSummary;

        public string Name => SourceName;

        public MobaRuntimeHealthSummary LastSummary => _lastSummary;

        public MobaRuntimeHealthSummary CollectHealth()
        {
            return _hasContext ? CollectHealth(in _lastContext) : _lastSummary;
        }

        BattleHealthEntry IBattleHealthProvider.CollectHealth()
        {
            var summary = CollectHealth();
            return MobaBattleCompositionAdapters.ToBattleHealthEntry(in summary);
        }

        public void Validate(in MobaRuntimeValidationContext context, MobaRuntimeValidationReport report)
        {
            if (report == null) return;

            _lastContext = context;
            _hasContext = true;
            var summary = CollectHealth(in context);
            _lastSummary = summary;

            if (!summary.HasSkillRuntime)
            {
                report.Warning(SourceName, "skill.runtime", "MobaSkillCastRuntimeService is not resolved; skill runtime health cannot be aggregated.", nameof(MobaSkillCastRuntimeService), code: "moba.runtime.health.skill_runtime_missing", category: MobaRuntimeValidationCategory.Diagnostics);
            }

            if (summary.WaitingSkillRuntimes > 0)
            {
                report.Warning(SourceName, "skill.runtime.waiting_children", $"Skill runtimes are waiting for retained children. waiting={summary.WaitingSkillRuntimes}, pendingChildren={summary.PendingSkillChildren}.", nameof(MobaSkillCastRuntimeService), code: "moba.runtime.health.skill_waiting_children", category: MobaRuntimeValidationCategory.Diagnostics);
            }

            AppendOptionalHealthFindings(in summary.OptionalHealth, report);

            if (summary.HasValidationHistory && summary.ValidationBlocksStartup)
            {
                report.Warning(SourceName, "validation.history", "Last runtime validation report blocks startup. Health summary mirrors this as diagnostics instead of introducing a second startup blocker. " + summary, nameof(IMobaRuntimeValidationHistory), code: "moba.runtime.health.validation_blocked", category: MobaRuntimeValidationCategory.Diagnostics);
            }

            RecordDiagnostics(in context, in summary);
            report.Info(SourceName, "summary", summary.ToString(), code: "moba.runtime.health.summary", category: MobaRuntimeValidationCategory.Diagnostics);
        }

        private static MobaRuntimeHealthSummary CollectHealth(in MobaRuntimeValidationContext context)
        {
            context.TryResolve<IMobaBattleDiagnosticsService>(out var diagnostics);

            var hasSkillRuntime = context.TryResolve<MobaSkillCastRuntimeService>(out var skillRuntimes) && skillRuntimes != null;
            var skillScan = hasSkillRuntime
                ? skillRuntimes.ScanDiagnostics(diagnostics)
                : default;

            var hasOptionalContributor = context.TryResolve<IMobaOptionalHealthContributor>(out var contributor) && contributor != null;
            var optionalHealth = hasOptionalContributor
                ? contributor.CollectHealth(diagnostics, ResolveCurrentFrame(in context), SourceName + ".optional")
                : default;

            MobaRuntimeValidationReport validationReport = null;
            var hasValidationHistory = context.TryResolve<IMobaRuntimeValidationHistory>(out var history) && history != null && history.TryGetLastReport(out validationReport) && validationReport != null;
            var validationBlocksStartup = hasValidationHistory && validationReport.ShouldBlockStartup;
            var validationErrors = hasValidationHistory ? validationReport.ErrorCount : 0;
            var validationWarnings = hasValidationHistory ? validationReport.WarningCount : 0;
            var validationInfos = hasValidationHistory ? validationReport.InfoCount : 0;

            return new MobaRuntimeHealthSummary(
                hasSkillRuntime,
                skillScan.ActiveRuntimes,
                skillScan.WaitingChildrenRuntimes,
                skillScan.PendingChildren,
                optionalHealth,
                hasValidationHistory,
                validationBlocksStartup,
                validationErrors,
                validationWarnings,
                validationInfos);
        }

        private static int ResolveCurrentFrame(in MobaRuntimeValidationContext context)
        {
            return context.TryResolve<IFrameTime>(out var frameTime) && frameTime != null
                ? frameTime.Frame.Value
                : 0;
        }

        private static void RecordDiagnostics(in MobaRuntimeValidationContext context, in MobaRuntimeHealthSummary summary)
        {
            if (!context.TryResolve<IMobaBattleDiagnosticsService>(out var diagnostics) || diagnostics == null) return;

            diagnostics.Gauge(MetricHealthy, summary.IsHealthy ? 1 : 0);
            diagnostics.Gauge(MetricSkillActive, summary.ActiveSkillRuntimes);
            diagnostics.Gauge(MetricSkillWaiting, summary.WaitingSkillRuntimes);
            diagnostics.Gauge(MetricSkillPendingChildren, summary.PendingSkillChildren);
            diagnostics.Gauge(MetricValidationErrors, summary.ValidationErrors);
            diagnostics.Gauge(MetricValidationWarnings, summary.ValidationWarnings);
        }

        private static void AppendOptionalHealthFindings(
            in MobaOptionalHealthContribution contribution,
            MobaRuntimeValidationReport report)
        {
            var findings = contribution.Findings;
            if (findings == null) return;

            var source = contribution.IsAvailable ? contribution.Source : SourceName;
            for (var i = 0; i < findings.Count; i++)
            {
                var finding = findings[i];
                switch (finding.Severity)
                {
                    case MobaRuntimeValidationSeverity.Error:
                        report.Error(source, finding.Path, finding.Message, blocksStartup: false, code: finding.Code, category: MobaRuntimeValidationCategory.Diagnostics);
                        break;
                    case MobaRuntimeValidationSeverity.Warning:
                        report.Warning(source, finding.Path, finding.Message, code: finding.Code, category: MobaRuntimeValidationCategory.Diagnostics);
                        break;
                    default:
                        report.Info(source, finding.Path, finding.Message, code: finding.Code, category: MobaRuntimeValidationCategory.Diagnostics);
                        break;
                }
            }
        }
    }
}
