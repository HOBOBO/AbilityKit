using System.IO;
using System.Text.Json;
using AbilityKit.Diagnostics.Analysis;
using AbilityKit.Diagnostics.Exporters;
using Xunit;

namespace AbilityKit.Diagnostics.Tests;

/// <summary>
/// 分析产物的 DTO 契约：Schema 常量与默认值必须保持稳定（供 Web 工具消费）。
/// </summary>
public sealed class AnalysisArtifactTests
{
    [Fact]
    public void Analysis_artifact_file_export_streams_valid_json_without_bom()
    {
        var artifact = new AbilityKitAnalysisArtifact
        {
            SchemaVersion = string.Empty,
            Session = new AnalysisSessionInfo { SessionId = "streaming-export" },
            Metadata = { new AnalysisKeyValue("scenario", "large-project") }
        };
        artifact.Profiler.Events.Add(new AnalysisProfilerEvent
        {
            Name = "diagnostics.warning",
            Message = "sample warning"
        });

        var exporter = new AnalysisArtifactJsonExporter();
        var expected = exporter.ExportToString(artifact);
        var path = Path.Combine(Path.GetTempPath(), "abilitykit-analysis-export-tests", Path.GetRandomFileName(), "artifact.json");
        exporter.Export(artifact, path);

        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length < 3 || !(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF));
        using var expectedJson = JsonDocument.Parse(expected);
        using var actualJson = JsonDocument.Parse(bytes);
        Assert.Equal(JsonSerializer.Serialize(expectedJson.RootElement), JsonSerializer.Serialize(actualJson.RootElement));
        Assert.Equal(AbilityKitAnalysisSchema.Version, actualJson.RootElement.GetProperty("SchemaVersion").GetString());
    }

    [Fact]
    public void Schema_constants_are_stable()
    {
        Assert.Equal("abilitykit-analysis.v1", AbilityKitAnalysisSchema.Version);
        Assert.Equal("AbilityKit.Diagnostics", AbilityKitAnalysisSchema.DefaultProducer);
    }

    [Fact]
    public void Artifact_defaults_the_schema_version_and_all_sections()
    {
        var artifact = new AbilityKitAnalysisArtifact();

        Assert.Equal(AbilityKitAnalysisSchema.Version, artifact.SchemaVersion);
        Assert.NotNull(artifact.Session);
        Assert.NotNull(artifact.Time);
        Assert.NotNull(artifact.Dictionaries);
        Assert.NotNull(artifact.Profiler);
        Assert.NotNull(artifact.Trace);
        Assert.Null(artifact.BattleDiagnostics); // 战斗诊断是可选区段
        Assert.NotNull(artifact.Diagnostics);
        Assert.NotNull(artifact.Runtime);
        Assert.NotNull(artifact.Insights);
        Assert.NotNull(artifact.ThresholdProfile);
        Assert.NotNull(artifact.Baseline);
        Assert.Empty(artifact.Metadata);
    }

    [Fact]
    public void Session_defaults_the_producer_to_the_schema_constant()
    {
        var session = new AnalysisSessionInfo();

        Assert.Equal(AbilityKitAnalysisSchema.DefaultProducer, session.Producer);
        Assert.Equal(string.Empty, session.SessionId);
        Assert.Equal(string.Empty, session.Project);
        Assert.Equal(0L, session.GeneratedAtUnixMs);
    }

    [Fact]
    public void KeyValue_constructor_normalizes_nulls_to_empty_strings()
    {
        var pair = new AnalysisKeyValue(null, null);

        Assert.Equal(string.Empty, pair.Key);
        Assert.Equal(string.Empty, pair.Value);

        var filled = new AnalysisKeyValue("k", "v");
        Assert.Equal("k", filled.Key);
        Assert.Equal("v", filled.Value);
    }

    [Fact]
    public void Optional_sections_default_to_empty_lists()
    {
        Assert.Empty(new AnalysisTraceSection().Roots);
        Assert.Empty(new AnalysisTraceSection().Edges);
        Assert.False(new AnalysisTraceSection().Truncated);

        Assert.Empty(new AnalysisDiagnosticsSection().Warnings);
        Assert.Empty(new AnalysisDiagnosticsSection().Exceptions);
        Assert.Empty(new AnalysisRuntimeSection().Records);
        Assert.Empty(new AnalysisInsightsSection().Records);
        Assert.Empty(new AnalysisThresholdProfile().Rules);
        Assert.Empty(new AnalysisThresholdProfile().Evaluations);
        Assert.Empty(new AnalysisBaselineSection().Metrics);
        Assert.Empty(new AnalysisDictionaries().TraceKinds);
    }

    [Fact]
    public void Trace_node_defaults_are_zeroed_and_not_ended()
    {
        var node = new AnalysisTraceNode();

        Assert.Equal(0L, node.ContextId);
        Assert.Equal(0, node.Kind);
        Assert.Equal(0, node.EndedFrame);
        Assert.Equal(0, node.EndReason);
        Assert.Equal(0, node.ChildCount);
        Assert.False(node.IsRoot);
        Assert.False(node.IsEnded);
        Assert.NotNull(node.Metadata);
    }
}
