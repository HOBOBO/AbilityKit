using System;
using System.Collections.Generic;
using System.Globalization;
using AbilityKit.Diagnostics.Analysis;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 在通用 Web 分析契约之上构建 MOBA 专用派生摘要。
    /// </summary>
    public static class MobaAnalysisDerivedSummaryBuilder
    {
        private const string SeverityInfo = "info";
        private const string SeverityWarning = "warning";
        private const string SeverityCritical = "critical";
        private const int TraceActiveNodesCriticalMin = 8;
        private const double TraceActiveNodesCriticalRatio = 0.5d;
        private const double SnapshotEmptyRateWarning = 0.2d;
        private const int RuntimeActiveWarning = 0;

        public static void AppendTo(AbilityKitAnalysisArtifact artifact, in MobaBattleDiagnosticsSnapshot snapshot)
        {
            if (artifact == null) return;
            AppendThresholdProfile(artifact.ThresholdProfile);
            AppendTraceHealth(artifact.Insights, artifact.Trace, artifact.ThresholdProfile);
            AppendSkillChain(artifact.Insights, artifact.Trace);
            AppendRuntimeLeak(artifact.Insights, snapshot, artifact.ThresholdProfile);
            AppendSnapshotContract(artifact.Insights, snapshot.Snapshot.Health, artifact.ThresholdProfile);
            AppendBaseline(artifact.Baseline, snapshot);
        }

        private static void AppendTraceHealth(AnalysisInsightsSection insights, AnalysisTraceSection trace, AnalysisThresholdProfile thresholds)
        {
            if (insights == null || trace == null) return;

            var rootCount = trace.Roots != null ? trace.Roots.Count : 0;
            var nodeCount = 0;
            var activeNodeCount = 0;
            var missingParentCount = 0;
            var truncatedRootCount = 0;
            var ids = new HashSet<long>();
            var parentIds = new List<long>(64);
            var kindCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var activeByKind = new Dictionary<string, int>(StringComparer.Ordinal);

            if (trace.Roots != null)
            {
                for (var i = 0; i < trace.Roots.Count; i++)
                {
                    var root = trace.Roots[i];
                    if (root == null) continue;
                    if (root.Truncated) truncatedRootCount++;
                    if (root.Nodes == null) continue;

                    for (var j = 0; j < root.Nodes.Count; j++)
                    {
                        var node = root.Nodes[j];
                        if (node == null) continue;
                        nodeCount++;
                        ids.Add(node.ContextId);
                        if (node.ParentId != 0L) parentIds.Add(node.ParentId);
                        Increment(kindCounts, Label(node.KindName, node.Kind));
                        if (!node.IsEnded)
                        {
                            activeNodeCount++;
                            Increment(activeByKind, Label(node.KindName, node.Kind));
                        }
                    }
                }
            }

            for (var i = 0; i < parentIds.Count; i++)
            {
                if (!ids.Contains(parentIds[i])) missingParentCount++;
            }

            var activeCriticalThreshold = nodeCount <= 0 ? TraceActiveNodesCriticalMin : Math.Max(TraceActiveNodesCriticalMin, (int)Math.Ceiling(nodeCount * TraceActiveNodesCriticalRatio));
            var severity = SeverityInfo;
            if (trace.Truncated || truncatedRootCount > 0 || missingParentCount > 0) severity = SeverityWarning;
            if (activeNodeCount > 0 && nodeCount > 0 && activeNodeCount >= activeCriticalThreshold) severity = SeverityCritical;
            AddThresholdEvaluation(thresholds, "moba.trace.active-nodes.critical", MobaBattleDiagnosticMetric.TraceActiveRoots, activeNodeCount, activeCriticalThreshold, "count", activeNodeCount >= activeCriticalThreshold ? SeverityCritical : SeverityInfo);

            var record = new AnalysisInsightRecord
            {
                Id = "moba.trace.health",
                Category = "trace-health",
                Title = "MOBA trace health",
                Summary = "roots=" + rootCount + ", nodes=" + nodeCount + ", active=" + activeNodeCount + ", missingParents=" + missingParentCount,
                Severity = severity,
                Score = nodeCount == 0 ? 0d : Math.Max(0d, 1d - ((double)activeNodeCount / nodeCount))
            };
            Add(record.Values, "rootCount", rootCount);
            Add(record.Values, "nodeCount", nodeCount);
            Add(record.Values, "activeNodeCount", activeNodeCount);
            Add(record.Values, "missingParentCount", missingParentCount);
            Add(record.Values, "truncated", trace.Truncated);
            Add(record.Values, "truncatedRootCount", truncatedRootCount);
            insights.Records.Add(record);

            if (trace.Truncated || truncatedRootCount > 0)
            {
                insights.Risks.Add(new AnalysisRiskRecord
                {
                    Id = "moba.trace.truncated",
                    Category = "trace-health",
                    Severity = SeverityWarning,
                    Message = "Trace export is truncated; derived conclusions may miss deeper effect chains.",
                    Recommendation = "Increase MaxTraceNodes/MaxTraceDepth or export a narrower active window."
                });
            }

            if (missingParentCount > 0)
            {
                var risk = new AnalysisRiskRecord
                {
                    Id = "moba.trace.missing-parent",
                    Category = "trace-health",
                    Severity = SeverityWarning,
                    Message = "Some trace nodes reference parents that are not present in the artifact.",
                    Recommendation = "Export complete roots when investigating causality or lineage gaps."
                };
                Add(risk.Values, "missingParentCount", missingParentCount);
                insights.Risks.Add(risk);
            }

            if (activeNodeCount > 0)
            {
                var risk = new AnalysisRiskRecord
                {
                    Id = "moba.trace.active-nodes",
                    Category = "trace-health",
                    Severity = activeNodeCount >= activeCriticalThreshold ? SeverityCritical : SeverityWarning,
                    Message = "Trace contains active nodes that have not reached an end reason.",
                    Recommendation = "Check runtime cleanup paths and end-scope disposal around skill/effect/projectile flows."
                };
                Add(risk.Values, "activeNodeCount", activeNodeCount);
                insights.Risks.Add(risk);
            }

            AppendRanking(insights, "moba.trace.kind-count", "nodes", kindCounts, 12);
            AppendRanking(insights, "moba.trace.active-kind-count", "nodes", activeByKind, 12);
        }

        private static void AppendSkillChain(AnalysisInsightsSection insights, AnalysisTraceSection trace)
        {
            if (insights == null || trace == null || trace.Roots == null) return;

            var skillRoots = 0;
            var completeSkillRoots = 0;
            var effectNodes = 0;
            var damageNodes = 0;
            var projectileNodes = 0;
            var buffNodes = 0;
            var presentationNodes = 0;
            var childRanking = new Dictionary<string, int>(StringComparer.Ordinal);

            for (var i = 0; i < trace.Roots.Count; i++)
            {
                var root = trace.Roots[i];
                if (root == null || root.Nodes == null || root.Nodes.Count == 0) continue;
                var rootNode = root.Nodes[0];
                if (rootNode == null || !IsKind(rootNode, MobaExecutionKind.SkillCast)) continue;

                skillRoots++;
                if (rootNode.IsEnded) completeSkillRoots++;
                var rootLabel = "skill#" + rootNode.ContextId;

                for (var j = 0; j < root.Nodes.Count; j++)
                {
                    var node = root.Nodes[j];
                    if (node == null) continue;
                    if (IsSkillEffect(node)) effectNodes++;
                    if (IsDamage(node)) damageNodes++;
                    if (IsProjectile(node)) projectileNodes++;
                    if (IsBuff(node)) buffNodes++;
                    if (IsPresentation(node)) presentationNodes++;
                }

                childRanking[rootLabel] = Math.Max(0, root.Nodes.Count - 1);
            }

            var severity = skillRoots == 0 ? SeverityInfo : completeSkillRoots < skillRoots ? SeverityWarning : SeverityInfo;
            var record = new AnalysisInsightRecord
            {
                Id = "moba.skill.chain",
                Category = "skill-chain",
                Title = "MOBA skill chain summary",
                Summary = "skillRoots=" + skillRoots + ", effects=" + effectNodes + ", damage=" + damageNodes + ", projectiles=" + projectileNodes,
                Severity = severity,
                Score = skillRoots == 0 ? 0d : (double)completeSkillRoots / skillRoots
            };
            Add(record.Values, "skillRootCount", skillRoots);
            Add(record.Values, "completeSkillRootCount", completeSkillRoots);
            Add(record.Values, "effectNodeCount", effectNodes);
            Add(record.Values, "damageNodeCount", damageNodes);
            Add(record.Values, "projectileNodeCount", projectileNodes);
            Add(record.Values, "buffNodeCount", buffNodes);
            Add(record.Values, "presentationNodeCount", presentationNodes);
            insights.Records.Add(record);
            AppendRanking(insights, "moba.skill.children", "nodes", childRanking, 12);
        }

        private static void AppendRuntimeLeak(AnalysisInsightsSection insights, in MobaBattleDiagnosticsSnapshot snapshot, AnalysisThresholdProfile thresholds)
        {
            if (insights == null) return;

            var activeTotal = 0;
            long spawnedTotal = 0L;
            long despawnedTotal = 0L;
            long rejectedTotal = 0L;
            long replacedTotal = 0L;
            var activeByKind = new Dictionary<string, int>(StringComparer.Ordinal);

            if (snapshot.Lifecycle != null)
            {
                for (var i = 0; i < snapshot.Lifecycle.Count; i++)
                {
                    var health = snapshot.Lifecycle[i];
                    activeTotal += health.ActiveCount;
                    spawnedTotal += health.SpawnedCount;
                    despawnedTotal += health.DespawnedCount;
                    rejectedTotal += health.RejectedCount;
                    replacedTotal += health.ReplacedCount;
                    activeByKind[health.Kind.ToString()] = health.ActiveCount;

                    var expectedActive = Math.Max(0L, health.SpawnedCount - health.DespawnedCount - health.RejectedCount - health.ReplacedCount);
                    if (health.ActiveCount > 0 && expectedActive <= 0L)
                    {
                        var risk = new AnalysisRiskRecord
                        {
                            Id = "moba.runtime.temp-entity-active-after-terminal." + health.Kind,
                            Category = "runtime-leak",
                            Severity = SeverityWarning,
                            Message = "Temporary entity kind remains active after terminal counters caught up.",
                            Recommendation = "Check despawn/expire cleanup and active-count reporting for " + health.Kind + "."
                        };
                        Add(risk.Values, "kind", health.Kind.ToString());
                        Add(risk.Values, "activeCount", health.ActiveCount);
                        Add(risk.Values, "spawnedCount", health.SpawnedCount);
                        Add(risk.Values, "despawnedCount", health.DespawnedCount);
                        Add(risk.Values, "rejectedCount", health.RejectedCount);
                        Add(risk.Values, "replacedCount", health.ReplacedCount);
                        insights.Risks.Add(risk);
                    }
                }
            }

            var runtimeSeverity = activeTotal > RuntimeActiveWarning ? SeverityWarning : SeverityInfo;
            AddThresholdEvaluation(thresholds, "moba.runtime.active.warning", MobaBattleDiagnosticMetric.SkillRuntimeActive, activeTotal, RuntimeActiveWarning, "count", runtimeSeverity);
            var record = new AnalysisInsightRecord
            {
                Id = "moba.runtime.leak",
                Category = "runtime-leak",
                Title = "MOBA runtime lifecycle pressure",
                Summary = "active=" + activeTotal + ", spawned=" + spawnedTotal + ", despawned=" + despawnedTotal,
                Severity = runtimeSeverity,
                Score = spawnedTotal <= 0L ? 0d : Math.Max(0d, 1d - ((double)activeTotal / Math.Max(1L, spawnedTotal)))
            };
            Add(record.Values, "activeTotal", activeTotal);
            Add(record.Values, "spawnedTotal", spawnedTotal);
            Add(record.Values, "despawnedTotal", despawnedTotal);
            Add(record.Values, "rejectedTotal", rejectedTotal);
            Add(record.Values, "replacedTotal", replacedTotal);
            insights.Records.Add(record);
            AppendRanking(insights, "moba.runtime.active-temp-entities", "entities", activeByKind, 8);
        }

        private static void AppendSnapshotContract(AnalysisInsightsSection insights, in MobaSnapshotRouterHealth health, AnalysisThresholdProfile thresholds)
        {
            if (insights == null) return;

            var totalRequests = health.SingleRequests + health.BatchRequests;
            var emptyRate = totalRequests <= 0L ? 0d : (double)health.EmptyCount / totalRequests;
            var severity = SeverityInfo;
            if (!health.IsOutputContractSatisfied || health.MissingRequiredEmitterCount > 0 || !health.HasEmitters) severity = SeverityCritical;
            else if (emptyRate > SnapshotEmptyRateWarning) severity = SeverityWarning;
            AddThresholdEvaluation(thresholds, "moba.snapshot.empty-rate.warning", MobaBattleDiagnosticMetric.SnapshotEmpty, emptyRate, SnapshotEmptyRateWarning, "ratio", emptyRate > SnapshotEmptyRateWarning ? SeverityWarning : SeverityInfo);

            var record = new AnalysisInsightRecord
            {
                Id = "moba.snapshot.contract",
                Category = "snapshot-contract",
                Title = "MOBA snapshot output contract",
                Summary = "emitters=" + health.EmitterCount + "/" + health.RequiredEmitterCount + ", missing=" + health.MissingRequiredEmitterCount + ", emptyRate=" + emptyRate.ToString("0.###", CultureInfo.InvariantCulture),
                Severity = severity,
                Score = health.RequiredEmitterCount <= 0 ? 0d : (double)(health.RequiredEmitterCount - health.MissingRequiredEmitterCount) / Math.Max(1, health.RequiredEmitterCount)
            };
            Add(record.Values, "emitterCount", health.EmitterCount);
            Add(record.Values, "requiredEmitterCount", health.RequiredEmitterCount);
            Add(record.Values, "missingRequiredEmitterCount", health.MissingRequiredEmitterCount);
            Add(record.Values, "singleRequests", health.SingleRequests);
            Add(record.Values, "batchRequests", health.BatchRequests);
            Add(record.Values, "hitCount", health.HitCount);
            Add(record.Values, "emptyCount", health.EmptyCount);
            Add(record.Values, "emptyRate", emptyRate.ToString("0.###", CultureInfo.InvariantCulture));
            Add(record.Values, "usedAttributeRegistry", health.UsedAttributeRegistry);
            insights.Records.Add(record);

            if (!health.HasEmitters || !health.IsOutputContractSatisfied)
            {
                var risk = new AnalysisRiskRecord
                {
                    Id = "moba.snapshot.contract-missing",
                    Category = "snapshot-contract",
                    Severity = SeverityCritical,
                    Message = "Snapshot output contract is not satisfied.",
                    Recommendation = "Register all required snapshot emitters before exporting battle analysis."
                };
                Add(risk.Values, "missingRequiredEmitterCount", health.MissingRequiredEmitterCount);
                if (health.MissingRequiredEmitters != null)
                {
                    for (var i = 0; i < health.MissingRequiredEmitters.Count; i++)
                    {
                        Add(risk.Values, "missing." + i, health.MissingRequiredEmitters[i]);
                    }
                }

                insights.Risks.Add(risk);
            }

            if (emptyRate > SnapshotEmptyRateWarning)
            {
                var risk = new AnalysisRiskRecord
                {
                    Id = "moba.snapshot.empty-rate",
                    Category = "snapshot-contract",
                    Severity = SeverityWarning,
                    Message = "Snapshot router returned empty results for a high share of requests.",
                    Recommendation = "Check frame timing, emitter filters and requested snapshot op codes."
                };
                Add(risk.Values, "emptyRate", emptyRate.ToString("0.###", CultureInfo.InvariantCulture));
                insights.Risks.Add(risk);
            }
        }

        private static void AppendThresholdProfile(AnalysisThresholdProfile profile)
        {
            if (profile == null) return;
            profile.Name = "moba-demo-default";
            profile.Version = "1.0.0";
            profile.Scope = "AbilityKit.Demo.Moba";
            AddRule(profile, "moba.trace.active-nodes.critical", MobaBattleDiagnosticMetric.TraceActiveRoots, "trace-health", SeverityCritical, ">=", TraceActiveNodesCriticalMin, "count", "Active trace nodes above this threshold indicate unfinished runtime scopes.", "trace", "runtime");
            AddRule(profile, "moba.snapshot.empty-rate.warning", MobaBattleDiagnosticMetric.SnapshotEmpty, "snapshot-contract", SeverityWarning, ">", SnapshotEmptyRateWarning, "ratio", "High snapshot empty rate indicates emitter, filter or frame timing mismatches.", "snapshot");
            AddRule(profile, "moba.runtime.active.warning", MobaBattleDiagnosticMetric.SkillRuntimeActive, "runtime-leak", SeverityWarning, ">", RuntimeActiveWarning, "count", "Temporary runtime objects should normally drain after battle analysis capture.", "runtime", "lifecycle");
            profile.Metadata.Add(new AnalysisKeyValue("mode", "demo-default"));
            profile.Metadata.Add(new AnalysisKeyValue("baselineReplaceable", "true"));
        }

        private static void AppendBaseline(AnalysisBaselineSection baseline, in MobaBattleDiagnosticsSnapshot snapshot)
        {
            if (baseline == null) return;
            baseline.BaselineId = "moba-demo-baseline";
            baseline.Source = "embedded-demo-baseline";
            baseline.ComparedAtUtc = DateTimeOffset.UtcNow.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

            var activeTotal = 0;
            long rejectedTotal = 0L;
            if (snapshot.Lifecycle != null)
            {
                for (var i = 0; i < snapshot.Lifecycle.Count; i++)
                {
                    activeTotal += snapshot.Lifecycle[i].ActiveCount;
                    rejectedTotal += snapshot.Lifecycle[i].RejectedCount;
                }
            }

            var totalRequests = snapshot.Snapshot.Health.SingleRequests + snapshot.Snapshot.Health.BatchRequests;
            var emptyRate = totalRequests <= 0L ? 0d : (double)snapshot.Snapshot.Health.EmptyCount / totalRequests;
            AddComparison(baseline, MobaBattleDiagnosticMetric.SkillRuntimeActive, "runtime-leak", "count", activeTotal, 0d, activeTotal > 0d ? SeverityWarning : SeverityInfo, "higher-is-worse", "Active runtime pressure against demo clean baseline.");
            AddComparison(baseline, MobaBattleDiagnosticMetric.SnapshotEmpty, "snapshot-contract", "ratio", emptyRate, 0.05d, emptyRate > SnapshotEmptyRateWarning ? SeverityWarning : SeverityInfo, "higher-is-worse", "Snapshot empty rate against demo operating baseline.");
            AddComparison(baseline, MobaBattleDiagnosticMetric.InputCommandRejected, "input", "count", rejectedTotal, 0d, rejectedTotal > 0L ? SeverityWarning : SeverityInfo, "higher-is-worse", "Rejected lifecycle/input pressure against demo baseline.");
            baseline.Metadata.Add(new AnalysisKeyValue("mode", "demo"));
            baseline.Metadata.Add(new AnalysisKeyValue("note", "Replace with historical project baseline in CI or production analysis."));
        }

        private static void AddRule(AnalysisThresholdProfile profile, string id, string metric, string category, string severity, string op, double value, string unit, string description, params string[] tags)
        {
            var rule = new AnalysisThresholdRule
            {
                Id = id,
                Metric = metric,
                Category = category,
                Severity = severity,
                Operator = op,
                Value = value,
                Unit = unit,
                Description = description
            };
            if (tags != null) rule.Tags.AddRange(tags);
            profile.Rules.Add(rule);
        }

        private static void AddThresholdEvaluation(AnalysisThresholdProfile profile, string ruleId, string metric, double actual, double expected, string unit, string severity)
        {
            if (profile == null) return;
            profile.Evaluations.Add(new AnalysisThresholdEvaluation
            {
                RuleId = ruleId,
                Metric = metric,
                Severity = severity,
                Status = severity == SeverityInfo ? "pass" : "fail",
                Actual = actual,
                Expected = expected,
                Delta = actual - expected,
                Unit = unit,
                Message = metric + " actual=" + actual.ToString("0.###", CultureInfo.InvariantCulture) + " expected=" + expected.ToString("0.###", CultureInfo.InvariantCulture)
            });
        }

        private static void AddComparison(AnalysisBaselineSection baseline, string metric, string category, string unit, double current, double expected, string severity, string direction, string summary)
        {
            var delta = current - expected;
            var deltaPercent = Math.Abs(expected) <= double.Epsilon ? (Math.Abs(delta) <= double.Epsilon ? 0d : 1d) : delta / expected;
            baseline.Metrics.Add(new AnalysisBaselineMetricComparison
            {
                Metric = metric,
                Category = category,
                Unit = unit,
                Current = current,
                Baseline = expected,
                Delta = delta,
                DeltaPercent = deltaPercent,
                Severity = severity,
                Direction = direction,
                Summary = summary
            });
        }

        private static bool IsSkillEffect(AnalysisTraceNode node)
        {
            return IsKind(node, MobaExecutionKind.SkillEffect) || IsKind(node, MobaExecutionKind.EffectExecution) || IsKind(node, MobaExecutionKind.EffectAction) || IsKind(node, MobaExecutionKind.SkillPhase);
        }

        private static bool IsDamage(AnalysisTraceNode node)
        {
            return IsKind(node, MobaExecutionKind.DamageAttack) || IsKind(node, MobaExecutionKind.DamageCalc) || IsKind(node, MobaExecutionKind.DamageApply);
        }

        private static bool IsProjectile(AnalysisTraceNode node)
        {
            return IsKind(node, MobaExecutionKind.ProjectileLaunch) || IsKind(node, MobaExecutionKind.ProjectileHit);
        }

        private static bool IsBuff(AnalysisTraceNode node)
        {
            return IsKind(node, MobaExecutionKind.BuffApply) || IsKind(node, MobaExecutionKind.BuffTick) || IsKind(node, MobaExecutionKind.BuffRemove);
        }

        private static bool IsPresentation(AnalysisTraceNode node)
        {
            return IsKind(node, MobaExecutionKind.PresentationPlay) || IsKind(node, MobaExecutionKind.PresentationStop);
        }

        private static bool IsKind(AnalysisTraceNode node, MobaExecutionKind kind)
        {
            if (node == null) return false;
            return node.Kind == (int)kind || string.Equals(node.KindName, kind.ToString(), StringComparison.Ordinal);
        }

        private static void AppendRanking(AnalysisInsightsSection insights, string name, string unit, Dictionary<string, int> counts, int limit)
        {
            if (insights == null || counts == null || counts.Count == 0) return;

            var items = new List<KeyValuePair<string, int>>(counts);
            items.Sort((a, b) => b.Value != a.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key, b.Key));
            var ranking = new AnalysisRanking { Name = name, Unit = unit ?? string.Empty };
            var count = Math.Min(Math.Max(0, limit), items.Count);
            for (var i = 0; i < count; i++)
            {
                ranking.Items.Add(new AnalysisRankingItem
                {
                    Key = items[i].Key,
                    Label = items[i].Key,
                    Value = items[i].Value
                });
            }

            insights.Rankings.Add(ranking);
        }

        private static void Increment(Dictionary<string, int> counts, string key)
        {
            if (counts == null) return;
            key = string.IsNullOrEmpty(key) ? "unknown" : key;
            counts.TryGetValue(key, out var value);
            counts[key] = value + 1;
        }

        private static string Label(string name, int id)
        {
            return !string.IsNullOrEmpty(name) ? name : id.ToString(CultureInfo.InvariantCulture);
        }

        private static void Add(List<AnalysisKeyValue> values, string key, int value) => Add(values, key, value.ToString(CultureInfo.InvariantCulture));
        private static void Add(List<AnalysisKeyValue> values, string key, long value) => Add(values, key, value.ToString(CultureInfo.InvariantCulture));
        private static void Add(List<AnalysisKeyValue> values, string key, bool value) => Add(values, key, value ? "true" : "false");
        private static void Add(List<AnalysisKeyValue> values, string key, string value)
        {
            if (values == null) return;
            values.Add(new AnalysisKeyValue(key, value));
        }
    }
}
