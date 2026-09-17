using System;
using System.Collections.Generic;
using System.Globalization;
using AbilityKit.Diagnostics.Analysis;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 构建面向 Web 的 MOBA 分析产物时使用的选项。
    /// </summary>
    public readonly struct MobaAnalysisBuildOptions
    {
        public MobaAnalysisBuildOptions(
            string project,
            string scenario,
            string sessionId = null,
            string producer = null,
            int startFrame = 0,
            int endFrame = 0,
            int maxTraceNodes = 0,
            int maxTraceDepth = 0,
            bool activeTraceOnly = false)
        {
            Project = project ?? string.Empty;
            Scenario = scenario ?? string.Empty;
            SessionId = sessionId ?? string.Empty;
            Producer = producer ?? string.Empty;
            StartFrame = startFrame;
            EndFrame = endFrame;
            MaxTraceNodes = maxTraceNodes;
            MaxTraceDepth = maxTraceDepth;
            ActiveTraceOnly = activeTraceOnly;
        }

        public string Project { get; }
        public string Scenario { get; }
        public string SessionId { get; }
        public string Producer { get; }
        public int StartFrame { get; }
        public int EndFrame { get; }
        public int MaxTraceNodes { get; }
        public int MaxTraceDepth { get; }
        public bool ActiveTraceOnly { get; }
    }

    /// <summary>
    /// 从 MOBA diagnostics 和 trace 快照构建统一的 abilitykit-analysis.v1 产物。
    /// </summary>
    public static class MobaAnalysisArtifactBuilder
    {
        public static AbilityKitAnalysisArtifact FromSnapshot(
            in MobaBattleDiagnosticsSnapshot snapshot,
            MobaTraceRegistry trace = null,
            in MobaAnalysisBuildOptions options = default)
        {
            var generatedAt = DateTimeOffset.UtcNow;
            var sessionId = !string.IsNullOrEmpty(options.SessionId)
                ? options.SessionId
                : !string.IsNullOrEmpty(snapshot.Profiler.SessionId)
                    ? snapshot.Profiler.SessionId
                    : "moba-" + generatedAt.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

            var artifact = new AbilityKitAnalysisArtifact
            {
                SchemaVersion = AbilityKitAnalysisSchema.Version,
                Session = new AnalysisSessionInfo
                {
                    SessionId = sessionId,
                    Project = string.IsNullOrEmpty(options.Project) ? "AbilityKit.Demo.Moba" : options.Project,
                    Scenario = options.Scenario ?? string.Empty,
                    Producer = string.IsNullOrEmpty(options.Producer) ? "AbilityKit.Demo.Moba" : options.Producer,
                    GeneratedAtUtc = generatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                    GeneratedAtUnixMs = generatedAt.ToUnixTimeMilliseconds()
                },
                Time = new AnalysisTimeAxis
                {
                    StartUnixMs = snapshot.Timestamp,
                    EndUnixMs = snapshot.Timestamp,
                    StartFrame = options.StartFrame,
                    EndFrame = options.EndFrame,
                    DurationMs = snapshot.Profiler.Root != null ? Math.Max(0L, snapshot.Profiler.Root.EndTimestamp - snapshot.Profiler.Root.StartTimestamp) : 0d,
                    Clock = "unix-ms"
                },
                Profiler = snapshot.HasProfilerSnapshot ? AnalysisProfilerBuilder.FromSnapshot(snapshot.Profiler) : new AnalysisProfilerSection()
            };

            AppendDictionaries(artifact.Dictionaries);
            AppendTrace(artifact.Trace, trace, options);
            AppendDiagnostics(artifact.Diagnostics, snapshot);
            AppendRuntime(artifact.Runtime, snapshot);
            MobaAnalysisDerivedSummaryBuilder.AppendTo(artifact, snapshot);
            AppendMetadata(artifact.Metadata, snapshot, trace, options);
            return artifact;
        }

        private static void AppendDictionaries(AnalysisDictionaries dictionaries)
        {
            AppendEnumDictionary<MobaTraceKind>(dictionaries.TraceKinds);
            AppendEnumDictionary<MobaTraceEndReason>(dictionaries.EndReasons);
            MobaAnalysisMetricCatalog.AppendTo(dictionaries.MetricCatalog);
            dictionaries.DiagnosticCategories.Add(new AnalysisDictionaryEntry { Id = 1, Name = "warning", Description = "MOBA diagnostic warnings." });
            dictionaries.DiagnosticCategories.Add(new AnalysisDictionaryEntry { Id = 2, Name = "exception", Description = "MOBA diagnostic exceptions." });
            dictionaries.DiagnosticCategories.Add(new AnalysisDictionaryEntry { Id = 3, Name = "aggregate", Description = "MOBA diagnostic aggregate counters and gauges." });
        }

        private static void AppendEnumDictionary<TEnum>(List<AnalysisDictionaryEntry> entries) where TEnum : Enum
        {
            var values = Enum.GetValues(typeof(TEnum));
            for (var i = 0; i < values.Length; i++)
            {
                var value = values.GetValue(i);
                entries.Add(new AnalysisDictionaryEntry
                {
                    Id = Convert.ToInt32(value, CultureInfo.InvariantCulture),
                    Name = value.ToString(),
                    Description = typeof(TEnum).Name + "." + value
                });
            }
        }

        private static void AppendTrace(AnalysisTraceSection section, MobaTraceRegistry trace, in MobaAnalysisBuildOptions options)
        {
            if (trace == null) return;

            var exportOptions = new TraceExportOptions(
                Math.Max(0, options.MaxTraceNodes),
                options.ActiveTraceOnly,
                true,
                Math.Max(0, options.MaxTraceDepth),
                TraceExportOrder.TreePreOrder);
            var roots = trace.ExportRoots(exportOptions);
            if (roots == null) return;

            for (var i = 0; i < roots.Count; i++)
            {
                var root = roots[i];
                var analysisRoot = new AnalysisTraceRoot
                {
                    RootId = root.RootId,
                    Truncated = root.Truncated
                };

                if (root.Truncated) section.Truncated = true;
                if (root.Nodes != null)
                {
                    for (var j = 0; j < root.Nodes.Count; j++)
                    {
                        var node = root.Nodes[j];
                        analysisRoot.Nodes.Add(ToAnalysisNode(in node));
                        if (node.ParentId != 0L)
                        {
                            section.Edges.Add(new AnalysisTraceEdge
                            {
                                Kind = "parent-child",
                                FromContextId = node.ParentId,
                                ToContextId = node.ContextId,
                                Label = node.KindName ?? string.Empty
                            });
                        }
                    }
                }

                section.Roots.Add(analysisRoot);
            }
        }

        private static AnalysisTraceNode ToAnalysisNode(in TraceNodeExportDto node)
        {
            return new AnalysisTraceNode
            {
                ContextId = node.ContextId,
                RootId = node.RootId,
                ParentId = node.ParentId,
                Kind = node.Kind,
                KindName = node.KindName ?? string.Empty,
                EndedFrame = node.EndedFrame,
                EndReason = node.EndReason,
                EndReasonName = ToEndReasonName(node.EndReason),
                ChildCount = node.ChildCount,
                IsRoot = node.IsRoot,
                IsEnded = node.IsEnded,
                Metadata = ToAnalysisMetadata(node.Metadata as MobaTraceMetadata)
            };
        }

        private static AnalysisTraceMetadata ToAnalysisMetadata(MobaTraceMetadata metadata)
        {
            var result = new AnalysisTraceMetadata();
            if (metadata == null) return result;

            result.ConfigId = metadata.ConfigId;
            result.SourceActorId = metadata.SourceActorId;
            result.TargetActorId = metadata.TargetActorId;
            result.SourceId = metadata.SourceId;
            result.TargetId = metadata.TargetId;
            result.OriginSourceId = metadata.OriginSourceId;
            result.OriginTargetId = metadata.OriginTargetId;
            result.OriginSource = metadata.OriginSource ?? string.Empty;
            result.OriginTarget = metadata.OriginTarget ?? string.Empty;
            result.Display = metadata.ToDisplayString();
            result.Properties.Add(new AnalysisKeyValue("traceKind", metadata.TraceKind.ToString()));
            result.Properties.Add(new AnalysisKeyValue("rootId", ToString(metadata.RootId)));
            result.Properties.Add(new AnalysisKeyValue("parentId", ToString(metadata.ParentId)));
            if (metadata.OriginKind != MobaTraceKind.None)
            {
                result.Properties.Add(new AnalysisKeyValue("originKind", metadata.OriginKind.ToString()));
                result.Properties.Add(new AnalysisKeyValue("originConfigId", ToString(metadata.OriginConfigId)));
            }
            return result;
        }

        private static void AppendDiagnostics(AnalysisDiagnosticsSection section, in MobaBattleDiagnosticsSnapshot snapshot)
        {
            if (snapshot.Warnings != null)
            {
                for (var i = 0; i < snapshot.Warnings.Count; i++)
                {
                    var warning = snapshot.Warnings[i];
                    section.Warnings.Add(new AnalysisDiagnosticRecord
                    {
                        Key = warning.Key,
                        Category = "warning",
                        Message = warning.Message,
                        Count = warning.Count,
                        SuppressedAtLimit = warning.SuppressedAtLimit,
                        Correlation = ToCorrelation(warning.Context)
                    });
                }
            }

            if (snapshot.Exceptions != null)
            {
                for (var i = 0; i < snapshot.Exceptions.Count; i++)
                {
                    var exception = snapshot.Exceptions[i];
                    section.Exceptions.Add(new AnalysisDiagnosticRecord
                    {
                        Key = exception.Key,
                        Category = "exception",
                        Message = exception.Message,
                        ExceptionType = exception.ExceptionType,
                        Count = exception.Count,
                        SuppressedAtLimit = exception.SuppressedAtLimit,
                        Correlation = ToCorrelation(exception.Context)
                    });
                }
            }

            section.Aggregates.Add(CreateInputAggregate(snapshot.Input));
            section.Aggregates.Add(CreateSnapshotAggregate(snapshot.Snapshot.Health));
        }

        private static void AppendRuntime(AnalysisRuntimeSection section, in MobaBattleDiagnosticsSnapshot snapshot)
        {
            if (snapshot.Lifecycle != null)
            {
                for (var i = 0; i < snapshot.Lifecycle.Count; i++)
                {
                    var health = snapshot.Lifecycle[i];
                    var record = new AnalysisRuntimeRecord
                    {
                        Kind = "temporary-entity-lifecycle",
                        Name = health.Kind.ToString()
                    };
                    Add(record.Values, "activeCount", health.ActiveCount);
                    Add(record.Values, "spawnedCount", health.SpawnedCount);
                    Add(record.Values, "despawnedCount", health.DespawnedCount);
                    Add(record.Values, "rejectedCount", health.RejectedCount);
                    Add(record.Values, "replacedCount", health.ReplacedCount);
                    Add(record.Values, "tickEventCount", health.TickEventCount);
                    Add(record.Values, "hitEventCount", health.HitEventCount);
                    Add(record.Values, "enterEventCount", health.EnterEventCount);
                    Add(record.Values, "exitEventCount", health.ExitEventCount);
                    Add(record.Values, "expireEventCount", health.ExpireEventCount);
                    Add(record.Values, "lastFrame", health.LastFrame);
                    section.Records.Add(record);
                }
            }

            AppendSnapshotRuntimeRecords(section, snapshot.Snapshot.Health);
        }

        private static void AppendSnapshotRuntimeRecords(AnalysisRuntimeSection section, in MobaSnapshotRouterHealth health)
        {
            if (health.Emitters != null)
            {
                for (var i = 0; i < health.Emitters.Count; i++)
                {
                    var emitter = health.Emitters[i];
                    section.Records.Add(new AnalysisRuntimeRecord
                    {
                        Kind = "snapshot-emitter",
                        Name = emitter.Name ?? string.Empty
                    });
                }
            }

            if (health.MissingRequiredEmitters != null)
            {
                for (var i = 0; i < health.MissingRequiredEmitters.Count; i++)
                {
                    section.Records.Add(new AnalysisRuntimeRecord
                    {
                        Kind = "snapshot-missing-required-emitter",
                        Name = health.MissingRequiredEmitters[i] ?? string.Empty
                    });
                }
            }
        }

        private static AnalysisDiagnosticAggregate CreateInputAggregate(in MobaInputDiagnosticAggregate input)
        {
            var aggregate = new AnalysisDiagnosticAggregate { Name = "moba.input" };
            Add(aggregate.Values, "acceptedBatches", input.AcceptedBatches);
            Add(aggregate.Values, "acceptedCommands", input.AcceptedCommands);
            Add(aggregate.Values, "handledCommands", input.HandledCommands);
            Add(aggregate.Values, "rejectedCommands", input.RejectedCommands);
            Add(aggregate.Values, "commandExceptions", input.CommandExceptions);
            return aggregate;
        }

        private static AnalysisDiagnosticAggregate CreateSnapshotAggregate(in MobaSnapshotRouterHealth health)
        {
            var aggregate = new AnalysisDiagnosticAggregate { Name = "moba.snapshot" };
            Add(aggregate.Values, "emitterCount", health.EmitterCount);
            Add(aggregate.Values, "requiredEmitterCount", health.RequiredEmitterCount);
            Add(aggregate.Values, "missingRequiredEmitterCount", health.MissingRequiredEmitterCount);
            Add(aggregate.Values, "singleRequests", health.SingleRequests);
            Add(aggregate.Values, "batchRequests", health.BatchRequests);
            Add(aggregate.Values, "hitCount", health.HitCount);
            Add(aggregate.Values, "emptyCount", health.EmptyCount);
            Add(aggregate.Values, "lastFrame", health.LastFrame);
            Add(aggregate.Values, "lastSnapshotOpCode", health.LastSnapshotOpCode);
            Add(aggregate.Values, "lastBatchSnapshotCount", health.LastBatchSnapshotCount);
            Add(aggregate.Values, "usedAttributeRegistry", health.UsedAttributeRegistry);
            Add(aggregate.Values, "isOutputContractSatisfied", health.IsOutputContractSatisfied);
            return aggregate;
        }

        private static AnalysisRuntimeCorrelation ToCorrelation(in MobaBattleDiagnosticContext context)
        {
            return new AnalysisRuntimeCorrelation
            {
                RootContextId = context.RootContextId,
                SourceContextId = context.SourceContextId,
                RuntimeId = context.RuntimeHandle.IsValid ? context.RuntimeHandle.RuntimeId : 0L,
                RuntimeRootContextId = context.RuntimeHandle.RootTraceContextId,
                ActorId = context.ActorId,
                SkillId = context.SkillId,
                Detail = context.Detail ?? string.Empty
            };
        }

        private static void AppendMetadata(List<AnalysisKeyValue> metadata, in MobaBattleDiagnosticsSnapshot snapshot, MobaTraceRegistry trace, in MobaAnalysisBuildOptions options)
        {
            Add(metadata, "artifact.kind", "moba-battle-analysis");
            Add(metadata, "trace.included", trace != null);
            Add(metadata, "trace.activeOnly", options.ActiveTraceOnly);
            Add(metadata, "trace.maxNodes", options.MaxTraceNodes);
            Add(metadata, "trace.maxDepth", options.MaxTraceDepth);
            Add(metadata, "warnings", snapshot.Warnings != null ? snapshot.Warnings.Count : 0);
            Add(metadata, "exceptions", snapshot.Exceptions != null ? snapshot.Exceptions.Count : 0);
            Add(metadata, "lifecycle.records", snapshot.Lifecycle != null ? snapshot.Lifecycle.Count : 0);
        }

        private static string ToEndReasonName(int reason)
        {
            return Enum.IsDefined(typeof(MobaTraceEndReason), reason)
                ? ((MobaTraceEndReason)reason).ToString()
                : reason.ToString(CultureInfo.InvariantCulture);
        }

        private static void Add(List<AnalysisKeyValue> values, string key, int value) => Add(values, key, value.ToString(CultureInfo.InvariantCulture));
        private static void Add(List<AnalysisKeyValue> values, string key, long value) => Add(values, key, value.ToString(CultureInfo.InvariantCulture));
        private static void Add(List<AnalysisKeyValue> values, string key, bool value) => Add(values, key, value ? "true" : "false");
        private static void Add(List<AnalysisKeyValue> values, string key, string value) => values.Add(new AnalysisKeyValue(key, value));
        private static string ToString(long value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
