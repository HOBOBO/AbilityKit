using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbilityKit.Game.Test.UnitTest
{
    // Converts the MOBA-owned acceptance contract to the gameplay-neutral artifact-v1
    // contract. The original JSON/JSONL files remain owned by MobaAcceptanceTraceExporter.
    public static class MobaAcceptanceArtifactV1Adapter
    {
        public const string ProducerVersion = "abilitykit-moba-acceptance/1";
        public const string ExtensionKey = "com.abilitykit.moba";

        [Serializable] public sealed class ArtifactReference { public string kind; public string uri; public string contentType; }
        [Serializable] public sealed class Extensions { public MobaExtension com_abilitykit_moba; }
        [Serializable] public sealed class MobaExtension
        {
            public string worldId; public string category; public string[] tags; public bool accelerated;
            public string expectationPath; public MobaAcceptanceResult result;
            public MobaAcceptanceCoverageSummary coverage; public MobaAcceptanceTraceCount[] traceCounts;
        }
        [Serializable] public sealed class CaseSummary
        {
            public int schemaVersion = 1; public string producerVersion; public string runId; public string suiteId;
            public string caseId; public string displayName; public string status; public string failureCode;
            public string message; public string startedAt; public long durationMs; public Execution execution;
            public string[] tags; public AssertionCounts assertions; public ArtifactReference[] artifacts; public Extensions extensions;
        }
        [Serializable] public sealed class Execution { public int tickRate; public string runner; public string backend; }
        [Serializable] public sealed class AssertionCounts { public int total; public int passed; public int failed; public int skipped; }
        [Serializable] public sealed class TraceRecord
        {
            public int schemaVersion = 1; public string runId; public string caseId; public long nodeId;
            public long parentId; public long rootId; public string kind; public int frame; public int timeMs; public int configId;
            public TraceAttributes attributes; public TraceExtensionContainer extensions;
        }
        [Serializable] public sealed class TraceAttributes { public bool isRoot; public bool isEnded; public int endedFrame; public int endReason; public int childCount; }
        [Serializable] public sealed class TraceExtensionContainer { public TraceMoba com_abilitykit_moba; }
        [Serializable] public sealed class TraceMoba
        {
            public int kindValue; public long sourceActorId; public long targetActorId;
            public long sourceId; public long targetId; public long originSourceId; public long originTargetId;
            public string originSource; public string originTarget; public string displayName; public string configLabel;
            public string runtimeLabel; public string actorLabel; public string sourceActorLabel; public string targetActorLabel;
            public string configSource; public string semanticVersion;
        }
        [Serializable] public sealed class BatchCase { public string caseId; public string status; public string failureCode; public string summaryUri; }
        [Serializable] public sealed class BatchCounts { public int total; public int passed; public int failed; public int errors; public int skipped; }
        [Serializable] public sealed class BatchSummary
        {
            public int schemaVersion = 1; public string producerVersion; public string runId; public string suiteId;
            public string startedAt; public long durationMs; public string status; public BatchCounts counts;
            public BatchCase[] cases; public object filters;
        }

        public static string GetRunId(string requested) => string.IsNullOrEmpty(requested) ? Guid.NewGuid().ToString("N") : requested;

        public static void ExportCase(string directory, string runId, string startedAt, long durationMs, MobaAcceptanceSummary summary, MobaAcceptanceTraceRecord[] records)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            Directory.CreateDirectory(directory);
            runId = GetRunId(runId);
            var casePath = Path.Combine(directory, summary.caseId + "_artifact-v1.json");
            var tracePath = Path.Combine(directory, summary.caseId + "_trace-v1.jsonl");
            var failed = summary.result == null || !summary.result.passed;
            var assertionsTotal = summary.coverage == null ? 0 : summary.coverage.expectedTraceNodeCount + summary.coverage.forbiddenTraceNodeCount
                + summary.coverage.expectedActionCount + summary.coverage.expectedRelationshipCount + summary.coverage.expectedStateCount + summary.coverage.expectedContextCount;
            var assertionsPassed = summary.coverage == null ? 0 : summary.coverage.matchedExpectedTraceNodeCount
                + Math.Max(0, summary.coverage.forbiddenTraceNodeCount - summary.coverage.unexpectedForbiddenTraceNodeCount)
                + summary.coverage.executedExpectedActionCount + summary.coverage.satisfiedRelationshipCount
                + summary.coverage.satisfiedStateCount + summary.coverage.satisfiedContextCount;
            var dto = new CaseSummary
            {
                producerVersion = ProducerVersion, runId = runId, suiteId = "moba-acceptance", caseId = summary.caseId,
                displayName = summary.caseId, status = failed ? "failed" : "passed", failureCode = failed ? "acceptance_assertion_failed" : null,
                message = failed ? "One or more MOBA acceptance expectations failed." : string.Empty,
                startedAt = string.IsNullOrEmpty(startedAt) ? DateTime.UtcNow.ToString("O") : startedAt,
                durationMs = Math.Max(0, durationMs), execution = new Execution { tickRate = summary.tickRate, runner = "MobaAcceptanceRunner", backend = "Unity" },
                tags = summary.tags ?? Array.Empty<string>(), assertions = new AssertionCounts { total = assertionsTotal, passed = Math.Min(assertionsPassed, assertionsTotal), failed = Math.Max(0, assertionsTotal - assertionsPassed), skipped = 0 },
                artifacts = new[] { new ArtifactReference { kind = "trace", uri = RelativeUri(directory, tracePath), contentType = "application/x-ndjson" } },
                extensions = new Extensions { com_abilitykit_moba = new MobaExtension { worldId = summary.worldId, category = summary.category, tags = summary.tags,
                    accelerated = summary.accelerated, expectationPath = summary.expectationPath, result = summary.result, coverage = summary.coverage, traceCounts = summary.traceCounts } }
            };
            File.WriteAllText(casePath, JsonUtility.ToJson(dto, true).Replace("com_abilitykit_moba", ExtensionKey));
            using (var writer = new StreamWriter(tracePath, false, new UTF8Encoding(false)))
            {
                for (var i = 0; records != null && i < records.Length; i++) writer.WriteLine(JsonUtility.ToJson(MapTrace(runId, records[i])).Replace("com_abilitykit_moba", ExtensionKey));
            }
        }

        public static string RelativeUri(string root, string path)
        {
            var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
            if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidOperationException("Artifact URI must remain under its artifact directory.");
            return relative.Replace('\\', '/');
        }

        public static void ExportBatch(string directory, string runId, MobaAcceptanceBatchSummary batch)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            runId = GetRunId(runId);
            var cases = new BatchCase[batch.results == null ? 0 : batch.results.Length];
            for (var i = 0; i < cases.Length; i++)
            {
                var result = batch.results[i];
                var caseId = string.IsNullOrEmpty(result.caseId) ? Path.GetFileNameWithoutExtension(result.expectationPath) : result.caseId;
                cases[i] = new BatchCase { caseId = caseId, status = result.passed ? "passed" : (result.summary == null ? "error" : "failed"),
                    failureCode = result.passed ? null : (result.summary == null ? "case_execution_error" : "acceptance_assertion_failed"),
                    summaryUri = caseId + "_artifact-v1.json" };
            }
            var dto = new BatchSummary { producerVersion = ProducerVersion, runId = runId, suiteId = "moba-acceptance",
                startedAt = batch.startedUtc, durationMs = Math.Max(0, batch.durationMs), status = batch.allPassed ? "passed" : "failed",
                counts = new BatchCounts { total = batch.total, passed = batch.passed, failed = batch.failed, errors = 0, skipped = 0 }, cases = cases };
            File.WriteAllText(Path.Combine(directory, "batch_summary_v1.json"), JsonUtility.ToJson(dto, true));
        }

        private static TraceRecord MapTrace(string runId, MobaAcceptanceTraceRecord record) => new TraceRecord
        {
            runId = runId, caseId = record.caseId, nodeId = record.nodeId, parentId = record.parentId, rootId = record.rootId,
            kind = record.kind, frame = Math.Max(0, record.frame), timeMs = Math.Max(0, record.timeMs), configId = record.configId,
            attributes = new TraceAttributes { isRoot = record.isRoot, isEnded = record.isEnded, endedFrame = record.endedFrame, endReason = record.endReason, childCount = record.childCount },
            extensions = new TraceExtensionContainer { com_abilitykit_moba = new TraceMoba { kindValue = record.kindValue, sourceActorId = record.sourceActorId, targetActorId = record.targetActorId,
                sourceId = record.sourceId, targetId = record.targetId, originSourceId = record.originSourceId, originTargetId = record.originTargetId,
                originSource = record.originSource, originTarget = record.originTarget, displayName = record.displayName, configLabel = record.configLabel,
                runtimeLabel = record.runtimeLabel, actorLabel = record.actorLabel, sourceActorLabel = record.sourceActorLabel, targetActorLabel = record.targetActorLabel,
                configSource = record.configSource, semanticVersion = record.semanticVersion } }
        };
    }
}
