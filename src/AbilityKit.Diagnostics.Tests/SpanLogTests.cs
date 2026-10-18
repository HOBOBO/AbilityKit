using System.Linq;
using AbilityKit.Diagnostics;
using Xunit;

namespace AbilityKit.Diagnostics.Tests;

/// <summary>
/// 时间线 span 环形缓冲（L2 时间维度 + L3 关联上下文）契约：
/// 完成/外部测量都进缓冲、帧号与 ContextId 正确透传、环形覆盖最旧记录并计数丢弃。
/// </summary>
public sealed class SpanLogTests
{
    [Fact]
    public void Completed_scopes_are_recorded_with_context_and_frame()
    {
        var profiler = new EditorProfiler();
        profiler.Start();

        profiler.SetFrame(41);
        using (profiler.Begin("world.tick", 1234L).ToScope())
        {
            using (profiler.Begin("moba.effect.action", 5678L).ToScope()) { }
        }
        profiler.Stop();

        var snapshot = profiler.GetSnapshot();
        Assert.Equal(2, snapshot.Spans.Count);

        var action = snapshot.Spans.First(s => s.Name == "moba.effect.action");
        Assert.Equal(5678L, action.ContextId);
        Assert.Equal(41, action.Frame);
        Assert.True(action.DurationNanoseconds >= 0L);

        var tick = snapshot.Spans.First(s => s.Name == "world.tick");
        Assert.Equal(1234L, tick.ContextId);
        Assert.True(tick.DurationNanoseconds >= action.DurationNanoseconds, "外层作用域时长应不小于内层");
    }

    [Fact]
    public void External_record_adds_span_with_synthesized_start()
    {
        var profiler = new EditorProfiler();
        profiler.Start();

        profiler.SetFrame(7);
        profiler.Record("moba.damage.pipeline", 2_500_000L);
        profiler.Stop();

        var snapshot = profiler.GetSnapshot();
        var span = Assert.Single(snapshot.Spans);
        Assert.Equal("moba.damage.pipeline", span.Name);
        Assert.Equal(2_500_000L, span.DurationNanoseconds);
        Assert.Equal(7, span.Frame);
        Assert.Equal(0L, span.ContextId);
    }

    [Fact]
    public void Ring_buffer_overwrites_oldest_and_counts_dropped()
    {
        var profiler = new EditorProfiler();
        profiler.Configure(new ProfilerOptions { Enabled = true, MaxSpanEvents = 2 });
        profiler.Start();

        foreach (var index in new[] { 1, 2, 3, 4, 5 })
        {
            using (profiler.Begin("span." + index).ToScope()) { }
        }
        profiler.Stop();

        var snapshot = profiler.GetSnapshot();
        Assert.Equal(2, snapshot.Spans.Count);
        Assert.Equal(3, snapshot.SpansDropped);
        Assert.Equal("span.4", snapshot.Spans[0].Name);
        Assert.Equal("span.5", snapshot.Spans[1].Name);
    }

    [Fact]
    public void Zero_capacity_disables_span_log()
    {
        var profiler = new EditorProfiler();
        profiler.Configure(new ProfilerOptions { Enabled = true, MaxSpanEvents = 0 });
        profiler.Start();

        using (profiler.Begin("no.span").ToScope()) { }
        profiler.Stop();

        var snapshot = profiler.GetSnapshot();
        Assert.Empty(snapshot.Spans);
    }
}
