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
