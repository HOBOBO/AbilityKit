using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AbilityKit.Diagnostics;
using Xunit;

namespace AbilityKit.Diagnostics.Tests;

/// <summary>
/// EditorProfiler 在 .NET 下可以无 Unity 依赖地构造。
/// Record/Add/SetGauge/Sample 走显式数值，断言完全确定；
/// Begin/Complete 使用真实 Stopwatch，只断言结构、命中数与单调性，不断言绝对毫秒。
/// </summary>
public sealed class EditorProfilerTests
{
    private static readonly Regex SessionIdPattern = new(@"^\d+-\d{14}$", RegexOptions.Compiled);
    private static readonly Regex GuidSessionIdPattern = new(@"^[0-9a-f]{32}$", RegexOptions.Compiled);

    private static long Ms(double milliseconds)
    {
        return (long)(milliseconds * 1_000_000d);
    }

    private static FlameNode? FindNode(FlameRoot root, string name)
    {
        foreach (var categoryRoot in root.Roots.Values)
        {
            var found = FindNode(categoryRoot, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;

        static FlameNode? FindNode(FlameNode node, string name)
        {
            if (node.Name == name)
            {
                return node;
            }

            foreach (var child in node.Children.Values)
            {
                var found = FindNode(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }

    // ------------------------------------------------------------- 生命周期

    [Fact]
    public void Fresh_profiler_is_disabled_and_ignores_all_data()
    {
        var profiler = new EditorProfiler();

        Assert.False(profiler.IsEnabled);
        Assert.False(profiler.Begin("net.call").IsValid);

        profiler.Record("net.call", Ms(1));
        profiler.Increment("combat.hit");
        profiler.Add("combat.hit", 5L);
        profiler.SetGauge("net.frame", 1L);
        profiler.Sample("net.delta", 1d);

        Assert.Empty(profiler.GetCounters());
        Assert.Empty(profiler.GetGauges());
        Assert.Empty(profiler.GetSamples());
        Assert.Empty(profiler.GetDurationSummaries());
        Assert.Empty(profiler.GetEvents());
        Assert.Empty(profiler.GetRates());
        Assert.Empty(profiler.GetMetrics());
        Assert.Empty(profiler.GetRoot().Roots);
        Assert.Empty(profiler.GetSessionHistory());
    }

    [Fact]
    public void Start_enables_recording_and_builds_a_timestamped_session_identity()
    {
        var profiler = new EditorProfiler();

        profiler.Start();

        Assert.True(profiler.IsEnabled);
        Assert.Matches(SessionIdPattern, profiler.GetRoot().SessionId);
        Assert.StartsWith("1-", profiler.GetRoot().SessionId);
        Assert.True(profiler.GetRoot().StartTimestamp > 0L);
        // 运行中的快照会用当前时间填充 EndTimestamp（内部未结束标记不可观测）
        Assert.True(profiler.GetRoot().EndTimestamp >= profiler.GetRoot().StartTimestamp);
    }

    [Fact]
    public void Start_clears_previously_collected_data_and_increments_the_session_index()
    {
        var profiler = new EditorProfiler();
        profiler.Start();
        profiler.Record("net.call", Ms(1));
        Assert.Single(profiler.GetDurationSummaries());

        profiler.Start();

        Assert.Empty(profiler.GetDurationSummaries());
        Assert.Empty(profiler.GetRoot().Roots);
        Assert.StartsWith("2-", profiler.GetRoot().SessionId);
    }

    [Fact]
    public void Stop_disables_recording_and_persists_the_end_timestamp()
    {
        var profiler = new EditorProfiler();
        profiler.Start();

        profiler.Stop();

        Assert.False(profiler.IsEnabled);
        var snapshot = profiler.GetSnapshot();
        Assert.True(snapshot.Root.EndTimestamp > 0L);
        Assert.True(snapshot.Timestamp >= snapshot.Root.EndTimestamp);
        Assert.True(snapshot.Root.EndTimestamp >= snapshot.Root.StartTimestamp);

        // 停止后的数据调用被忽略
        profiler.Record("net.call", Ms(1));
        Assert.Empty(profiler.GetDurationSummaries());
    }

    [Fact]
    public void Clear_resets_collections_history_and_session_identity()
    {
        var profiler = new EditorProfiler();
        profiler.Start();
        profiler.Record("net.call", Ms(1));
        profiler.EmitEvent(DiagnosticSeverity.Warning, "net", "net.call", "msg");
        profiler.SaveSession("first");
        Assert.Single(profiler.GetSessionHistory());

        profiler.Clear();

        Assert.True(profiler.IsEnabled); // Clear 不改变启停状态
        Assert.Empty(profiler.GetDurationSummaries());
        Assert.Empty(profiler.GetEvents());
        Assert.Empty(profiler.GetSessionHistory());
        Assert.Matches(GuidSessionIdPattern, profiler.GetRoot().SessionId);
    }

    // ------------------------------------------------------------- 选项

    [Fact]
    public void Configure_stores_an_independent_clone_and_null_resets_to_defaults()
    {
        var profiler = new EditorProfiler();
        var options = new ProfilerOptions { MaxSamplesPerMetric = 5, MaxDiagnosticEvents = 9 };
        profiler.Configure(options);
        options.MaxSamplesPerMetric = 100; // 外部修改不得影响 profiler

        var applied = profiler.GetOptions();
        Assert.Equal(5, applied.MaxSamplesPerMetric);
        Assert.Equal(9, applied.MaxDiagnosticEvents);

        // GetOptions 返回的也是克隆
        applied.MaxSamplesPerMetric = 77;
        Assert.Equal(5, profiler.GetOptions().MaxSamplesPerMetric);

        profiler.Configure(null!);
        var defaults = profiler.GetOptions();
        Assert.True(defaults.Enabled);
        Assert.Equal(512, defaults.MaxSamplesPerMetric);
        Assert.Equal(256, defaults.MaxDiagnosticEvents);
    }

    [Fact]
    public void Options_enabled_false_blocks_collection_even_while_running()
    {
        var profiler = new EditorProfiler();
        profiler.Configure(new ProfilerOptions { Enabled = false });
        profiler.Start();

        Assert.True(profiler.IsEnabled); // Start 仍然生效，但选项关闭采集
        profiler.Record("net.call", Ms(1));
        profiler.Increment("combat.hit");
        Assert.Empty(profiler.GetDurationSummaries());
        Assert.Empty(profiler.GetCounters());
    }

    // ------------------------------------------------------------- Begin / Complete

    [Fact]
    public void Begin_returns_invalid_token_for_empty_or_null_name()
    {
        var profiler = new EditorProfiler();
        profiler.Start();

        Assert.False(profiler.Begin(string.Empty).IsValid);
        Assert.False(profiler.Begin(null!).IsValid);
        Assert.Empty(profiler.GetRoot().Roots);
        Assert.Empty(profiler.GetMetrics());
    }

    [Fact]
    public void Begin_is_rejected_when_the_metric_sample_rate_is_zero()
    {
        var profiler = new EditorProfiler();
        var options = ProfilerOptions.CreateDefault();
        options.SetMetricSampleRate("net.skip", 0d);
        profiler.Configure(options);
        profiler.Start();

        Assert.False(profiler.Begin("net.skip").IsValid);
        Assert.Empty(profiler.GetRoot().Roots);
        Assert.Empty(profiler.GetMetrics());
    }

    [Fact]
    public void Nested_scopes_build_a_flame_hierarchy()
    {
        var profiler = new EditorProfiler();
        profiler.Start();

        var outer = profiler.Begin("net.outer");
        var inner = profiler.Begin("net.inner");
        profiler.Complete(inner);
        profiler.Complete(outer);

        Assert.True(outer.IsValid);
        var root = profiler.GetRoot();
        Assert.True(root.Roots.ContainsKey("net"));

        var categoryRoot = root.Roots["net"];
        var outerNode = Assert.Single(categoryRoot.Children.Values);
        Assert.Equal("net.outer", outerNode.Name);
        Assert.Equal(1, outerNode.Depth);
        Assert.Equal(1, outerNode.HitCount);

        var innerNode = Assert.Single(outerNode.Children.Values);
        Assert.Equal("net.inner", innerNode.Name);
        Assert.Equal(2, innerNode.Depth);
        Assert.Equal(1, innerNode.HitCount);

        // 单调时钟保证 inner 严格包含于 outer 之内
        Assert.True(innerNode.TotalNanoseconds <= outerNode.TotalNanoseconds);
        Assert.True(innerNode.SelfNanoseconds >= 0L);
        Assert.True(outerNode.SelfNanoseconds >= 0L);
        Assert.Equal(0L, categoryRoot.TotalNanoseconds);
        Assert.Equal(0L, categoryRoot.SelfNanoseconds);

        var summaries = profiler.GetDurationSummaries();
        Assert.Equal(1L, summaries["net.outer"].Count);
        Assert.Equal(1L, summaries["net.inner"].Count);

        var samples = profiler.GetSamples();
        Assert.Single(samples["net.outer"]);
        Assert.Single(samples["net.inner"]);
    }

    [Fact]
    public void Sequential_siblings_aggregate_under_the_same_parent()
    {
        var profiler = new EditorProfiler();
        profiler.Start();

        var outer = profiler.Begin("net.outer");
        var first = profiler.Begin("net.first");
        profiler.Complete(first);
        var second = profiler.Begin("net.second");
        profiler.Complete(second);
        profiler.Complete(outer);

        var root = profiler.GetRoot();
        var outerNode = FindNode(root, "net.outer");
        Assert.NotNull(outerNode);
        Assert.Equal(2, outerNode!.Children.Count);
        Assert.True(outerNode.Children.ContainsKey("net.first"));
        Assert.True(outerNode.Children.ContainsKey("net.second"));
        Assert.Equal(1, outerNode.HitCount);
        Assert.Equal(1, outerNode.Children["net.first"].HitCount);
        Assert.Equal(1, outerNode.Children["net.second"].HitCount);

        // 两个子节点的总耗时之和不会超过父节点（自时间为非负）
        var childSum = outerNode.Children.Values.Sum(c => c.TotalNanoseconds);
        Assert.True(childSum <= outerNode.TotalNanoseconds);
    }

    [Fact]
    public void Out_of_order_completion_unwinds_to_the_named_frame()
    {
        var profiler = new EditorProfiler();
        profiler.Start();

        var outer = profiler.Begin("a.outer");
        var mid = profiler.Begin("a.mid");
        var leaf = profiler.Begin("a.leaf");

        // 先 Complete 最外层：乱序 unwind 把 leaf、mid 按各自开始时间强制收尾，
        // 不丢弃任何帧的数据（旧实现会把栈顶 leaf 帧直接丢弃，HitCount 恒为 0）。
        profiler.Complete(outer);
        var root = profiler.GetRoot();
        Assert.Equal(1, FindNode(root, "a.outer")!.HitCount);
        Assert.Equal(1, FindNode(root, "a.mid")!.HitCount);
        Assert.Equal(1, FindNode(root, "a.leaf")!.HitCount);
        Assert.True(profiler.GetDurationSummaries().ContainsKey("a.leaf"));

        // mid 已被强制收尾，再次 Complete 时栈已空，走独立时长路径记录到 root 层级的
        // 同名新节点——按名字聚合的耗时摘要无歧义，应为 2 次。
        profiler.Complete(mid);
        var summaries = profiler.GetDurationSummaries();
        Assert.Equal(2, summaries["a.mid"].Count);
        _ = leaf; // leaf 的令牌从未显式 Complete，但已被 unwind 强制收尾
    }

    [Fact]
    public void Complete_token_not_on_stack_records_standalone_duration()
    {
        var profiler = new EditorProfiler();
        profiler.Start();

        var a = profiler.Begin("b.a");
        var orphan = profiler.Begin("b.orphan");
        profiler.Complete(orphan);

        // 再次 Complete 已收尾过的令牌：目标不在栈上，应记为独立时长，
        // 不把耗时归属到栈顶的无关帧上（旧实现会错误记到 a 上）。
        profiler.Complete(orphan);

        var root = profiler.GetRoot();
        Assert.Equal(2, FindNode(root, "b.orphan")!.HitCount);
        Assert.Equal(0, FindNode(root, "b.a")!.HitCount); // a 仍在栈上未被收尾
    }

    [Fact]
    public void TicksToNanoseconds_large_tick_count_does_not_overflow()
    {
        // ticks ≈ 2× 中间溢出阈值（9.22e9）：旧实现的直接相乘会溢出成负纳秒
        // （Linux 上 Frequency==1e9，即 >9.2 秒的探针就会触发）。
        var ticks = long.MaxValue / 1_000_000_000L * 2L;

        var nanoseconds = EditorProfiler.TicksToNanoseconds(ticks);

        Assert.True(nanoseconds > 0L, $"expected positive nanoseconds, got {nanoseconds}");
        var expected = (long)((decimal)ticks * 1_000_000_000m / System.Diagnostics.Stopwatch.Frequency);
        Assert.Equal(expected, nanoseconds);
    }

    [Fact]
    public void Complete_on_a_foreign_thread_records_a_flat_duration()
    {
        var profiler = new EditorProfiler();
        profiler.Start();

        // Begin 发生在线程池线程上，其探针栈是该线程独有的；
        // 在测试线程上 Complete 时本地栈为空，应退化为扁平 Record。
        var token = Task.Run(() => profiler.Begin("net.cross")).GetAwaiter().GetResult();
        Assert.True(token.IsValid);

        profiler.Complete(token);

        var summaries = profiler.GetDurationSummaries();
        Assert.True(summaries.ContainsKey("net.cross"));
        Assert.Equal(1L, summaries["net.cross"].Count);

        var node = FindNode(profiler.GetRoot(), "net.cross");
        Assert.NotNull(node);
        Assert.Equal(1, node!.HitCount);
        Assert.True(node.TotalNanoseconds >= 0L);
    }

    [Fact]
    public void Complete_with_default_token_is_ignored()
    {
        var profiler = new EditorProfiler();
        profiler.Start();

        profiler.Complete(default);

        Assert.Empty(profiler.GetDurationSummaries());
        Assert.Empty(profiler.GetRoot().Roots);
    }

    // ------------------------------------------------------------- Record / 耗时

    [Fact]
    public void Record_accumulates_deterministic_duration_summaries()
    {
        var profiler = new EditorProfiler();
        profiler.Start();

        profiler.Record("net.query", Ms(1));
        profiler.Record("net.query", Ms(0.5));

        var summary = profiler.GetDurationSummaries()["net.query"];
        Assert.Equal(2L, summary.Count);
        Assert.Equal(1.5d, summary.SumMilliseconds, 9);
        Assert.Equal(0.75d, summary.MeanMilliseconds, 9);
        Assert.Equal(0.5d, summary.MinMilliseconds, 9);
        Assert.Equal(1d, summary.MaxMilliseconds, 9);

        Assert.Equal(new[] { 1d, 0.5d }, profiler.GetSamples()["net.query"]);
    }

    [Fact]
    public void Record_attaches_to_the_category_root_of_the_metric()
    {
        var profiler = new EditorProfiler();
        profiler.Start();

        profiler.Record("net.send", Ms(2));

        var root = profiler.GetRoot();
        Assert.True(root.Roots.ContainsKey("net"));
        var categoryRoot = root.Roots["net"];
        Assert.Equal("net", categoryRoot.Name);

        var node = categoryRoot.Children["net.send"];
        Assert.Equal(2_000_000L, node.TotalNanoseconds);
        Assert.Equal(2_000_000L, node.SelfNanoseconds);
        Assert.Equal(1, node.HitCount);
        Assert.Equal(0L, categoryRoot.SelfNanoseconds); // 分类根自身无采样
    }

    [Fact]
    public void Record_without_a_dot_lands_in_the_default_category()
    {
        var profiler = new EditorProfiler();
        profiler.Start();

        profiler.Record("flat", Ms(1));

        var root = profiler.GetRoot();
        Assert.True(root.Roots.ContainsKey("default"));
        Assert.True(root.Roots["default"].Children.ContainsKey("flat"));
    }

    [Fact]
    public void Record_ignores_negative_or_unnamed_inputs()
    {
        var profiler = new EditorProfiler();
        profiler.Start();

        profiler.Record("net.call", -1L);
        profiler.Record(null!, Ms(1));
        profiler.Record(string.Empty, Ms(1));

        Assert.Empty(profiler.GetDurationSummaries());
        Assert.Empty(profiler.GetRoot().Roots);
        Assert.Empty(profiler.GetMetrics());
    }

    // ------------------------------------------------------------- 计数器 / 频率

    [Fact]
    public void Counters_aggregate_value_min_max_and_mean()
    {
        var profiler = new EditorProfiler();
        profiler.Start();

        profiler.Increment("combat.hit"); // 累计 1
        profiler.Increment("combat.hit"); // 累计 2
        profiler.Increment("combat.hit"); // 累计 3
        profiler.Add("combat.hit", 5L);   // 累计 8

        var record = profiler.GetCounters()["combat.hit"];
        Assert.Equal(8L, record.Value);
        Assert.Equal(8L, record.Delta);
        Assert.Equal(4L, record.SampleCount);
        Assert.Equal(1L, record.MinValue); // 运行总量的最小值
        Assert.Equal(8L, record.MaxValue);
        Assert.Equal(2d, record.MeanValue, 9);
    }

    [Fact]
    public void Counter_rates_use_absolute_values_and_track_the_peak()
    {
        var profiler = new EditorProfiler();
        profiler.Start();

        profiler.Add("combat.drift", -3L);
        profiler.Add("combat.drift", 5L);

        var counter = profiler.GetCounters()["combat.drift"];
        Assert.Equal(2L, counter.Value);
        Assert.Equal(-3L, counter.MinValue);
        Assert.Equal(2L, counter.MaxValue);

        var rate = profiler.GetRates()["combat.drift"];
        Assert.Equal(8L, rate.Count1Second);   // |−3| + 5
        Assert.Equal(8L, rate.Count5Seconds);
        Assert.Equal(8L, rate.Count60Seconds);
        Assert.Equal(8L, rate.PeakPerSecond);
        Assert.Equal(2L, rate.TotalCount);
    }

    [Fact]
    public void Add_with_zero_is_ignored()
    {
        var profiler = new EditorProfiler();
        profiler.Start();

        profiler.Add("combat.hit", 0L);

        Assert.Empty(profiler.GetCounters());
        Assert.Empty(profiler.GetMetrics());
    }

    // ------------------------------------------------------------- Gauge / Sample

    [Fact]
    public void Gauges_keep_the_latest_value_and_register_the_metric()
    {
        var profiler = new EditorProfiler();
        profiler.Start();

        profiler.SetGauge("net.actors", 10L);
        profiler.SetGauge("net.actors", 20L);

        var gauge = profiler.GetGauges()["net.actors"];
        Assert.Equal("net.actors", gauge.Name);
        Assert.Equal(20L, gauge.Value);
        Assert.True(gauge.Timestamp > 0L);

        var metric = profiler.GetMetrics()["net.actors"];
        Assert.Equal(MetricKind.Gauge, metric.Kind);
        Assert.Equal("net", metric.Category);
        Assert.Equal("value", metric.Unit);
    }

    [Fact]
    public void Samples_store_values_and_reject_non_finite_inputs()
    {
        var profiler = new EditorProfiler();
        profiler.Start();

        profiler.Sample("net.delta", double.NaN);
        profiler.Sample("net.delta", double.PositiveInfinity);
        profiler.Sample("net.delta", 1.25d);

        var samples = profiler.GetSamples();
        Assert.Equal(new[] { 1.25d }, samples["net.delta"]);
        Assert.Equal(MetricKind.Sample, profiler.GetMetrics()["net.delta"].Kind);
    }

    [Fact]
    public void Samples_are_trimmed_to_the_configured_cap_keeping_the_newest()
    {
        var profiler = new EditorProfiler();
        profiler.Configure(new ProfilerOptions { MaxSamplesPerMetric = 3 });
        profiler.Start();

        for (var i = 1; i <= 5; i++)
        {
            profiler.Sample("net.delta", i);
        }

        Assert.Equal(new[] { 3d, 4d, 5d }, profiler.GetSamples()["net.delta"]);
    }

    // ------------------------------------------------------------- 事件与阈值

    [Fact]
    public void Events_are_capped_and_evict_the_oldest_first()
    {
        var profiler = new EditorProfiler();
        profiler.Configure(new ProfilerOptions { MaxDiagnosticEvents = 2 });
        profiler.Start();

        profiler.EmitEvent(DiagnosticSeverity.Info, "net", "net.e1", "one");
        profiler.EmitEvent(DiagnosticSeverity.Info, "net", "net.e2", "two");
        profiler.EmitEvent(DiagnosticSeverity.Info, "net", "net.e3", "three");

        var events = profiler.GetEvents();
        Assert.Equal(2, events.Count);
        Assert.Equal("net.e2", events[0].Name);
        Assert.Equal("net.e3", events[1].Name);
        Assert.Equal("two", events[0].Message);
    }

    [Fact]
    public void EmitEvent_falls_back_to_the_name_category_and_normalizes_nulls()
    {
        var profiler = new EditorProfiler();
        profiler.Start();

        profiler.EmitEvent(DiagnosticSeverity.Error, string.Empty, "net.fail", null!);
        profiler.EmitEvent(DiagnosticSeverity.Info, "cat", string.Empty, "ignored");

        var events = profiler.GetEvents();
        var evt = Assert.Single(events);
        Assert.Equal("net.fail", evt.Name);
        Assert.Equal("net", evt.Category); // 空分类回退到名称前缀
        Assert.Equal(string.Empty, evt.Message);
        Assert.Equal(DiagnosticSeverity.Error, evt.Severity);
    }

    [Fact]
    public void Duration_thresholds_emit_warning_then_error_events()
    {
        var profiler = new EditorProfiler();
        profiler.Start();
        profiler.ConfigureDurationThreshold("net.query", 5d, 10d);

        profiler.Record("net.query", Ms(6));
        profiler.Record("net.query", Ms(11));

        var events = profiler.GetEvents();
        Assert.Equal(2, events.Count);
        Assert.Equal(DiagnosticSeverity.Warning, events[0].Severity);
        Assert.Equal(5d, events[0].Threshold, 9);
        Assert.Equal(6d, events[0].Value, 9);
        Assert.Equal(DiagnosticSeverity.Error, events[1].Severity);
        Assert.Equal(10d, events[1].Threshold, 9);
        Assert.Equal("net", events[0].Category);
    }

    [Fact]
    public void Duration_threshold_boundary_is_inclusive()
    {
        var profiler = new EditorProfiler();
        profiler.Start();
        profiler.ConfigureDurationThreshold("net.query", 5d, 0d);

        profiler.Record("net.query", Ms(5)); // 恰好等于阈值

        var evt = Assert.Single(profiler.GetEvents());
        Assert.Equal(DiagnosticSeverity.Warning, evt.Severity);
        Assert.Equal(5d, evt.Value, 9);
    }

    [Fact]
    public void Duration_thresholds_are_removed_by_nonpositive_values()
    {
        var profiler = new EditorProfiler();
        profiler.Start();

        profiler.ConfigureDurationThreshold("net.query", 1d, 2d);
        profiler.ConfigureDurationThreshold("net.query", 0d, 0d);
        profiler.Record("net.query", Ms(100));

        Assert.Empty(profiler.GetEvents());
    }

    [Fact]
    public void Repeated_duration_threshold_violations_respect_the_cooldown_window()
    {
        var profiler = new EditorProfiler();
        profiler.Start();
        profiler.ConfigureDurationThreshold("net.query", 5d, 0d);

        profiler.Record("net.query", Ms(6));
        profiler.Record("net.query", Ms(7)); // 1 秒冷却窗口内的重复告警被抑制

        Assert.Single(profiler.GetEvents());
    }

    [Fact]
    public void Rate_thresholds_emit_warning_then_error_events()
    {
        var profiler = new EditorProfiler();
        profiler.Start();
        profiler.ConfigureRateThreshold("combat.spawn", 3L, 5L);

        for (var i = 0; i < 5; i++)
        {
            profiler.Increment("combat.spawn");
        }

        var events = profiler.GetEvents();
        Assert.Equal(2, events.Count);
        Assert.Equal(DiagnosticSeverity.Warning, events[0].Severity);
        Assert.Equal("combat.spawn", events[0].Name);
        Assert.Equal(DiagnosticSeverity.Error, events[1].Severity);
        Assert.Equal(5L, (long)events[1].Threshold);
    }

    // ------------------------------------------------------------- 采样与分类治理

    [Fact]
    public void Disabled_categories_block_every_metric_kind_case_insensitively()
    {
        var profiler = new EditorProfiler();
        var options = ProfilerOptions.CreateDefault();
        options.DisableCategory("NET"); // 大小写不敏感
        profiler.Configure(options);
        profiler.Start();

        profiler.Record("net.query", Ms(1));
        profiler.Increment("net.hits");
        profiler.SetGauge("net.frame", 1L);
        profiler.Sample("net.delta", 1d);

        Assert.Empty(profiler.GetDurationSummaries());
        Assert.Empty(profiler.GetCounters());
        Assert.Empty(profiler.GetGauges());
        Assert.Empty(profiler.GetSamples());
        Assert.Empty(profiler.GetRoot().Roots);
        Assert.Empty(profiler.GetMetrics()); // 采样决策先于指标注册

        // 其他分类不受影响
        profiler.Record("sim.tick", Ms(1));
        Assert.True(profiler.GetDurationSummaries().ContainsKey("sim.tick"));
    }

    [Fact]
    public void Metric_sample_rates_record_on_a_deterministic_interval()
    {
        var profiler = new EditorProfiler();
        var options = ProfilerOptions.CreateDefault();
        options.SetMetricSampleRate("combat.half", 0.5d);
        profiler.Configure(options);
        profiler.Start();

        for (var i = 0; i < 4; i++)
        {
            profiler.Add("combat.half", 1L);
        }

        var record = profiler.GetCounters()["combat.half"];
        Assert.Equal(2L, record.SampleCount); // 每 2 次放行 1 次
        Assert.Equal(2L, record.Value);       // 被丢弃的调用完全不贡献数值
    }

    [Fact]
    public void Category_sample_rates_apply_to_all_metrics_in_the_category()
    {
        var profiler = new EditorProfiler();
        var options = ProfilerOptions.CreateDefault();
        options.SetCategorySampleRate("sim", 0.25d);
        profiler.Configure(options);
        profiler.Start();

        for (var i = 0; i < 8; i++)
        {
            profiler.Add("sim.tick", 1L);
        }

        var record = profiler.GetCounters()["sim.tick"];
        Assert.Equal(2L, record.SampleCount); // 每 4 次放行 1 次
        Assert.Equal(2L, record.Value);       // 丢弃的调用不贡献数值
    }

    [Fact]
    public void Zero_sample_rate_drops_every_call()
    {
        var profiler = new EditorProfiler();
        var options = ProfilerOptions.CreateDefault();
        options.SetCategorySampleRate("sim", 0d);
        profiler.Configure(options);
        profiler.Start();

        for (var i = 0; i < 5; i++)
        {
            profiler.Sample("sim.tick", 1d);
        }

        Assert.Empty(profiler.GetSamples());
    }

    // ------------------------------------------------------------- 指标注册

    [Fact]
    public void RegisterMetric_normalizes_the_category_and_skips_empty_names()
    {
        var profiler = new EditorProfiler();
        profiler.RegisterMetric(new MetricDefinition { Name = "combat.hit" }); // 无分类
        profiler.RegisterMetric(new MetricDefinition { Name = "flat" });
        profiler.RegisterMetric(new MetricDefinition { Name = string.Empty });

        var metrics = profiler.GetMetrics();
        Assert.Equal(2, metrics.Count);
        Assert.Equal("combat", metrics["combat.hit"].Category);
        Assert.Equal("default", metrics["flat"].Category);

        // 注册表副本可被安全修改
        metrics["rogue"] = new MetricDefinition { Name = "rogue" };
        Assert.Equal(2, profiler.GetMetrics().Count);
    }

    [Fact]
    public void Registered_category_overrides_the_name_prefix_for_flame_roots()
    {
        var profiler = new EditorProfiler();
        profiler.RegisterMetric(new MetricDefinition { Name = "pipeline.step", Category = "flow" });
        profiler.Start();

        profiler.Record("pipeline.step", Ms(1));

        var root = profiler.GetRoot();
        Assert.True(root.Roots.ContainsKey("flow"));
        Assert.False(root.Roots.ContainsKey("pipeline"));
        Assert.True(root.Roots["flow"].Children.ContainsKey("pipeline.step"));
    }

    [Fact]
    public void Begin_registers_the_metric_definition()
    {
        var profiler = new EditorProfiler();
        profiler.Start();

        var token = profiler.Begin("net.call");
        profiler.Complete(token);

        var metric = profiler.GetMetrics()["net.call"];
        Assert.Equal(MetricKind.Duration, metric.Kind);
        Assert.Equal("net", metric.Category);
        Assert.Equal("ms", metric.Unit);

        var counterMetric = new EditorProfiler();
        counterMetric.Start();
        counterMetric.Increment("combat.hit");
        Assert.Equal(MetricKind.Counter, counterMetric.GetMetrics()["combat.hit"].Kind);
        Assert.Equal("count", counterMetric.GetMetrics()["combat.hit"].Unit);
    }

    // ------------------------------------------------------------- 快照与隔离

    [Fact]
    public void GetRoot_returns_independent_snapshots()
    {
        var profiler = new EditorProfiler();
        profiler.Start();
        profiler.Record("net.call", Ms(1));

        var first = profiler.GetRoot();
        FindNode(first, "net.call")!.AddMeasurement(999_999L);
        first.Roots["rogue"] = new FlameNode("rogue", "rogue");

        var second = profiler.GetRoot();
        Assert.False(second.Roots.ContainsKey("rogue"));
        Assert.Equal(1_000_000L, FindNode(second, "net.call")!.TotalNanoseconds);
    }

    [Fact]
    public void GetSnapshot_returns_isolated_copies()
    {
        var profiler = new EditorProfiler();
        profiler.Start();
        profiler.Increment("combat.hit");

        var snapshot = profiler.GetSnapshot();
        snapshot.Counters["rogue"] = new CounterRecord { Name = "rogue" };
        snapshot.Samples["rogue"] = new List<double> { 1d };
        snapshot.Metrics["rogue"] = new MetricDefinition { Name = "rogue" };
        snapshot.Events.Add(default);

        Assert.False(profiler.GetCounters().ContainsKey("rogue"));
        Assert.False(profiler.GetSamples().ContainsKey("rogue"));
        Assert.False(profiler.GetMetrics().ContainsKey("rogue"));
        Assert.Empty(profiler.GetEvents());
    }

    [Fact]
    public void SaveSession_appends_to_the_history_and_summarizes_counts()
    {
        var profiler = new EditorProfiler();
        profiler.Start();
        profiler.Increment("combat.hit");
        profiler.Increment("combat.hit");
        profiler.SetGauge("net.frame", 5L);
        profiler.Record("net.call", Ms(1));
        profiler.EmitEvent(DiagnosticSeverity.Warning, "net", "net.call", "msg");

        var first = profiler.SaveSession("first");

        Assert.Equal("first", first.Label);
        Assert.Equal(profiler.GetRoot().SessionId, first.SessionId);
        Assert.Equal(1, first.CounterCount);
        Assert.Equal(1, first.GaugeCount);
        Assert.Equal(1, first.SampleCount); // 仅 net.call 产生样本列表；计数器不进样本
        Assert.Equal(1, first.EventCount);
        Assert.Equal(3, first.MetricCount);
        Assert.True(first.DurationMilliseconds >= 0d);

        var second = profiler.SaveSession();
        Assert.Equal(string.Empty, second.Label);
        Assert.Equal(2, profiler.GetSessionHistory().Count);
        // 会话历史进入快照
        Assert.Equal(2, profiler.GetSnapshot().Sessions.Count);
    }
}
