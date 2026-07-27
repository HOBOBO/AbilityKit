using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle;

namespace AbilityKit.Demo.Moba.Services
{
    public static class MobaBattleCompositionAdapters
    {
        public static BattleValidationReport ToBattleValidationReport(MobaRuntimeValidationReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));

            var result = new BattleValidationReport();
            for (int i = 0; i < report.Entries.Count; i++)
            {
                var entry = report.Entries[i];
                var finding = new BattleValidationFinding(
                    entry.Source,
                    entry.Code,
                    ToBattleSeverity(entry.Severity),
                    entry.Message,
                    entry.BlocksStartup);
                result.Add(in finding);
            }

            return result;
        }

        public static BattleHealthEntry ToBattleHealthEntry(in MobaRuntimeHealthSummary summary)
        {
            var metrics = new Dictionary<string, double>
            {
                ["skill.active"] = summary.ActiveSkillRuntimes,
                ["skill.waiting"] = summary.WaitingSkillRuntimes,
                ["skill.pending_children"] = summary.PendingSkillChildren,
                ["trace.roots"] = summary.TraceRoots,
                ["trace.active_roots"] = summary.ActiveTraceRoots,
                ["trace.retained_roots"] = summary.RetainedTraceRoots,
                ["trace.retained_ended_roots"] = summary.RetainedEndedTraceRoots,
                ["trace.stale_retained_roots"] = summary.StaleRetainedTraceRoots,
                ["validation.errors"] = summary.ValidationErrors,
                ["validation.warnings"] = summary.ValidationWarnings,
                ["validation.infos"] = summary.ValidationInfos,
                ["validation.blocks_startup"] = summary.ValidationBlocksStartup ? 1d : 0d,
            };

            return new BattleHealthEntry(
                MobaRuntimeHealthSummaryValidator.SourceName,
                ToBattleHealthLevel(in summary),
                summary.ToString(),
                metrics);
        }

        private static BattleValidationSeverity ToBattleSeverity(MobaRuntimeValidationSeverity severity)
        {
            switch (severity)
            {
                case MobaRuntimeValidationSeverity.Error:
                    return BattleValidationSeverity.Error;
                case MobaRuntimeValidationSeverity.Warning:
                    return BattleValidationSeverity.Warning;
                default:
                    return BattleValidationSeverity.Info;
            }
        }

        private static BattleHealthLevel ToBattleHealthLevel(in MobaRuntimeHealthSummary summary)
        {
            if (summary.HasRuntimeErrors) return BattleHealthLevel.Unhealthy;
            if (summary.HasRuntimeWarnings) return BattleHealthLevel.Degraded;
            if (!summary.HasSkillRuntime || !summary.HasTraceRegistry) return BattleHealthLevel.Unknown;
            return BattleHealthLevel.Healthy;
        }
    }
}
