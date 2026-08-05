using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using UnityEngine;

namespace AbilityKit.Game.Test.UnitTest
{
    public static class MobaAcceptanceTraceExporter
    {
        public const string DefaultArtifactDirectory = "local/Logs/moba-acceptance";

        public static MobaAcceptanceTraceRecord[] CaptureTraceRecords(MobaSkillConfigTestHarness harness, string caseId)
        {
            if (harness == null) throw new ArgumentNullException(nameof(harness));

            var records = new List<MobaAcceptanceTraceRecord>(64);
            var seen = new HashSet<long>();
            foreach (MobaTraceKind kind in Enum.GetValues(typeof(MobaTraceKind)))
            {
                if (kind == MobaTraceKind.None) continue;

                foreach (var node in harness.Trace.GetNodesByKind((int)kind))
                {
                    if (!node.IsValid || !seen.Add(node.ContextId)) continue;
                    var metadata = node.Metadata;
                    var record = new MobaAcceptanceTraceRecord
                    {
                        caseId = caseId,
                        frame = node.CreatedFrame,
                        timeMs = (int)Math.Round(harness.FrameTime.FrameToTime(new AbilityKit.Ability.FrameSync.FrameIndex(node.CreatedFrame)) * 1000d),
                        rootId = node.RootId,
                        parentId = node.ParentId,
                        nodeId = node.ContextId,
                        kind = ((MobaTraceKind)node.Kind).ToString(),
                        kindValue = node.Kind,
                        configId = metadata != null ? metadata.ConfigId : 0,
                        sourceActorId = metadata != null ? metadata.SourceActorId : 0,
                        targetActorId = metadata != null ? metadata.TargetActorId : 0,
                        sourceId = metadata != null ? metadata.SourceId : 0,
                        targetId = metadata != null ? metadata.TargetId : 0,
                        originSourceId = metadata != null ? metadata.OriginSourceId : 0,
                        originTargetId = metadata != null ? metadata.OriginTargetId : 0,
                        originSource = metadata != null ? metadata.OriginSource : null,
                        originTarget = metadata != null ? metadata.OriginTarget : null,
                        isRoot = node.IsRoot,
                        isEnded = node.IsEnded,
                        endedFrame = node.EndedFrame,
                        endReason = node.EndReason,
                        childCount = node.ChildCount
                    };
                    ApplyTraceSemantics(harness, record);
                    records.Add(record);
                }
            }

            records.Sort(CompareTraceRecord);
            return records.ToArray();
        }

        public static MobaAcceptanceSummary BuildSummary(
            MobaSkillConfigTestHarness harness,
            MobaAcceptanceExpectation expectation,
            MobaAcceptanceTraceRecord[] records,
            string traceJsonlPath,
            string summaryJsonPath)
        {
            if (harness == null) throw new ArgumentNullException(nameof(harness));
            if (expectation == null) throw new ArgumentNullException(nameof(expectation));
            if (records == null) throw new ArgumentNullException(nameof(records));

            var effectRootId = FindRootId(records, "EffectExecution", expectation.config != null ? expectation.config.effectId : 0);
            var coverage = BuildCoverage(expectation, records, effectRootId);
            var diagnostics = BuildDiagnosticsSummary(harness, expectation.config != null ? expectation.config.triggerId : 0);
            var passed = coverage.allRequiredTraceNodesMatched
                && coverage.allForbiddenTraceNodesAbsent
                && coverage.allExpectedActionsExecuted
                && coverage.allRelationshipsSatisfied;
            return new MobaAcceptanceSummary
            {
                caseId = expectation.caseId,
                worldId = expectation.worldId,
                tickRate = expectation.tickRate,
                accelerated = expectation.accelerated,
                category = MobaAcceptanceRunner.ResolveCategory(expectation),
                tags = expectation.scenario != null && expectation.scenario.tags != null && expectation.scenario.tags.Length > 0 ? expectation.scenario.tags : expectation.tags,
                generatedFrom = expectation.generatedFrom,
                lastReviewedAt = expectation.lastReviewedAt,
                scenario = expectation.scenario,
                actors = expectation.scenario != null && expectation.scenario.actors != null && expectation.scenario.actors.Length > 0 ? expectation.scenario.actors : expectation.actors,
                setupActions = expectation.scenario != null && expectation.scenario.setupActions != null && expectation.scenario.setupActions.Length > 0 ? expectation.scenario.setupActions : expectation.setupActions,
                timeline = expectation.scenario != null && expectation.scenario.timeline != null && expectation.scenario.timeline.Length > 0 ? expectation.scenario.timeline : expectation.timeline,
                stateExpectations = expectation.scenario != null && expectation.scenario.stateExpectations != null && expectation.scenario.stateExpectations.Length > 0 ? expectation.scenario.stateExpectations : expectation.stateExpectations,
                contextExpectations = expectation.scenario != null && expectation.scenario.contextExpectations != null && expectation.scenario.contextExpectations.Length > 0 ? expectation.scenario.contextExpectations : expectation.contextExpectations,
                input = expectation.input,
                config = expectation.config,
                result = new MobaAcceptanceResult
                {
                    passed = passed,
                    skillCastTraceFound = Contains(records, "SkillCast", expectation.config != null ? expectation.config.skillId : 0, 0),
                    effectExecutionTraceFound = Contains(records, "EffectExecution", expectation.config != null ? expectation.config.effectId : 0, 0),
                    allExpectedActionsExecuted = coverage.allExpectedActionsExecuted,
                    projectileLaunched = expectation.config == null || expectation.config.expectedProjectile == null || Contains(records, "ProjectileLaunch", expectation.config.expectedProjectile.projectileId, effectRootId),
                    areaSpawned = ContainsKind(records, "AreaSpawn"),
                    buffApplied = ContainsKind(records, "BuffApply"),
                    effectRootId = effectRootId,
                    finalFrame = harness.FrameTime.Frame.Value,
                    finalTimeMs = (int)Math.Round(harness.FrameTime.Time * 1000d),
                    traceNodeCount = records.Length,
                    expectedTraceNodeCount = coverage.expectedTraceNodeCount,
                    matchedExpectedTraceNodeCount = coverage.matchedExpectedTraceNodeCount,
                    missingExpectedTraceNodeCount = coverage.missingExpectedTraceNodeCount,
                    expectedActionCount = coverage.expectedActionCount,
                    executedExpectedActionCount = coverage.executedExpectedActionCount,
                    expectedRelationshipCount = coverage.expectedRelationshipCount,
                    satisfiedRelationshipCount = coverage.satisfiedRelationshipCount
                },
                coverage = coverage,
                traceCounts = CountByKind(records),
                traceDictionary = BuildTraceDictionary(harness, records),
                traceDictionaryVersion = BuildTraceDictionaryVersion(harness),
                diagnostics = diagnostics,
                traceJsonlPath = NormalizePath(traceJsonlPath),
                summaryJsonPath = NormalizePath(summaryJsonPath)
            };
        }

        public static void Export(string artifactDirectory, MobaAcceptanceSummary summary, MobaAcceptanceTraceRecord[] records)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            if (records == null) throw new ArgumentNullException(nameof(records));

            var directory = string.IsNullOrEmpty(artifactDirectory) ? DefaultArtifactDirectory : artifactDirectory;
            Directory.CreateDirectory(directory);

            var tracePath = string.IsNullOrEmpty(summary.traceJsonlPath)
                ? Path.Combine(directory, summary.caseId + "_trace.jsonl")
                : summary.traceJsonlPath;
            var summaryPath = string.IsNullOrEmpty(summary.summaryJsonPath)
                ? Path.Combine(directory, summary.caseId + "_summary.json")
                : summary.summaryJsonPath;

            using (var writer = new StreamWriter(tracePath, false))
            {
                for (var i = 0; i < records.Length; i++)
                {
                    writer.WriteLine(JsonUtility.ToJson(records[i]));
                }
            }

            File.WriteAllText(summaryPath, JsonUtility.ToJson(summary, true));
        }

        public static string GetTraceJsonlPath(string artifactDirectory, string caseId)
        {
            return Path.Combine(string.IsNullOrEmpty(artifactDirectory) ? DefaultArtifactDirectory : artifactDirectory, caseId + "_trace.jsonl");
        }

        public static string GetSummaryJsonPath(string artifactDirectory, string caseId)
        {
            return Path.Combine(string.IsNullOrEmpty(artifactDirectory) ? DefaultArtifactDirectory : artifactDirectory, caseId + "_summary.json");
        }

        private static int CompareTraceRecord(MobaAcceptanceTraceRecord x, MobaAcceptanceTraceRecord y)
        {
            var rootCompare = x.rootId.CompareTo(y.rootId);
            if (rootCompare != 0) return rootCompare;
            return x.nodeId.CompareTo(y.nodeId);
        }

        private static void ApplyTraceSemantics(MobaSkillConfigTestHarness harness, MobaAcceptanceTraceRecord record)
        {
            if (record == null) return;
            var configSource = string.Empty;
            var configName = ResolveConfigName(harness != null ? harness.Config : null, record.kind, record.configId, out configSource);
            record.configSource = configSource;
            record.semanticVersion = BuildTraceDictionaryVersion(harness);
            record.configLabel = BuildConfigLabel(record.kind, record.configId, configName);
            record.runtimeLabel = BuildRuntimeLabel(record);
            record.sourceActorLabel = ResolveActorLabel(harness, record.sourceActorId, "source");
            record.targetActorLabel = ResolveActorLabel(harness, record.targetActorId, "target");
            record.actorLabel = !string.IsNullOrEmpty(record.sourceActorLabel) ? record.sourceActorLabel : record.targetActorLabel;
            record.displayName = BuildTraceDisplayName(record.kind, record.configLabel, record.runtimeLabel);
        }

        private static MobaAcceptanceTraceDictionaryEntry[] BuildTraceDictionary(MobaSkillConfigTestHarness harness, MobaAcceptanceTraceRecord[] records)
        {
            var entries = new Dictionary<string, MobaAcceptanceTraceDictionaryEntry>(StringComparer.Ordinal);
            var sourceVersion = BuildTraceDictionaryVersion(harness);
            if (records == null) return Array.Empty<MobaAcceptanceTraceDictionaryEntry>();

            for (var i = 0; i < records.Length; i++)
            {
                var record = records[i];
                if (record == null) continue;
                AddDictionaryEntry(entries, "trace-kind", record.kindValue.ToString(), record.kind, record.kind, "MobaTraceKind", sourceVersion);
                if (record.configId > 0) AddDictionaryEntry(entries, "config", record.configId.ToString(), record.configLabel, record.configLabel, record.configSource, sourceVersion);
                if (record.sourceActorId > 0) AddDictionaryEntry(entries, "actor", record.sourceActorId.ToString(), record.sourceActorLabel, record.sourceActorLabel, "scenario/runtime", sourceVersion);
                if (record.targetActorId > 0) AddDictionaryEntry(entries, "actor", record.targetActorId.ToString(), record.targetActorLabel, record.targetActorLabel, "scenario/runtime", sourceVersion);
            }

            var result = new List<MobaAcceptanceTraceDictionaryEntry>(entries.Values);
            result.Sort((a, b) => string.CompareOrdinal(a.key, b.key));
            return result.ToArray();
        }

        private static void AddDictionaryEntry(Dictionary<string, MobaAcceptanceTraceDictionaryEntry> entries, string kind, string id, string name, string label, string source, string sourceVersion)
        {
            if (string.IsNullOrEmpty(id) || id == "0") return;
            var key = kind + ":" + id;
            if (entries.ContainsKey(key)) return;
            entries[key] = new MobaAcceptanceTraceDictionaryEntry
            {
                key = key,
                kind = kind,
                id = id,
                name = string.IsNullOrEmpty(name) ? id : name,
                label = string.IsNullOrEmpty(label) ? kind + " #" + id : label,
                source = string.IsNullOrEmpty(source) ? "trace" : source,
                sourceVersion = sourceVersion
            };
        }

        private static string BuildTraceDictionaryVersion(MobaSkillConfigTestHarness harness)
        {
            var version = harness != null && harness.Config != null ? harness.Config.Version : 0;
            return "moba-trace-dictionary/v1 config=" + version;
        }

        private static string ResolveConfigName(MobaConfigDatabase config, string kind, int configId, out string source)
        {
            source = string.Empty;
            if (config == null || configId <= 0) return string.Empty;
            var normalizedKind = kind ?? string.Empty;

            if ((normalizedKind.IndexOf("Skill", StringComparison.OrdinalIgnoreCase) >= 0 || normalizedKind.IndexOf("Cast", StringComparison.OrdinalIgnoreCase) >= 0)
                && config.TryGetSkill(configId, out var skill))
            {
                source = "SkillMO";
                return ReadName(skill);
            }

            if (normalizedKind.IndexOf("Buff", StringComparison.OrdinalIgnoreCase) >= 0 && config.TryGetBuff(configId, out var buff))
            {
                source = "BuffMO";
                return ReadName(buff);
            }

            if (normalizedKind.IndexOf("Projectile", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (config.TryGetProjectile(configId, out var projectile))
                {
                    source = "ProjectileMO";
                    return ReadName(projectile);
                }

                if (config.TryGetProjectileLauncher(configId, out var launcher))
                {
                    source = "ProjectileLauncherMO";
                    return ReadName(launcher);
                }
            }

            if (normalizedKind.IndexOf("Area", StringComparison.OrdinalIgnoreCase) >= 0 && config.TryGetAoe(configId, out var area))
            {
                source = "AoeMO";
                return ReadName(area);
            }

            if (normalizedKind.IndexOf("Summon", StringComparison.OrdinalIgnoreCase) >= 0 && config.TryGetSummon(configId, out var summon))
            {
                source = "SummonMO";
                return ReadName(summon);
            }

            source = "trace-metadata";
            return string.Empty;
        }

        private static string BuildConfigLabel(string kind, int configId, string configName)
        {
            if (configId <= 0) return string.Empty;
            var prefix = string.IsNullOrEmpty(kind) ? "配置" : kind;
            return string.IsNullOrEmpty(configName) ? prefix + " #" + configId : prefix + " " + configName + " (#" + configId + ")";
        }

        private static string BuildRuntimeLabel(MobaAcceptanceTraceRecord record)
        {
            if (record == null) return string.Empty;
            if (!string.IsNullOrEmpty(record.originSource) || !string.IsNullOrEmpty(record.originTarget))
            {
                return (record.originSource ?? string.Empty) + " -> " + (record.originTarget ?? string.Empty);
            }

            return (record.kind ?? "trace") + " context #" + record.nodeId;
        }

        private static string BuildTraceDisplayName(string kind, string configLabel, string runtimeLabel)
        {
            if (!string.IsNullOrEmpty(configLabel)) return configLabel;
            if (!string.IsNullOrEmpty(runtimeLabel)) return runtimeLabel;
            return string.IsNullOrEmpty(kind) ? "Trace Node" : kind;
        }

        private static string ResolveActorLabel(MobaSkillConfigTestHarness harness, long actorId, string role)
        {
            if (actorId <= 0) return string.Empty;
            var alias = FindActorAlias(harness, actorId);
            var prefix = string.Equals(role, "target", StringComparison.OrdinalIgnoreCase) ? "目标" : "来源";
            return string.IsNullOrEmpty(alias) ? prefix + "角色 #" + actorId : prefix + "角色 " + alias + " (#" + actorId + ")";
        }

        private static string FindActorAlias(MobaSkillConfigTestHarness harness, long actorId)
        {
            if (harness == null || harness.ActorAliases == null || actorId <= 0) return string.Empty;
            foreach (var pair in harness.ActorAliases)
            {
                if (pair.Value == actorId) return pair.Key;
            }

            return string.Empty;
        }

        private static string ReadName(object value)
        {
            if (value == null) return string.Empty;
            var property = value.GetType().GetProperty("Name", BindingFlags.Instance | BindingFlags.Public);
            return property != null ? property.GetValue(value, null) as string ?? string.Empty : string.Empty;
        }

        private static bool Contains(MobaAcceptanceTraceRecord[] records, string kind, int configId, long rootId)
        {
            for (var i = 0; i < records.Length; i++)
            {
                var record = records[i];
                if (record.kind == kind && record.configId == configId && (rootId <= 0 || record.rootId == rootId)) return true;
            }

            return false;
        }

        private static long FindRootId(MobaAcceptanceTraceRecord[] records, string kind, int configId)
        {
            for (var i = 0; i < records.Length; i++)
            {
                var record = records[i];
                if (record.kind == kind && record.configId == configId) return record.rootId;
            }

            return 0;
        }

        private static bool ContainsAllActions(MobaAcceptanceTraceRecord[] records, MobaAcceptanceExpectation expectation, long effectRootId)
        {
            var actions = expectation.config != null ? expectation.config.expectedActions : null;
            if (actions == null) return true;

            for (var i = 0; i < actions.Length; i++)
            {
                if (!Contains(records, "EffectAction", actions[i].actionId, effectRootId)) return false;
            }

            return true;
        }

        private static MobaAcceptanceCoverageSummary BuildCoverage(MobaAcceptanceExpectation expectation, MobaAcceptanceTraceRecord[] records, long effectRootId)
        {
            var required = expectation.mustContain;
            var forbidden = expectation.mustNotContain;
            var actions = expectation.config != null ? expectation.config.expectedActions : null;
            var relationships = expectation.relationships;
            var coverage = new MobaAcceptanceCoverageSummary
            {
                expectedTraceNodeCount = required != null ? required.Length : 0,
                forbiddenTraceNodeCount = forbidden != null ? forbidden.Length : 0,
                expectedActionCount = actions != null ? actions.Length : 0,
                expectedRelationshipCount = relationships != null ? relationships.Length : 0,
                expectedStateCount = CountStateExpectations(expectation),
                expectedContextCount = CountContextExpectations(expectation)
            };

            var missingTraceNodes = new List<string>();
            if (required != null)
            {
                for (var i = 0; i < required.Length; i++)
                {
                    var item = required[i];
                    var count = Count(records, item.kind, item.configId, item.underEffectId > 0 ? effectRootId : 0);
                    var minCount = item.minCount > 0 ? item.minCount : 1;
                    if (count >= minCount && (item.maxCount <= 0 || count <= item.maxCount)) coverage.matchedExpectedTraceNodeCount++;
                    else missingTraceNodes.Add(FormatTraceExpectation(item, count));
                }
            }

            var unexpectedTraceNodes = new List<string>();
            if (forbidden != null)
            {
                for (var i = 0; i < forbidden.Length; i++)
                {
                    var item = forbidden[i];
                    var count = Count(records, item.kind, item.configId, item.underEffectId > 0 ? effectRootId : 0);
                    if (count > 0) unexpectedTraceNodes.Add(FormatTraceExpectation(item, count));
                }
            }

            var missingActions = new List<string>();
            if (actions != null)
            {
                for (var i = 0; i < actions.Length; i++)
                {
                    if (Contains(records, "EffectAction", actions[i].actionId, effectRootId)) coverage.executedExpectedActionCount++;
                    else missingActions.Add(actions[i].type + "(" + actions[i].actionId + ")");
                }
            }

            var missingRelationships = new List<string>();
            if (relationships != null)
            {
                for (var i = 0; i < relationships.Length; i++)
                {
                    if (HasRelationship(records, relationships[i])) coverage.satisfiedRelationshipCount++;
                    else missingRelationships.Add(relationships[i].parentKind + "(" + relationships[i].parentConfigId + ")->" + relationships[i].childKind + "(" + relationships[i].childConfigId + ")");
                }
            }

            coverage.missingExpectedTraceNodeCount = missingTraceNodes.Count;
            coverage.unexpectedForbiddenTraceNodeCount = unexpectedTraceNodes.Count;
            coverage.allRequiredTraceNodesMatched = coverage.missingExpectedTraceNodeCount == 0;
            coverage.allForbiddenTraceNodesAbsent = coverage.unexpectedForbiddenTraceNodeCount == 0;
            coverage.allExpectedActionsExecuted = coverage.executedExpectedActionCount == coverage.expectedActionCount;
            coverage.allRelationshipsSatisfied = coverage.satisfiedRelationshipCount == coverage.expectedRelationshipCount;
            coverage.missingTraceNodes = string.Join(",", missingTraceNodes.ToArray());
            coverage.unexpectedTraceNodes = string.Join(",", unexpectedTraceNodes.ToArray());
            coverage.missingActions = string.Join(",", missingActions.ToArray());
            coverage.missingRelationships = string.Join(",", missingRelationships.ToArray());
            return coverage;
        }

        private static int CountStateExpectations(MobaAcceptanceExpectation expectation)
        {
            var count = expectation.stateExpectations != null ? expectation.stateExpectations.Length : 0;
            if (expectation.scenario != null && expectation.scenario.stateExpectations != null) count += expectation.scenario.stateExpectations.Length;
            return count;
        }

        private static MobaAcceptanceDiagnosticsSummary BuildDiagnosticsSummary(MobaSkillConfigTestHarness harness, int triggerId)
        {
            if (harness == null || harness.World == null || harness.World.Services == null) return null;

            var warningCount = 0;
            var exportedWarnings = Array.Empty<MobaAcceptanceDiagnosticWarning>();
            var rejectionSummary = string.Empty;
            if (harness.World.Services.TryResolve<IMobaBattleDiagnosticsService>(out var diagnostics) && diagnostics != null)
            {
                var warnings = diagnostics.GetWarningsSnapshot();
                if (warnings != null && warnings.Count > 0)
                {
                    exportedWarnings = new MobaAcceptanceDiagnosticWarning[warnings.Count];
                    var rejectionBuilder = new StringBuilder();
                    var rejectionCount = 0;
                    for (var i = 0; i < warnings.Count; i++)
                    {
                        var warning = warnings[i];
                        exportedWarnings[i] = new MobaAcceptanceDiagnosticWarning
                        {
                            key = warning.Key,
                            message = warning.Message,
                            count = warning.Count,
                            suppressedAtLimit = warning.SuppressedAtLimit
                        };

                        if (string.IsNullOrEmpty(warning.Key)
                            || warning.Key.IndexOf("plan.action.rejected.", StringComparison.OrdinalIgnoreCase) < 0
                            || string.IsNullOrEmpty(warning.Message))
                        {
                            continue;
                        }

                        if (rejectionCount > 0) rejectionBuilder.Append(" | ");
                        rejectionBuilder.Append(warning.Message);
                        rejectionCount++;
                    }

                    warningCount = exportedWarnings.Length;
                    rejectionSummary = rejectionBuilder.ToString();
                }
            }

            return new MobaAcceptanceDiagnosticsSummary
            {
                warningCount = warningCount,
                warnings = exportedWarnings,
                planActionRejections = rejectionSummary,
                triggerRuntimeSnapshot = BuildTriggerRuntimeSnapshot(harness, triggerId)
            };
        }

        private static string BuildTriggerRuntimeSnapshot(MobaSkillConfigTestHarness harness, int triggerId)
        {
            if (harness == null || triggerId <= 0)
            {
                return string.Empty;
            }

            var summary = new StringBuilder();
            summary.Append("triggerId=").Append(triggerId);

            if (!harness.TriggerPlans.TryGetRecordByTriggerId(triggerId, out TriggerPlanJsonDatabase.Record record))
            {
                summary.Append(" record=<missing>");
                return summary.ToString();
            }

            summary.Append(" eventId=").Append(record.EventId);
            summary.Append(" scope=").Append(record.Scope);

            var actions = record.Plan.Actions;
            summary.Append(" planActionCount=").Append(actions != null ? actions.Length : 0);
            summary.Append(" actions=[");
            if (actions != null)
            {
                for (var i = 0; i < actions.Length; i++)
                {
                    if (i > 0) summary.Append(", ");
                    summary.Append(i)
                        .Append(":id=")
                        .Append(actions[i].Id.Value)
                        .Append("/arity=")
                        .Append(actions[i].Arguments.Arity)
                        .Append("/named=")
                        .Append(actions[i].Arguments.HasNamedArgs);
                }
            }
            summary.Append(']');

            summary.Append(" executionRoot=");
            summary.Append(DescribeExecutable(record.ExecutionRoot, 0));
            return summary.ToString();
        }

        private static string DescribeExecutable(ITriggerPlanExecutable executable, int depth)
        {
            if (executable == null)
            {
                return "<null>";
            }

            var summary = new StringBuilder();
            summary.Append(executable.GetType().Name)
                .Append("{name=")
                .Append(executable.Name)
                .Append(",kind=")
                .Append(executable.Kind)
                .Append(",weight=")
                .Append(executable.Weight);

            if (executable is ActionCallTriggerPlanExecutable actionExecutable)
            {
                summary.Append(",actionId=")
                    .Append(actionExecutable.Action.Id.Value)
                    .Append(",arity=")
                    .Append(actionExecutable.Action.Arguments.Arity)
                    .Append(",named=")
                    .Append(actionExecutable.Action.Arguments.HasNamedArgs)
                    .Append('}');
                return summary.ToString();
            }

            if (executable is CompositeTriggerPlanExecutableBase composite)
            {
                summary.Append(",children=[");
                for (var i = 0; i < composite.Children.Count; i++)
                {
                    if (i > 0) summary.Append(", ");
                    if (depth >= 3)
                    {
                        summary.Append("...");
                        break;
                    }

                    summary.Append(DescribeExecutable(composite.Children[i], depth + 1));
                }
                summary.Append("]}");
                return summary.ToString();
            }

            summary.Append('}');
            return summary.ToString();
        }

        private static int CountContextExpectations(MobaAcceptanceExpectation expectation)
        {
            var count = expectation.contextExpectations != null ? expectation.contextExpectations.Length : 0;
            if (expectation.scenario != null && expectation.scenario.contextExpectations != null) count += expectation.scenario.contextExpectations.Length;
            return count;
        }

        private static string FormatTraceExpectation(MobaAcceptanceTraceExpectation expectation, int actualCount)
        {
            return expectation.kind + "(" + expectation.configId + ",underEffectId=" + expectation.underEffectId + ",actual=" + actualCount + ")";
        }

        private static bool ContainsKind(MobaAcceptanceTraceRecord[] records, string kind)
        {
            if (records == null) return false;
            for (var i = 0; i < records.Length; i++)
            {
                if (records[i].kind == kind) return true;
            }

            return false;
        }

        private static int Count(MobaAcceptanceTraceRecord[] records, string kind, int configId, long rootId)
        {
            if (records == null) return 0;
            var count = 0;
            for (var i = 0; i < records.Length; i++)
            {
                var record = records[i];
                if (record.kind == kind && record.configId == configId && (rootId <= 0 || record.rootId == rootId)) count++;
            }

            return count;
        }

        private static bool HasRelationship(MobaAcceptanceTraceRecord[] records, MobaAcceptanceRelationshipExpectation relationship)
        {
            if (records == null || relationship == null) return false;
            for (var i = 0; i < records.Length; i++)
            {
                var parent = records[i];
                if (parent.kind != relationship.parentKind || parent.configId != relationship.parentConfigId) continue;
                for (var j = 0; j < records.Length; j++)
                {
                    var child = records[j];
                    if (child.kind == relationship.childKind && child.configId == relationship.childConfigId && child.rootId == parent.rootId) return true;
                }
            }

            return false;
        }

        private static MobaAcceptanceTraceCount[] CountByKind(MobaAcceptanceTraceRecord[] records)
        {
            var counts = new Dictionary<string, int>();
            for (var i = 0; i < records.Length; i++)
            {
                var kind = records[i].kind ?? string.Empty;
                counts.TryGetValue(kind, out var count);
                counts[kind] = count + 1;
            }

            var result = new List<MobaAcceptanceTraceCount>(counts.Count);
            foreach (var pair in counts)
            {
                result.Add(new MobaAcceptanceTraceCount { kind = pair.Key, count = pair.Value });
            }

            result.Sort((a, b) => string.CompareOrdinal(a.kind, b.kind));
            return result.ToArray();
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
        }
    }
}
