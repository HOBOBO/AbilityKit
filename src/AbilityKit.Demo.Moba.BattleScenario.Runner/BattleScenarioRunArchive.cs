using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Scenario;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AbilityKit.Demo.Moba.BattleScenario.Runner;

/// <summary>
/// 将同一次 Console 场景执行的验收和可选诊断证据发布成一个独立批次。
/// </summary>
internal static class BattleScenarioRunArchive
{
    private const string Host = "dotnet-console";

    internal static BattleScenarioBatchResult Run(string inputDirectory, string outputRoot,
        IReadOnlyCollection<string>? caseFiles, string evidenceMode, out string publishedPath)
    {
        var started = DateTimeOffset.UtcNow;
        var runId = started.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N");
        var staging = Path.Combine(Path.GetFullPath(outputRoot), ".writing-" + runId);
        publishedPath = Path.Combine(Path.GetFullPath(outputRoot), runId);

        var timer = Stopwatch.StartNew();
        var entries = new List<MobaScenarioRunIndex.Case>();
        var batch = BattleScenarioBatchRunner.RunDirectory(inputDirectory, caseFiles, run =>
            entries.Add(WriteCase(staging, entries.Count + 1, run, evidenceMode)), evidenceMode != "none");
        if (entries.Count == 0) throw new InvalidDataException("输入目录没有可批跑的 .battlescenario 文件。");

        var counts = new MobaScenarioRunIndex.Counts
        {
            total = entries.Count,
            passed = entries.Count(entry => entry.status == "passed"),
            failed = entries.Count(entry => entry.status == "failed"),
            errors = entries.Count(entry => entry.status == "error"),
            noAssertions = entries.Count(entry => entry.status == "no-assertions"),
        };
        WriteJson(Path.Combine(staging, MobaScenarioRunIndex.FileName), new MobaScenarioRunIndex
        {
            schemaVersion = 1,
            producerVersion = "1",
            suiteId = MobaScenarioRunIndex.Suite,
            host = Host,
            evidenceMode = evidenceMode,
            runId = runId,
            startedAt = started.ToString("O", CultureInfo.InvariantCulture),
            durationMs = timer.ElapsedMilliseconds,
            status = counts.errors > 0 ? "error" : counts.failed > 0 ? "failed" : counts.passed > 0 ? "passed" : "no-assertions",
            counts = counts,
            cases = entries.ToArray(),
        });
        Directory.Move(staging, publishedPath);
        return batch;
    }

    private static MobaScenarioRunIndex.Case WriteCase(string staging, int index, BattleScenarioCaseRun run, string mode)
    {
        var entryFolder = $"cases/{index:D5}";
        var directory = Path.Combine(staging, entryFolder);
        Directory.CreateDirectory(directory);
        var outcome = run.Outcome;
        var hasAssertions = outcome?.Summary != null || outcome?.Prediction != null;
        var entry = new MobaScenarioRunIndex.Case
        {
            caseId = run.CaseId,
            scenario = Path.GetFileName(run.Path),
            status = outcome == null ? "error" : !hasAssertions ? "no-assertions" : outcome.Result.Passed ? "passed" : "failed",
            summary = outcome?.Result.Summary ?? run.Error ?? string.Empty,
            summaryUri = $"{entryFolder}/summary.json",
            evidenceStatus = mode == "none" ? "not-captured" : run.EvidenceError != null ? "error" : "not-retained",
        };
        var evidenceError = run.EvidenceError;
        if (mode != "none" && (mode == "all" || entry.status == "failed" || entry.status == "error"))
        {
            entry.evidenceStatus = evidenceError != null ? "error" : "not-captured";
            if (run.Snapshot != null)
            {
                try
                {
                    File.WriteAllText(Path.Combine(directory, "battle-diagnostics.json"),
                        MobaBattleDiagnosticArtifactCodec.ExportSnapshotToString(run.Snapshot));
                    entry.diagnosticUri = $"{entryFolder}/battle-diagnostics.json";
                    entry.evidenceStatus = "available";
                }
                catch (Exception error)
                {
                    evidenceError = error.Message;
                    entry.evidenceStatus = "error";
                }
            }
        }

        WriteJson(Path.Combine(directory, "summary.json"), new
        {
            schemaVersion = 1,
            entry.caseId,
            entry.scenario,
            entry.status,
            entry.summary,
            host = Host,
            inputFingerprint = run.Scenario == null ? null : FingerprintInput(run.Scenario),
            expectationsFingerprint = run.Scenario == null ? null : Hash(JsonConvert.SerializeObject(run.Scenario.Expectations)),
            outcome?.DeterminismFingerprint,
            assertions = outcome?.Summary,
            predictionFailures = outcome?.PredictionFailures,
            entry.evidenceStatus,
            entry.diagnosticUri,
            evidenceError,
        });
        return entry;
    }

    private static string FingerprintInput(TestScenario scenario)
    {
        var input = JObject.Parse(ScenarioCodec.Serialize(scenario));
        input.Remove("expectations");
        return Hash(input.ToString(Formatting.None));
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static void WriteJson(string path, object value) =>
        File.WriteAllText(path, JsonConvert.SerializeObject(value, Formatting.Indented));
}
