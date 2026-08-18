using System.Collections.Generic;
using System.Linq;
using AbilityKit.Diagnostics;
using AbilityKit.Diagnostics.Analysis;
using Xunit;

namespace AbilityKit.Diagnostics.Tests;

/// <summary>
/// AnalysisProfilerBuilder 是纯转换函数：快照完全由手工构造（或由 EditorProfiler 确定
/// 性数据产生），不涉及任何时钟。
/// </summary>
public sealed class AnalysisProfilerBuilderTests
{
    private static ProfilerSnapshot CreateSnapshot()
    {
        var snapshot = new ProfilerSnapshot
        {
            SessionId = "sess-1",
            Timestamp = 42L,
            Root = new FlameRoot { SessionId = "sess-1", StartTimestamp = 1000L, EndTimestamp = 3500L }
        };

        var netRoot = new FlameNode("net", "net");
        var send = new FlameNode("net.send", "net", netRoot)
        {
            TotalNanoseconds = 2_500_000L,
            SelfNanoseconds = 1_000_000L,
            HitCount = 3
        };
        send.Children["net.send.retry"] = new FlameNode("net.send.retry", "net", send)
        {
            TotalNanoseconds = 1_000_000L,
            SelfNanoseconds = 1_000_000L,
            HitCount = 2
        };
        netRoot.Children["net.send"] = send;
        netRoot.Children["net.recv"] = new FlameNode("net.recv", "net", netRoot)
        {
            TotalNanoseconds = 500_000L,
            SelfNanoseconds = 500_000L,
            HitCount = 1
        };
        snapshot.Root.Roots["net"] = netRoot;

        return snapshot;
    }

    [Fact]
    public void FromSnapshot_copies_the_header_and_clamps_negative_duration()
    {
        var section = AnalysisProfilerBuilder.FromSnapshot(CreateSnapshot());

        Assert.Equal("sess-1", section.SessionId);
        Assert.Equal(42L, section.Timestamp);
        Assert.Equal(2500d, section.DurationMs, 9);

        var negative = CreateSnapshot();
        negative.Root.EndTimestamp = 100L; // 早于 StartTimestamp
        Assert.Equal(0d, AnalysisProfilerBuilder.FromSnapshot(negative).DurationMs, 9);
    }

    [Fact]
    public void Metrics_map_fields_and_fall_back_to_the_dictionary_key()
    {
        var snapshot = CreateSnapshot();
        snapshot.Metrics = new Dictionary<string, MetricDefinition>
        {
            ["legacy"] = new MetricDefinition
            {
                Name = string.Empty,
                Category = null,
                Kind = MetricKind.Counter,
                Unit = null,
                Description = null,
                Tags = new[] { "core", null, string.Empty }
            },
            ["modern"] = new MetricDefinition
            {
                Name = "net.query",
                Category = "net",
                Kind = MetricKind.Duration,
                Unit = "ms",
                Description = "query",
                Tags = new[] { "net" }
            }
        };

        var section = AnalysisProfilerBuilder.FromSnapshot(snapshot);

        Assert.Equal(2, section.Metrics.Count);
        var legacy = section.Metrics.Single(m => m.Name == "legacy");
        Assert.Equal(string.Empty, legacy.Category);
        Assert.Equal("Counter", legacy.Kind);
        Assert.Equal(string.Empty, legacy.Unit);
        Assert.Equal(string.Empty, legacy.Description);
        Assert.Equal(new[] { "core" }, legacy.Tags); // null/空标签被过滤

        var modern = section.Metrics.Single(m => m.Name == "net.query");
        Assert.Equal("net", modern.Category);
        Assert.Equal("Duration", modern.Kind);
        Assert.Equal("query", modern.Description);
    }

    [Fact]
    public void Counters_map_the_core_fields()
    {
        var snapshot = CreateSnapshot();
        snapshot.Counters = new Dictionary<string, CounterRecord>
        {
            ["combat.hit"] = new CounterRecord { Name = "combat.hit", Value = 12L, Delta = 4L, SampleCount = 7L }
        };

        var counter = AnalysisProfilerBuilder.FromSnapshot(snapshot).Counters.Single();

        Assert.Equal("combat.hit", counter.Name);
        Assert.Equal(12L, counter.Value);
        Assert.Equal(4L, counter.Delta);
        Assert.Equal(7L, counter.SampleCount);
    }

    [Fact]
    public void Gauges_map_value_and_timestamp()
    {
        var snapshot = CreateSnapshot();
        snapshot.Gauges = new Dictionary<string, GaugeRecord>
        {
            ["net.frame"] = new GaugeRecord { Name = "net.frame", Value = 60L, Timestamp = 123L }
        };

        var gauge = AnalysisProfilerBuilder.FromSnapshot(snapshot).Gauges.Single();

        Assert.Equal("net.frame", gauge.Name);
        Assert.Equal(60L, gauge.Value);
        Assert.Equal(123L, gauge.Timestamp);
    }

    [Fact]
    public void Sample_summaries_aggregate_count_sum_mean_min_max()
    {
        var snapshot = CreateSnapshot();
        snapshot.Samples = new Dictionary<string, List<double>>
        {
            ["s.a"] = new List<double> { 3d, 1d, 2d },
            ["s.empty"] = new List<double>()
        };

        var section = AnalysisProfilerBuilder.FromSnapshot(snapshot);

        Assert.Equal(2, section.Samples.Count);
        var a = section.Samples.Single(s => s.Name == "s.a");
        Assert.Equal(3, a.Count);
        Assert.Equal(6d, a.Sum, 9);
        Assert.Equal(2d, a.Mean, 9);
        Assert.Equal(1d, a.Min, 9);
        Assert.Equal(3d, a.Max, 9);

        var empty = section.Samples.Single(s => s.Name == "s.empty");
        Assert.Equal(0, empty.Count);
        Assert.Equal(0d, empty.Sum, 9);
        Assert.Equal(0d, empty.Mean, 9);
        Assert.Equal(0d, empty.Min, 9);
        Assert.Equal(0d, empty.Max, 9);
    }

    [Fact]
    public void Rates_map_the_rolling_windows()
    {
        var snapshot = CreateSnapshot();
        snapshot.Rates = new Dictionary<string, RateRecord>
        {
            ["combat.spawn"] = new RateRecord
            {
                Name = "combat.spawn",
                TotalCount = 9L,
                Count1Second = 1L,
                Count5Seconds = 4L,
                Count60Seconds = 9L,
                PeakPerSecond = 2L
            }
        };

        var rate = AnalysisProfilerBuilder.FromSnapshot(snapshot).Rates.Single();

        Assert.Equal("combat.spawn", rate.Name);
        Assert.Equal(9L, rate.Total);
        Assert.Equal(1L, rate.Count1Second);
        Assert.Equal(4L, rate.Count5Seconds);
        Assert.Equal(9L, rate.Count60Seconds);
        Assert.Equal(2L, rate.PeakPerSecond);
    }

    [Fact]
    public void Durations_map_the_summary_statistics()
    {
        var snapshot = CreateSnapshot();
        snapshot.Durations = new Dictionary<string, DurationSummaryRecord>
        {
            ["net.query"] = new DurationSummaryRecord
            {
                Name = "net.query",
                Count = 4L,
                SumMilliseconds = 10d,
                MeanMilliseconds = 2.5d,
                MinMilliseconds = 1d,
                MaxMilliseconds = 4d
            }
        };

        var duration = AnalysisProfilerBuilder.FromSnapshot(snapshot).Durations.Single();

        Assert.Equal("net.query", duration.Name);
        Assert.Equal(4L, duration.Count);
        Assert.Equal(10d, duration.SumMs, 9);
        Assert.Equal(2.5d, duration.MeanMs, 9);
        Assert.Equal(1d, duration.MinMs, 9);
        Assert.Equal(4d, duration.MaxMs, 9);
    }

    [Fact]
    public void Events_stringify_severity_and_normalize_nulls()
    {
        var snapshot = CreateSnapshot();
        snapshot.Events = new List<DiagnosticEventRecord>
        {
            new DiagnosticEventRecord
            {
                Timestamp = 5L,
                Severity = DiagnosticSeverity.Error,
                Category = "net",
                Name = "net.fail",
                Message = "boom",
                Value = 3d,
                Threshold = 2d
            },
            new DiagnosticEventRecord { Severity = DiagnosticSeverity.Warning, Category = null, Name = null, Message = null }
        };

        var events = AnalysisProfilerBuilder.FromSnapshot(snapshot).Events;

        Assert.Equal(2, events.Count);
        Assert.Equal(5L, events[0].Timestamp);
        Assert.Equal("Error", events[0].Severity);
        Assert.Equal("net", events[0].Category);
        Assert.Equal("boom", events[0].Message);
        Assert.Equal(3d, events[0].Value, 9);
        Assert.Equal(2d, events[0].Threshold, 9);

        Assert.Equal("Warning", events[1].Severity);
        Assert.Equal(string.Empty, events[1].Category);
        Assert.Equal(string.Empty, events[1].Name);
        Assert.Equal(string.Empty, events[1].Message);
    }

    [Fact]
    public void Flame_nodes_convert_nanoseconds_to_milliseconds_recursively()
    {
        var section = AnalysisProfilerBuilder.FromSnapshot(CreateSnapshot());

        var netRoot = Assert.Single(section.Flame);
        Assert.Equal("net", netRoot.Name);
        Assert.Equal("net", netRoot.Category);
        Assert.Equal(0d, netRoot.TotalMs, 9);

        var send = netRoot.Children.Single(c => c.Name == "net.send");
        Assert.Equal(2.5d, send.TotalMs, 9);
        Assert.Equal(1d, send.SelfMs, 9);
        Assert.Equal(3, send.Hits);

        var retry = Assert.Single(send.Children);
        Assert.Equal("net.send.retry", retry.Name);
        Assert.Equal(1d, retry.TotalMs, 9);
        Assert.Equal(1d, retry.SelfMs, 9);
        Assert.Equal(2, retry.Hits);

        var recv = netRoot.Children.Single(c => c.Name == "net.recv");
        Assert.Equal(0.5d, recv.TotalMs, 9);
    }

    [Fact]
    public void Null_snapshot_sections_produce_empty_collections()
    {
        var snapshot = new ProfilerSnapshot
        {
            SessionId = null,
            Metrics = null,
            Counters = null,
            Gauges = null,
            Samples = null,
            Rates = null,
            Durations = null,
            Events = null,
            Root = null
        };

        var section = AnalysisProfilerBuilder.FromSnapshot(snapshot);

        Assert.Equal(string.Empty, section.SessionId);
        Assert.Equal(0d, section.DurationMs, 9);
        Assert.Empty(section.Metrics);
        Assert.Empty(section.Counters);
        Assert.Empty(section.Gauges);
        Assert.Empty(section.Samples);
        Assert.Empty(section.Rates);
        Assert.Empty(section.Durations);
        Assert.Empty(section.Events);
        Assert.Empty(section.Flame);
    }

    [Fact]
    public void End_to_end_from_a_live_EditorProfiler_snapshot()
    {
        var profiler = new EditorProfiler();
        profiler.Start();
        profiler.Record("net.a", 1_500_000L);
        profiler.Record("net.a", 500_000L);
        profiler.Increment("c.hits");
        profiler.Increment("c.hits");
        profiler.SetGauge("g.frame", 7L);
        profiler.Sample("s.step", 2.5d);
        profiler.EmitEvent(DiagnosticSeverity.Warning, "net", "net.warn", "slow", 12d, 10d);

        var section = AnalysisProfilerBuilder.FromSnapshot(profiler.GetSnapshot());

        Assert.Matches(@"^\d+-\d{14}$", section.SessionId);
        Assert.True(section.DurationMs >= 0d);

        var duration = Assert.Single(section.Durations);
        Assert.Equal("net.a", duration.Name);
        Assert.Equal(2L, duration.Count);
        Assert.Equal(2d, duration.SumMs, 9);
        Assert.Equal(0.5d, duration.MinMs, 9);
        Assert.Equal(1.5d, duration.MaxMs, 9);

        var counter = Assert.Single(section.Counters);
        Assert.Equal("c.hits", counter.Name);
        Assert.Equal(2L, counter.Value);

        var gauge = Assert.Single(section.Gauges);
        Assert.Equal(7L, gauge.Value);

        var stepSample = section.Samples.Single(s => s.Name == "s.step");
        Assert.Equal(1, stepSample.Count);
        Assert.Equal(2.5d, stepSample.Mean, 9);
        var durationSample = section.Samples.Single(s => s.Name == "net.a");
        Assert.Equal(2, durationSample.Count);

        var evt = Assert.Single(section.Events);
        Assert.Equal("Warning", evt.Severity);
        Assert.Equal("slow", evt.Message);
        Assert.Equal(12d, evt.Value, 9);
        Assert.Equal(10d, evt.Threshold, 9);

        var flameRoot = Assert.Single(section.Flame);
        Assert.Equal("net", flameRoot.Name);
        var netA = Assert.Single(flameRoot.Children);
        Assert.Equal("net.a", netA.Name);
        Assert.Equal(2d, netA.TotalMs, 9);
        Assert.Equal(2, netA.Hits);

        // 指标注册表：net.a / c.hits / g.frame / s.step（EmitEvent 不注册指标）
        Assert.Equal(4, section.Metrics.Count);
        Assert.Contains(section.Metrics, m => m.Name == "net.a" && m.Kind == "Duration");
        Assert.Contains(section.Metrics, m => m.Name == "c.hits" && m.Kind == "Counter");
    }
}
