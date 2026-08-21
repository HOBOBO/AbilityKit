using System;
using System.Collections.Generic;
using System.Globalization;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Diagnostics.Analysis;

namespace AbilityKit.Demo.Moba.Services
{
    internal static class MobaAnalysisMetricCatalog
    {
        public static void AppendTo(List<AnalysisMetricCatalogEntry> catalog)
        {
            if (catalog == null) return;

            Add(catalog, MobaBattleDiagnosticMetric.ContinuousTick, "moba.continuous", "duration", "ms", "Continuous runtime tick duration.", "stable", "sampled", "continuous", "frame", "runtimeKind");
            Add(catalog, MobaBattleDiagnosticMetric.BuffDrain, "moba.buff", "duration", "ms", "Buff drain execution duration.", "stable", "sampled", "buff", "frame", "actor", "buff");
            Add(catalog, MobaBattleDiagnosticMetric.DamagePipeline, "moba.damage", "duration", "ms", "Damage pipeline execution duration.", "stable", "sampled", "damage", "frame", "actor", "skill");
            Add(catalog, MobaBattleDiagnosticMetric.DamageStage, "moba.damage", "duration", "ms", "Damage stage execution duration.", "stable", "sampled", "damage", "frame", "stage", "actor", "skill");
            Add(catalog, MobaBattleDiagnosticMetric.EffectsStep, "moba.effect", "duration", "ms", "Skill effect step execution duration.", "stable", "sampled", "skill", "frame", "actor", "skill", "traceKind");
            Add(catalog, MobaBattleDiagnosticMetric.SkillPipelineStep, "moba.skill", "duration", "ms", "Skill cast pipeline step duration.", "stable", "sampled", "skill", "frame", "actor", "skill", "pipelineStep");
            Add(catalog, MobaBattleDiagnosticMetric.SkillRunnerStep, "moba.skill", "duration", "ms", "Skill runner step duration.", "stable", "sampled", "skill", "frame", "runtime", "skill");

            Add(catalog, MobaBattleDiagnosticMetric.TraceRoots, "moba.trace", "gauge", "count", "Total trace roots retained by the MOBA trace registry.", "stable", "always", "trace", "frameRange");
            Add(catalog, MobaBattleDiagnosticMetric.TraceActiveRoots, "moba.trace", "gauge", "count", "Active trace roots that have not reached a terminal state.", "stable", "always", "trace", "frameRange", "traceKind");
            Add(catalog, MobaBattleDiagnosticMetric.TraceRetainedRoots, "moba.trace", "gauge", "count", "Trace roots retained for analysis export.", "stable", "always", "trace", "frameRange");
            Add(catalog, MobaBattleDiagnosticMetric.TraceRetainedEndedRoots, "moba.trace", "gauge", "count", "Ended trace roots still retained for later analysis.", "stable", "always", "trace", "frameRange");
            Add(catalog, MobaBattleDiagnosticMetric.TraceStaleRetainedRoots, "moba.trace", "gauge", "count", "Retained trace roots considered stale by cleanup policy.", "stable", "always", "trace", "frameRange");

            Add(catalog, MobaBattleDiagnosticMetric.SkillRuntimeActive, "moba.skill", "gauge", "count", "Active skill runtime instances.", "stable", "always", "skill", "frame", "actor", "skill", "runtime");
            Add(catalog, MobaBattleDiagnosticMetric.SkillRuntimeWaitingChildren, "moba.skill", "gauge", "count", "Skill runtimes waiting for child runtime completion.", "stable", "always", "skill", "frame", "actor", "skill", "runtime");
            Add(catalog, MobaBattleDiagnosticMetric.SkillRuntimePendingChildren, "moba.skill", "gauge", "count", "Pending child runtimes linked to skill runtimes.", "stable", "always", "skill", "frame", "actor", "skill", "runtime");

            Add(catalog, MobaBattleDiagnosticMetric.TriggerRegistered, "moba.trigger", "counter", "count", "Registered trigger listener count.", "stable", "always", "triggering", "triggerKind", "actor", "skill");
            Add(catalog, MobaBattleDiagnosticMetric.TriggerUnregistered, "moba.trigger", "counter", "count", "Unregistered trigger listener count.", "stable", "always", "triggering", "triggerKind", "actor", "skill");
            Add(catalog, MobaBattleDiagnosticMetric.TriggerDispatchScope, "moba.trigger", "duration", "ms", "Nested profiler scope for a complete trigger dispatch.", "stable", "profiled", "triggering", "frame");
            Add(catalog, MobaBattleDiagnosticMetric.TriggerEvaluateScope, "moba.trigger", "duration", "ms", "Nested profiler scope for trigger condition evaluation.", "stable", "profiled", "triggering", "frame");
            Add(catalog, MobaBattleDiagnosticMetric.TriggerExecuteScope, "moba.trigger", "duration", "ms", "Nested profiler scope for trigger execution.", "stable", "profiled", "triggering", "frame");
            Add(catalog, MobaBattleDiagnosticMetric.TriggerDispatchStarted, "moba.trigger", "counter", "count", "Trigger dispatch start count.", "stable", "always", "triggering", "triggerKind", "frame");
            Add(catalog, MobaBattleDiagnosticMetric.TriggerDispatchCompleted, "moba.trigger", "counter", "count", "Trigger dispatch completion count.", "stable", "always", "triggering", "triggerKind", "frame");
            Add(catalog, MobaBattleDiagnosticMetric.TriggerDispatchDuration, "moba.trigger", "duration", "ms", "Trigger dispatch duration.", "stable", "sampled", "triggering", "triggerKind", "frame");
            Add(catalog, MobaBattleDiagnosticMetric.TriggerDispatchExecuted, "moba.trigger", "counter", "count", "Executed trigger actions during dispatch.", "stable", "always", "triggering", "triggerKind", "frame");
            Add(catalog, MobaBattleDiagnosticMetric.TriggerDispatchShortCircuited, "moba.trigger", "counter", "count", "Trigger dispatches stopped by short-circuit policy.", "stable", "always", "triggering", "triggerKind", "frame");
            Add(catalog, MobaBattleDiagnosticMetric.TriggerEvaluated, "moba.trigger", "counter", "count", "Trigger condition evaluations.", "stable", "always", "triggering", "triggerKind", "actor", "skill");
            Add(catalog, MobaBattleDiagnosticMetric.TriggerEvaluatePassed, "moba.trigger", "counter", "count", "Passed trigger condition evaluations.", "stable", "always", "triggering", "triggerKind", "actor", "skill");
            Add(catalog, MobaBattleDiagnosticMetric.TriggerEvaluateFailed, "moba.trigger", "counter", "count", "Failed trigger condition evaluations.", "stable", "always", "triggering", "triggerKind", "actor", "skill");
            Add(catalog, MobaBattleDiagnosticMetric.TriggerEvaluateDuration, "moba.trigger", "duration", "ms", "Trigger condition evaluation duration.", "stable", "sampled", "triggering", "triggerKind", "actor", "skill");
            Add(catalog, MobaBattleDiagnosticMetric.TriggerExecuted, "moba.trigger", "counter", "count", "Executed trigger action count.", "stable", "always", "triggering", "triggerKind", "actor", "skill");
            Add(catalog, MobaBattleDiagnosticMetric.TriggerExecuteDuration, "moba.trigger", "duration", "ms", "Trigger action execution duration.", "stable", "sampled", "triggering", "triggerKind", "actor", "skill");
            Add(catalog, MobaBattleDiagnosticMetric.TriggerShortCircuit, "moba.trigger", "counter", "count", "Trigger action short-circuit count.", "stable", "always", "triggering", "triggerKind", "actor", "skill");
            Add(catalog, MobaBattleDiagnosticMetric.TriggerActionInterrupted, "moba.trigger", "counter", "count", "Interrupted trigger action count.", "stable", "always", "triggering", "triggerKind", "actor", "skill");
            Add(catalog, MobaBattleDiagnosticMetric.TriggerActionFailed, "moba.trigger", "counter", "count", "Failed trigger action count.", "stable", "always", "triggering", "triggerKind", "actor", "skill");
            Add(catalog, MobaBattleDiagnosticMetric.EffectExecuteScope, "moba.effect", "duration", "ms", "Nested profiler scope for a complete effect execution.", "stable", "profiled", "effect", "frame");
            Add(catalog, MobaBattleDiagnosticMetric.EffectActionScope, "moba.effect", "duration", "ms", "Nested profiler scope for one formal effect action.", "stable", "profiled", "effect", "action", "frame");
            Add(catalog, MobaBattleDiagnosticMetric.EffectActionInvoked, "moba.effect", "counter", "count", "Formal effect action invocation count.", "stable", "always", "effect", "action", "actor", "skill");
            Add(catalog, MobaBattleDiagnosticMetric.EffectActionSucceeded, "moba.effect", "counter", "count", "Successfully completed formal effect action count.", "stable", "always", "effect", "action", "actor", "skill");
            Add(catalog, MobaBattleDiagnosticMetric.EffectActionFailed, "moba.effect", "counter", "count", "Failed formal effect action count.", "stable", "always", "effect", "action", "actor", "skill");
            Add(catalog, MobaBattleDiagnosticMetric.EffectActionDuration, "moba.effect", "duration", "ms", "Formal effect action execution duration.", "stable", "sampled", "effect", "action", "actor", "skill");
            Add(catalog, MobaBattleDiagnosticMetric.EffectActionAllocatedBytes, "moba.effect", "sample", "bytes", "Managed bytes allocated by a sampled formal effect action on the executing thread.", "stable", "sampled", "effect", "action", "actor", "skill");

            Add(catalog, MobaBattleDiagnosticMetric.PipelineTraceEvent, "moba.pipeline", "event", "none", "Pipeline trace event exported for correlation.", "stable", "always", "pipeline", "traceKind", "rootContextId", "sourceContextId");
            Add(catalog, MobaBattleDiagnosticMetric.PipelineRunStarted, "moba.pipeline", "counter", "count", "Pipeline run start count.", "stable", "always", "pipeline", "pipeline", "frame");
            Add(catalog, MobaBattleDiagnosticMetric.PipelineRunEnded, "moba.pipeline", "counter", "count", "Pipeline run end count.", "stable", "always", "pipeline", "pipeline", "frame");
            Add(catalog, MobaBattleDiagnosticMetric.PipelinePhaseStarted, "moba.pipeline", "counter", "count", "Pipeline phase start count.", "stable", "always", "pipeline", "pipeline", "phase", "frame");
            Add(catalog, MobaBattleDiagnosticMetric.PipelinePhaseCompleted, "moba.pipeline", "counter", "count", "Pipeline phase completion count.", "stable", "always", "pipeline", "pipeline", "phase", "frame");
            Add(catalog, MobaBattleDiagnosticMetric.PipelinePhaseError, "moba.pipeline", "counter", "count", "Pipeline phase error count.", "stable", "always", "pipeline", "pipeline", "phase", "frame");
            Add(catalog, MobaBattleDiagnosticMetric.PipelineTick, "moba.pipeline", "counter", "count", "Pipeline tick count.", "stable", "always", "pipeline", "pipeline", "frame");
            Add(catalog, MobaBattleDiagnosticMetric.PipelinePaused, "moba.pipeline", "counter", "count", "Pipeline pause count.", "stable", "always", "pipeline", "pipeline", "frame");
            Add(catalog, MobaBattleDiagnosticMetric.PipelineResumed, "moba.pipeline", "counter", "count", "Pipeline resume count.", "stable", "always", "pipeline", "pipeline", "frame");
            Add(catalog, MobaBattleDiagnosticMetric.PipelineInterrupted, "moba.pipeline", "counter", "count", "Pipeline interruption count.", "stable", "always", "pipeline", "pipeline", "frame");

            Add(catalog, MobaBattleDiagnosticMetric.SkillRunnerRunning, "moba.skill", "gauge", "count", "Running skill runner count.", "stable", "always", "skill", "frame", "actor", "skill");
            Add(catalog, MobaBattleDiagnosticMetric.SkillRunnerTicked, "moba.skill", "counter", "count", "Skill runner tick count.", "stable", "always", "skill", "frame", "actor", "skill");
            Add(catalog, MobaBattleDiagnosticMetric.SkillRunnerEnded, "moba.skill", "counter", "count", "Skill runner end count.", "stable", "always", "skill", "frame", "actor", "skill");
            Add(catalog, MobaBattleDiagnosticMetric.SkillRunnerCleanupExceptions, "moba.skill", "counter", "count", "Skill runner cleanup exception count.", "stable", "always", "skill", "frame", "actor", "skill");

            Add(catalog, MobaBattleDiagnosticMetric.PlanActionSkipped, "moba.planAction", "counter", "count", "Skipped plan action count.", "stable", "always", "plan-action", "action", "actor", "skill");
            Add(catalog, MobaBattleDiagnosticMetric.PlanActionRejected, "moba.planAction", "counter", "count", "Rejected plan action count.", "stable", "always", "plan-action", "action", "actor", "skill");
            Add(catalog, MobaBattleDiagnosticMetric.PlanActionApplied, "moba.planAction", "counter", "count", "Applied plan action count.", "stable", "always", "plan-action", "action", "actor", "skill");
            Add(catalog, MobaBattleDiagnosticMetric.PlanActionTrace, "moba.planAction", "event", "none", "Plan action trace event for source correlation.", "stable", "always", "plan-action", "action", "rootContextId", "sourceContextId");

            Add(catalog, MobaBattleDiagnosticMetric.InputBatchAccepted, "moba.input", "counter", "count", "Accepted input batch count.", "stable", "always", "input", "frame", "player");
            Add(catalog, MobaBattleDiagnosticMetric.InputBatchAcceptedCount, "moba.input", "counter", "count", "Accepted commands inside input batches.", "stable", "always", "input", "frame", "player");
            Add(catalog, MobaBattleDiagnosticMetric.InputBatchHandledCount, "moba.input", "counter", "count", "Handled commands from accepted input batches.", "stable", "always", "input", "frame", "player");
            Add(catalog, MobaBattleDiagnosticMetric.InputCommandRejected, "moba.input", "counter", "count", "Rejected input command count.", "stable", "always", "input", "frame", "player", "reason");
            Add(catalog, MobaBattleDiagnosticMetric.InputCommandException, "moba.input", "counter", "count", "Input command exception count.", "stable", "always", "input", "frame", "player", "exceptionType");

            Add(catalog, MobaBattleDiagnosticMetric.SnapshotRequest, "moba.snapshot", "counter", "count", "Single snapshot request count.", "stable", "always", "snapshot", "frame", "opCode");
            Add(catalog, MobaBattleDiagnosticMetric.SnapshotBatchRequest, "moba.snapshot", "counter", "count", "Batch snapshot request count.", "stable", "always", "snapshot", "frame", "opCode");
            Add(catalog, MobaBattleDiagnosticMetric.SnapshotHit, "moba.snapshot", "counter", "count", "Snapshot request hit count.", "stable", "always", "snapshot", "frame", "opCode", "emitter");
            Add(catalog, MobaBattleDiagnosticMetric.SnapshotEmpty, "moba.snapshot", "counter", "count", "Snapshot request empty result count.", "stable", "always", "snapshot", "frame", "opCode", "emitter");
            Add(catalog, MobaBattleDiagnosticMetric.SnapshotEmitterCount, "moba.snapshot", "gauge", "count", "Registered snapshot emitter count.", "stable", "always", "snapshot", "frame");
            Add(catalog, MobaBattleDiagnosticMetric.SnapshotBatchSize, "moba.snapshot", "sample", "count", "Snapshot batch size distribution.", "stable", "sampled", "snapshot", "frame", "opCode");

            Add(catalog, MobaBattleDiagnosticMetric.ExceptionPrefix + "*", "moba.exception", "counter", "count", "Exception counters grouped by exception metric suffix.", "stable", "always", "diagnostics", "exceptionType", "rootContextId", "sourceContextId");
            Add(catalog, MobaBattleDiagnosticMetric.TempEntityPrefix + "*", "moba.temp_entity", "gauge", "count", "Temporary entity lifecycle metrics grouped by entity kind and lifecycle suffix.", "stable", "always", "runtime", "entityKind", "lifecycle", "frame");
            AppendFrameMetricsTo(catalog, BattleDiagnosticMetricProfileRegistry.Resolve());
        }

        public static void AppendFrameMetricsTo(
            List<AnalysisMetricCatalogEntry> catalog,
            BattleDiagnosticResolvedMetricProfile profile = null)
        {
            if (catalog == null) return;
            var descriptors = profile?.Descriptors ?? BattleDiagnosticFrameMetricCatalog.All;
            for (var i = 0; i < descriptors.Count; i++)
            {
                var descriptor = descriptors[i];
                if (catalog.Exists(entry => string.Equals(entry.Name, descriptor.Metric, StringComparison.Ordinal)))
                    continue;
                var entry = new AnalysisMetricCatalogEntry
                {
                    Name = descriptor.Metric,
                    Category = "battle.frame_metric." + descriptor.Category.ToString().ToLowerInvariant(),
                    Kind = descriptor.ValueKind.ToString().ToLowerInvariant(),
                    Unit = descriptor.Unit,
                    Description = descriptor.DisplayName + " frame history metric.",
                    Stability = "stable",
                    Sampling = "sampled",
                    Owner = "frame-sync"
                };
                entry.Dimensions.Add("frame");
                entry.Dimensions.Add("dimension");
                entry.Tags.Add(descriptor.Group);
                entry.Values.Add(new AnalysisKeyValue("displayName", descriptor.DisplayName));
                entry.Values.Add(new AnalysisKeyValue("group", descriptor.Group));
                entry.Values.Add(new AnalysisKeyValue("order", descriptor.Order.ToString(CultureInfo.InvariantCulture)));
                if (descriptor.HasSuggestedRange)
                {
                    entry.Values.Add(new AnalysisKeyValue("suggestedMinimum", Format(descriptor.SuggestedMinimum)));
                    entry.Values.Add(new AnalysisKeyValue("suggestedMaximum", Format(descriptor.SuggestedMaximum)));
                }
                if (descriptor.HasAssessment)
                {
                    entry.Values.Add(new AnalysisKeyValue("assessmentMode", descriptor.AssessmentMode.ToString()));
                    entry.Values.Add(new AnalysisKeyValue("warningThreshold", Format(descriptor.WarningThreshold)));
                    if (descriptor.CriticalThreshold < double.MaxValue)
                        entry.Values.Add(new AnalysisKeyValue("criticalThreshold", Format(descriptor.CriticalThreshold)));
                }
                catalog.Add(entry);
            }
        }

        public static void AppendFrameThresholdsTo(
            AnalysisThresholdProfile target,
            BattleDiagnosticResolvedMetricProfile profile = null)
        {
            if (target == null) return;
            var descriptors = profile?.Descriptors ?? BattleDiagnosticFrameMetricCatalog.All;
            for (var i = 0; i < descriptors.Count; i++)
            {
                var descriptor = descriptors[i];
                if (!descriptor.HasAssessment) continue;
                AddFrameThreshold(target, in descriptor, "warning", descriptor.WarningThreshold);
                if (descriptor.CriticalThreshold < double.MaxValue)
                    AddFrameThreshold(target, in descriptor, "critical", descriptor.CriticalThreshold);
            }
            AppendCompoundRules(target);
            if (profile != null) AppendProfileMetadata(target, profile);
        }

        private static void AddFrameThreshold(
            AnalysisThresholdProfile profile,
            in BattleDiagnosticMetricDescriptor descriptor,
            string severity,
            double value)
        {
            var id = "battle.frame_metric." + descriptor.Metric + "." + severity;
            var rule = profile.Rules.Find(item => string.Equals(item.Id, id, StringComparison.Ordinal));
            if (rule == null)
            {
                rule = new AnalysisThresholdRule { Id = id };
                profile.Rules.Add(rule);
            }
            rule.Metric = descriptor.Metric;
            rule.Category = descriptor.Category.ToString().ToLowerInvariant();
            rule.Severity = severity;
            rule.Operator = descriptor.AssessmentMode == BattleDiagnosticMetricAssessmentMode.WindowDeltaHigh
                ? "window-delta>="
                : descriptor.AssessmentMode == BattleDiagnosticMetricAssessmentMode.WindowMaximumHigh
                    ? "window-max>="
                    : ">=";
            rule.Value = value;
            rule.Unit = descriptor.Unit;
            rule.Description = descriptor.DisplayName + " " + severity + " threshold.";
            rule.Tags.Clear();
            rule.Tags.Add(descriptor.Group);
        }

        private static void AppendCompoundRules(AnalysisThresholdProfile profile)
        {
            var rules = BattleDiagnosticFrameMetricCatalog.AllCompoundRules;
            for (var i = 0; i < rules.Count; i++)
            {
                var descriptor = rules[i];
                var id = "battle.frame_metric.compound." + descriptor.Id;
                var rule = profile.Rules.Find(item => string.Equals(item.Id, id, StringComparison.Ordinal));
                if (rule == null)
                {
                    rule = new AnalysisThresholdRule { Id = id };
                    profile.Rules.Add(rule);
                }
                rule.Metric = descriptor.Id;
                rule.Category = descriptor.Category.ToString().ToLowerInvariant();
                rule.Severity = "warning";
                rule.Operator = "all";
                rule.Value = 1d;
                rule.Unit = "rule";
                rule.Description = descriptor.DisplayName;
                rule.Tags.Clear();
                rule.Tags.Add("compound");
                rule.Values.Clear();
                rule.Values.Add(new AnalysisKeyValue("primaryMetric", descriptor.PrimaryMetric));
                rule.Values.Add(new AnalysisKeyValue("secondaryMetric", descriptor.SecondaryMetric));
            }
        }

        private static void AppendProfileMetadata(
            AnalysisThresholdProfile target,
            BattleDiagnosticResolvedMetricProfile profile)
        {
            SetMetadata(target.Metadata, "frameMetricProfile", profile.Name);
            SetMetadata(target.Metadata, "frameMetricProject", profile.Context.Project);
            SetMetadata(target.Metadata, "frameMetricGameMode", profile.Context.GameMode);
            SetMetadata(target.Metadata, "frameMetricNetworkMode", profile.Context.NetworkMode);
            SetMetadata(target.Metadata, "frameMetricDeviceTier", profile.Context.DeviceTier);
        }

        private static void SetMetadata(List<AnalysisKeyValue> values, string key, string value)
        {
            var item = values.Find(entry => string.Equals(entry.Key, key, StringComparison.Ordinal));
            if (item == null)
            {
                values.Add(new AnalysisKeyValue(key, value));
                return;
            }
            item.Value = value ?? string.Empty;
        }

        private static void Add(List<AnalysisMetricCatalogEntry> catalog, string name, string category, string kind, string unit, string description, string stability, string sampling, string owner, params string[] dimensions)
        {
            var entry = new AnalysisMetricCatalogEntry
            {
                Name = name,
                Category = category,
                Kind = kind,
                Unit = unit,
                Description = description,
                Stability = stability,
                Sampling = sampling,
                Owner = owner
            };

            if (dimensions != null)
            {
                for (var i = 0; i < dimensions.Length; i++)
                {
                    entry.Dimensions.Add(dimensions[i]);
                }
            }

            catalog.Add(entry);
        }

        private static string Format(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
