using System.Collections.Generic;
using AbilityKit.Diagnostics;
using Xunit;

namespace AbilityKit.Diagnostics.Tests;

/// <summary>
/// FlameNode / FlameRoot / ProfilerOptions / DiagnosticsSessionRecord 都是纯数据结构，
/// 这里全部使用确定性的显式构造值，不依赖任何时钟。
/// </summary>
public sealed class FlameDataTests
{
    // ---------------------------------------------------------------- FlameNode

    [Fact]
    public void GetOrCreateChild_creates_once_and_returns_the_same_node()
    {
        var parent = new FlameNode("parent", "net");

        var first = parent.GetOrCreateChild("child", "net");
        var second = parent.GetOrCreateChild("child", "other");

        Assert.Same(first, second);
        Assert.Single(parent.Children);
        Assert.Same(parent, first.Parent);
        Assert.Equal("net", first.Category);
    }

    [Fact]
    public void GetOrCreateChild_maps_empty_names_to_unnamed()
    {
        var parent = new FlameNode("parent", "net");

        var child = parent.GetOrCreateChild(string.Empty, null);

        Assert.Equal("unnamed", child.Name);
        Assert.Single(parent.Children);
    }

    [Fact]
    public void AddMeasurement_accumulates_totals_and_ignores_negative_values()
    {
        var node = new FlameNode("node", "net");

        node.AddMeasurement(100L);
        node.AddMeasurement(50L);
        node.AddMeasurement(-1L);

        Assert.Equal(150L, node.TotalNanoseconds);
        Assert.Equal(2, node.HitCount);
    }

    [Fact]
    public void Depth_follows_the_parent_chain()
    {
        var root = new FlameNode("root", "net");
        var child = root.GetOrCreateChild("child", "net");
        var grandChild = child.GetOrCreateChild("grand", "net");

        Assert.Equal(0, root.Depth);
        Assert.Equal(1, child.Depth);
        Assert.Equal(2, grandChild.Depth);
    }

    [Fact]
    public void Clone_creates_an_independent_deep_copy()
    {
        var root = new FlameNode("root", "net");
        var child = root.GetOrCreateChild("child", "net");
        child.AddMeasurement(100L);
        child.SelfNanoseconds = 60L;

        var clone = root.Clone();
        var cloneChild = clone.Children["child"];

        Assert.NotSame(root, clone);
        Assert.NotSame(child, cloneChild);
        Assert.Same(clone, cloneChild.Parent);

        cloneChild.AddMeasurement(50L);
        clone.Children["extra"] = new FlameNode("extra", "net", clone);

        Assert.Equal(100L, child.TotalNanoseconds);
        Assert.Equal(1, child.HitCount);
        Assert.Single(root.Children);
        Assert.Equal(150L, cloneChild.TotalNanoseconds);
        Assert.Equal(2, cloneChild.HitCount);
        Assert.Equal(2, clone.Children.Count);
        // 数值字段被完整复制
        Assert.Equal(60L, cloneChild.SelfNanoseconds);
    }

    // ---------------------------------------------------------------- FlameRoot

    [Fact]
    public void GetOrCreateRoot_normalizes_category_and_reuses_nodes()
    {
        var flame = new FlameRoot();

        var first = flame.GetOrCreateRoot("net");
        var second = flame.GetOrCreateRoot("net");

        Assert.Same(first, second);
        Assert.Equal("net", first.Name);
        Assert.Single(flame.Roots);

        var fallback = flame.GetOrCreateRoot(string.Empty);
        Assert.Equal("default", fallback.Name);
        Assert.Equal(2, flame.Roots.Count);
    }

    [Fact]
    public void CloneSnapshot_copies_deeply_and_overrides_the_end_timestamp()
    {
        var flame = new FlameRoot { SessionId = "s1", StartTimestamp = 100L, EndTimestamp = 0L };
        var node = flame.GetOrCreateRoot("net").GetOrCreateChild("net.send", "net");
        node.AddMeasurement(10L);

        var clone = flame.CloneSnapshot(350L);

        Assert.Equal("s1", clone.SessionId);
        Assert.Equal(100L, clone.StartTimestamp);
        Assert.Equal(350L, clone.EndTimestamp);

        var cloneNode = clone.Roots["net"].Children["net.send"];
        Assert.NotSame(node, cloneNode);
        Assert.Equal(10L, cloneNode.TotalNanoseconds);

        // 修改克隆不影响原始树
        cloneNode.AddMeasurement(5L);
        Assert.Equal(10L, node.TotalNanoseconds);
    }

    [Fact]
    public void FinalizeSelfTime_subtracts_child_totals_recursively()
    {
        var flame = new FlameRoot();
        var parent = flame.GetOrCreateRoot("net");
        parent.TotalNanoseconds = 1_000L;
        var child = parent.GetOrCreateChild("net.send", "net");
        child.TotalNanoseconds = 400L;
        var grandChild = child.GetOrCreateChild("net.retry", "net");
        grandChild.TotalNanoseconds = 150L;

        flame.FinalizeSelfTime();

        Assert.Equal(600L, parent.SelfNanoseconds); // 1000 - 400
        Assert.Equal(250L, child.SelfNanoseconds);  // 400 - 150
        Assert.Equal(150L, grandChild.SelfNanoseconds);
    }

    [Fact]
    public void FinalizeSelfTime_clamps_self_time_at_zero_when_children_exceed_parent()
    {
        var flame = new FlameRoot();
        var parent = flame.GetOrCreateRoot("net");
        parent.TotalNanoseconds = 300L;
        parent.SelfNanoseconds = 999L; // 之前的值也必须被覆盖
        var child = parent.GetOrCreateChild("net.send", "net");
        child.TotalNanoseconds = 500L;

        flame.FinalizeSelfTime();

        Assert.Equal(0L, parent.SelfNanoseconds);
        Assert.Equal(500L, child.SelfNanoseconds);
    }

    // ---------------------------------------------------------------- ProfilerOptions

    [Fact]
    public void Clone_is_an_independent_deep_copy()
    {
        var options = ProfilerOptions.CreateDefault();
        options.DefaultSampleRate = 0.5d;
        options.MaxSamplesPerMetric = 10;
        options.MaxDiagnosticEvents = 4;
        options.DisableCategory("net");
        options.SetCategorySampleRate("sim", 0.25d);
        options.SetMetricSampleRate("sim.step", 0.75d);

        var clone = options.Clone();
        clone.DefaultSampleRate = 0.1d;
        clone.MaxSamplesPerMetric = 99;
        clone.MaxDiagnosticEvents = 99;
        clone.DisableCategory("extra");
        clone.SetCategorySampleRate("sim", 1d);
        clone.SetMetricSampleRate("sim.step", 1d);
        clone.EnableCategory("net");

        Assert.True(options.IsCategoryDisabled("net"));
        Assert.False(options.IsCategoryDisabled("extra"));
        Assert.Equal(0.5d, options.DefaultSampleRate, 9);
        Assert.Equal(10, options.MaxSamplesPerMetric);
        Assert.Equal(4, options.MaxDiagnosticEvents);
        Assert.Equal(0.25d, options.GetSampleRate("sim", "sim.other"), 9);
        Assert.Equal(0.75d, options.GetSampleRate("sim", "sim.step"), 9);
    }

    [Fact]
    public void Category_toggle_roundtrips_case_insensitively()
    {
        var options = ProfilerOptions.CreateDefault();

        options.DisableCategory("NET");
        Assert.True(options.IsCategoryDisabled("net"));
        Assert.True(options.IsCategoryDisabled("Net"));

        options.EnableCategory("net");
        Assert.False(options.IsCategoryDisabled("net"));

        options.DisableCategory(string.Empty);
        Assert.False(options.IsCategoryDisabled(string.Empty));
    }

    [Fact]
    public void GetSampleRate_prefers_metric_over_category_over_default()
    {
        var options = ProfilerOptions.CreateDefault();
        options.DefaultSampleRate = 0.9d;
        options.SetCategorySampleRate("sim", 0.5d);
        options.SetMetricSampleRate("sim.step", 0.25d);

        Assert.Equal(0.25d, options.GetSampleRate("sim", "sim.step"), 9);
        Assert.Equal(0.5d, options.GetSampleRate("sim", "sim.other"), 9);
        Assert.Equal(0.9d, options.GetSampleRate("other", "other.thing"), 9);
    }

    [Fact]
    public void GetSampleRate_clamps_out_of_range_and_non_finite_rates()
    {
        var options = ProfilerOptions.CreateDefault();

        options.SetMetricSampleRate("m.high", 2d);
        options.SetMetricSampleRate("m.zero", -1d);
        options.SetMetricSampleRate("m.nan", double.NaN);
        options.SetMetricSampleRate("m.inf", double.PositiveInfinity);

        Assert.Equal(1d, options.GetSampleRate("cat", "m.high"), 9);
        Assert.Equal(0d, options.GetSampleRate("cat", "m.zero"), 9);
        Assert.Equal(0d, options.GetSampleRate("cat", "m.nan"), 9);
        Assert.Equal(0d, options.GetSampleRate("cat", "m.inf"), 9);
    }

    [Fact]
    public void Sample_rate_setters_ignore_null_or_empty_keys()
    {
        var options = ProfilerOptions.CreateDefault();

        options.SetCategorySampleRate(null!, 0.5d);
        options.SetCategorySampleRate(string.Empty, 0.5d);
        options.SetMetricSampleRate(null!, 0.5d);
        options.SetMetricSampleRate(string.Empty, 0.5d);

        Assert.Empty(options.CategorySampleRates);
        Assert.Empty(options.MetricSampleRates);
    }

    // ---------------------------------------------------------------- DiagnosticsSessionRecord

    [Fact]
    public void FromSnapshot_copies_all_counts_and_the_label()
    {
        var snapshot = new ProfilerSnapshot
        {
            SessionId = "s1",
            Root = new FlameRoot { SessionId = "s1", StartTimestamp = 1000L, EndTimestamp = 3500L },
            Counters = new Dictionary<string, CounterRecord>
            {
                ["a"] = new CounterRecord(),
                ["b"] = new CounterRecord()
            },
            Gauges = new Dictionary<string, GaugeRecord> { ["g"] = new GaugeRecord() },
            Samples = new Dictionary<string, List<double>>
            {
                ["x"] = new List<double>(),
                ["y"] = new List<double>()
            },
            Events = new List<DiagnosticEventRecord> { default, default, default },
            Metrics = new Dictionary<string, MetricDefinition>
            {
                ["m1"] = default,
                ["m2"] = default,
                ["m3"] = default,
                ["m4"] = default
            }
        };

        var record = DiagnosticsSessionRecord.FromSnapshot(snapshot, "label");

        Assert.Equal("s1", record.SessionId);
        Assert.Equal("label", record.Label);
        Assert.Equal(2500d, record.DurationMilliseconds, 9);
        Assert.Equal(2, record.CounterCount);
        Assert.Equal(1, record.GaugeCount);
        Assert.Equal(2, record.SampleCount);
        Assert.Equal(3, record.EventCount);
        Assert.Equal(4, record.MetricCount);
        Assert.True(record.SavedTimestamp > 0L);
    }

    [Fact]
    public void FromSnapshot_tolerates_null_parts_and_clamps_negative_duration()
    {
        var snapshot = new ProfilerSnapshot
        {
            SessionId = "s2",
            Root = new FlameRoot { SessionId = "s2", StartTimestamp = 900L, EndTimestamp = 100L }
        };

        var record = DiagnosticsSessionRecord.FromSnapshot(snapshot, null);

        Assert.Equal("s2", record.SessionId);
        Assert.Equal(string.Empty, record.Label);
        Assert.Equal(0d, record.DurationMilliseconds, 9);
        Assert.Equal(0, record.CounterCount);
        Assert.Equal(0, record.GaugeCount);
        Assert.Equal(0, record.SampleCount);
        Assert.Equal(0, record.EventCount);
        Assert.Equal(0, record.MetricCount);
    }

    [Fact]
    public void FromSnapshot_without_a_root_reports_zero_duration()
    {
        var snapshot = new ProfilerSnapshot { SessionId = "s3" };

        var record = DiagnosticsSessionRecord.FromSnapshot(snapshot, "lab");

        Assert.Equal(0d, record.DurationMilliseconds, 9);
    }
}
