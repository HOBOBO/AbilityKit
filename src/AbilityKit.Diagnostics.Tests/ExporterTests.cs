using System.IO;
using System.Text.Json;
using AbilityKit.Diagnostics;
using AbilityKit.Diagnostics.Exporters;
using Xunit;

namespace AbilityKit.Diagnostics.Tests;

/// <summary>
/// 导出器产物契约：flamegraph 格式必须是可渲染的 SVG（修复点：此前输出的是 folded 文本）、
/// 所有落盘产物不带 UTF-8 BOM（外部火焰图工具对首行 BOM 敏感）、folded 保持 Brendan Gregg 格式。
/// </summary>
public sealed class ExporterTests
{
    private static ProfilerSnapshot CreateSnapshot()
    {
        var profiler = new EditorProfiler();
        profiler.Start();
        using (profiler.Begin("moba.trigger.dispatch").ToScope())
        {
            using (profiler.Begin("moba.trigger.evaluate").ToScope()) { }
            profiler.Increment("moba.trigger.dispatch.started");
        }
        using (profiler.Begin("world.tick").ToScope())
        {
            profiler.Record("moba.skill.pipeline.step", 2_500_000);
        }
        profiler.Stop();
        return profiler.GetSnapshot();
    }

    [Fact]
    public void FlameGraphExporter_extension_is_flame_svg()
    {
        Assert.Equal(".flame.svg", new FlameGraphExporter().Extension);
    }

    [Fact]
    public void FlameGraphExporter_emits_svg_document_with_frames_and_tooltips()
    {
        var svg = new FlameGraphExporter().ExportToString(CreateSnapshot());

        Assert.StartsWith("<svg", svg);
        Assert.Contains("prefers-color-scheme: dark", svg);
        Assert.Contains("<title>moba.trigger.dispatch", svg);
        Assert.Contains("Top self time", svg);
        // 两个类目根（moba / world）都要渲染出行。
        Assert.Contains("<title>moba  total=", svg);
        Assert.Contains("<title>world  total=", svg);
    }

    [Fact]
    public void FlameGraphExporter_renders_empty_snapshot_without_throwing()
    {
        var profiler = new EditorProfiler();
        profiler.Start();
        profiler.Stop();

        var svg = new FlameGraphExporter().ExportToString(profiler.GetSnapshot());
        Assert.StartsWith("<svg", svg);
    }

    [Fact]
    public void Exported_files_do_not_start_with_utf8_bom()
    {
        var directory = Path.Combine(Path.GetTempPath(), "abilitykit-exporter-tests", Path.GetRandomFileName());
        var snapshot = CreateSnapshot();

        var jsonPath = Path.Combine(directory, "profile.json");
        new JsonExporter().Export(snapshot, jsonPath);
        var foldedPath = Path.Combine(directory, "profile.folded.txt");
        new FoldedExporter().Export(snapshot, foldedPath);
        var svgPath = Path.Combine(directory, "profile.flame.svg");
        new FlameGraphExporter().Export(snapshot, svgPath);

        foreach (var path in new[] { jsonPath, foldedPath, svgPath })
        {
            var bytes = File.ReadAllBytes(path);
            Assert.True(
                bytes.Length < 3 || !(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF),
                path + " must not start with a UTF-8 BOM");
        }

        Assert.Equal((byte)'{', File.ReadAllBytes(jsonPath)[0]);
    }

    [Fact]
    public void Streaming_exporters_match_string_exports()
    {
        var directory = Path.Combine(Path.GetTempPath(), "abilitykit-exporter-tests", Path.GetRandomFileName());
        var snapshot = CreateSnapshot();
        var cases = new (IExporter Exporter, string Name)[]
        {
            (new JsonExporter(), "profile.json"),
            (new CsvExporter(), "profile.csv"),
            (new FoldedExporter(), "profile.folded")
        };

        foreach (var item in cases)
        {
            var path = Path.Combine(directory, item.Name);
            item.Exporter.Export(snapshot, path);
            Assert.Equal(item.Exporter.ExportToString(snapshot), File.ReadAllText(path));
        }
    }

    [Fact]
    public void Advanced_streaming_exporters_write_valid_files()
    {
        var directory = Path.Combine(Path.GetTempPath(), "abilitykit-exporter-tests", Path.GetRandomFileName());
        var snapshot = CreateSnapshot();

        var speedscopePath = Path.Combine(directory, "profile.speedscope.json");
        new SpeedScopeExporter().Export(snapshot, speedscopePath);
        Assert.Equal(new SpeedScopeExporter().ExportToString(snapshot), File.ReadAllText(speedscopePath));
        using (var parsed = JsonDocument.Parse(File.ReadAllText(speedscopePath)))
        {
            Assert.True(parsed.RootElement.GetProperty("profiles")[0].GetProperty("events").GetArrayLength() > 0);
        }

        var chromePath = Path.Combine(directory, "profile.chrome-perf.json");
        new ChromePerfExporter().Export(snapshot, chromePath);
        using (var parsed = JsonDocument.Parse(File.ReadAllText(chromePath)))
        {
            Assert.Equal("spans", parsed.RootElement.GetProperty("metadata").GetProperty("timeline").GetString());
            Assert.True(parsed.RootElement.GetProperty("traceEvents").GetArrayLength() > 0);
        }

        var flamePath = Path.Combine(directory, "profile.flame.svg");
        new FlameGraphExporter().Export(snapshot, flamePath);
        Assert.Equal(new FlameGraphExporter().ExportToString(snapshot), File.ReadAllText(flamePath));
        Assert.StartsWith("<svg", File.ReadAllText(flamePath));

        var zeroxPath = Path.Combine(directory, "profile.0x.json");
        new ZeroxExporter().Export(snapshot, zeroxPath);
        Assert.Equal(new ZeroxExporter().ExportToString(snapshot), File.ReadAllText(zeroxPath));
        using (var parsed = JsonDocument.Parse(File.ReadAllText(zeroxPath)))
        {
            Assert.True(parsed.RootElement.GetProperty("children").GetArrayLength() > 0);
        }

        foreach (var path in new[] { flamePath, zeroxPath })
        {
            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length < 3 || !(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF),
                path + " must not start with a UTF-8 BOM");
        }
    }

    [Fact]
    public void FoldedExporter_keeps_brendan_gregg_format()
    {
        var folded = new FoldedExporter().ExportToString(CreateSnapshot());
        Assert.Contains("moba;moba.trigger.dispatch;moba.trigger.evaluate ", folded);
        Assert.Contains("world;world.tick;moba.skill.pipeline.step ", folded);
    }

    [Fact]
    public void Chrome_export_uses_real_timeline_when_spans_present()
    {
        var snapshot = CreateSnapshot();

        var chrome = new ChromePerfExporter().ExportToString(snapshot);
        Assert.Contains("\"timeline\": \"spans\"", chrome);

        using var parsed = JsonDocument.Parse(chrome);
        var events = parsed.RootElement.GetProperty("traceEvents").EnumerateArray().ToList();
        Assert.NotEmpty(events);
        var lastTs = -1d;
        foreach (var evt in events)
        {
            Assert.Equal("X", evt.GetProperty("ph").GetString());
            Assert.Equal("span", evt.GetProperty("cat").GetString());
            var ts = evt.GetProperty("ts").GetDouble();
            Assert.True(ts >= lastTs, "trace events must be chronological");
            lastTs = ts;
            Assert.True(evt.GetProperty("args").TryGetProperty("frame", out _));
        }
    }

    [Fact]
    public void Chrome_export_falls_back_to_aggregated_without_spans()
    {
        var snapshot = CreateSnapshot();
        snapshot.Spans = null;

        var chrome = new ChromePerfExporter().ExportToString(snapshot);
        Assert.Contains("\"timeline\": \"aggregated\"", chrome);
        using var parsed = JsonDocument.Parse(chrome);
        Assert.NotEmpty(parsed.RootElement.GetProperty("traceEvents").EnumerateArray());
    }

    [Fact]
    public void Speedscope_export_falls_back_to_aggregated_without_spans()
    {
        var snapshot = CreateSnapshot();
        snapshot.Spans = null;

        var speedscope = new SpeedScopeExporter().ExportToString(snapshot);
        using var parsed = JsonDocument.Parse(speedscope);
        Assert.False(parsed.RootElement.GetProperty("profiles")[0].GetProperty("name").GetString().Contains("(timeline)", StringComparison.Ordinal));
        Assert.NotEmpty(parsed.RootElement.GetProperty("profiles")[0].GetProperty("events").EnumerateArray());
    }

    [Fact]
    public void Speedscope_export_uses_real_timeline_when_spans_present()
    {
        var snapshot = CreateSnapshot();

        var speedscope = new SpeedScopeExporter().ExportToString(snapshot);
        Assert.Contains("(timeline)", speedscope);

        using var parsed = JsonDocument.Parse(speedscope);
        var events = parsed.RootElement.GetProperty("profiles")[0].GetProperty("events").EnumerateArray().ToList();
        Assert.Equal(snapshot.Spans.Count * 2, events.Count);
        var lastAt = -1L;
        foreach (var evt in events)
        {
            var at = evt.GetProperty("at").GetInt64();
            Assert.True(at >= lastAt, "speedscope events must be chronological");
            lastAt = at;
        }
    }

    [Fact]
    public void Json_export_contains_span_log_summary()
    {
        var snapshot = CreateSnapshot();

        var json = new JsonExporter().ExportToString(snapshot);
        Assert.Contains("\"spanLog\"", json);
        Assert.Contains("\"count\": 4", json);
        Assert.Contains("\"frameRange\"", json);
    }
}
