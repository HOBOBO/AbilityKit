using System;
using System.Collections.Generic;
using System.Globalization;
using AbilityKit.Diagnostics;
using Xunit;

namespace AbilityKit.Diagnostics.Tests;

/// <summary>
/// ProfilerHub 是全局静态状态；该类是唯一触碰 Hub 的测试类，
/// xUnit 对同一个测试类内的用例串行执行，因此这里是安全的。
/// 每个会改动 Hub 的用例都在 finally 中恢复为 NullProfiler。
/// </summary>
public sealed class ProfilerHubTests
{
    private sealed class RecordingProfiler : IProfiler
    {
        public List<string> Calls { get; } = new List<string>();

        public bool Enabled { get; set; } = true;

        public bool IsEnabled => Enabled;

        public ProbeToken Begin(string name)
        {
            Calls.Add("Begin:" + name);
            return default;
        }

        public void Complete(ProbeToken token)
        {
            Calls.Add("Complete");
        }

        public void Record(string name, long nanoseconds)
        {
            Calls.Add("Record:" + name + ":" + nanoseconds.ToString(CultureInfo.InvariantCulture));
        }

        public void Increment(string counter)
        {
            Calls.Add("Increment:" + counter);
        }

        public void Add(string counter, long value)
        {
            Calls.Add("Add:" + counter + ":" + value.ToString(CultureInfo.InvariantCulture));
        }

        public void SetGauge(string name, long value)
        {
            Calls.Add("SetGauge:" + name + ":" + value.ToString(CultureInfo.InvariantCulture));
        }

        public void Sample(string name, double value)
        {
            Calls.Add("Sample:" + name + ":" + value.ToString(CultureInfo.InvariantCulture));
        }
    }

    [Fact]
    public void Current_defaults_to_the_null_profiler()
    {
        Assert.Same(NullProfiler.Instance, ProfilerHub.Current);
        Assert.False(ProfilerHub.IsEnabled);
        Assert.Null(ProfilerHub.GetEditorProfiler());
    }

    [Fact]
    public void SetProfiler_swaps_the_current_instance()
    {
        try
        {
            var profiler = new RecordingProfiler();
            ProfilerHub.SetProfiler(profiler);

            Assert.Same(profiler, ProfilerHub.Current);
            Assert.True(ProfilerHub.IsEnabled);
        }
        finally
        {
            ProfilerHub.SetProfiler(null);
        }
    }

    [Fact]
    public void SetProfiler_with_null_falls_back_to_the_null_profiler()
    {
        try
        {
            ProfilerHub.SetProfiler(new RecordingProfiler());
            ProfilerHub.SetProfiler(null);

            Assert.Same(NullProfiler.Instance, ProfilerHub.Current);
            Assert.False(ProfilerHub.IsEnabled);
        }
        finally
        {
            ProfilerHub.SetProfiler(null);
        }
    }

    [Fact]
    public void IsEnabled_delegates_to_the_current_profiler()
    {
        try
        {
            var profiler = new RecordingProfiler();
            ProfilerHub.SetProfiler(profiler);

            Assert.True(ProfilerHub.IsEnabled);
            profiler.Enabled = false;
            Assert.False(ProfilerHub.IsEnabled);
        }
        finally
        {
            ProfilerHub.SetProfiler(null);
        }
    }

    [Fact]
    public void Data_calls_delegate_to_the_current_profiler()
    {
        try
        {
            var profiler = new RecordingProfiler();
            ProfilerHub.SetProfiler(profiler);

            ProfilerHub.Begin("scope");
            ProfilerHub.Record("dur", 123L);
            ProfilerHub.Increment("counter");
            ProfilerHub.Add("counter", 4L);
            ProfilerHub.SetGauge("gauge", 7L);
            ProfilerHub.Sample("sample", 1.5d);

            Assert.Equal(new[]
            {
                "Begin:scope",
                "Record:dur:123",
                "Increment:counter",
                "Add:counter:4",
                "SetGauge:gauge:7",
                "Sample:sample:1.5"
            }, profiler.Calls);
        }
        finally
        {
            ProfilerHub.SetProfiler(null);
        }
    }

    [Fact]
    public void GetEditorProfiler_returns_null_for_non_editor_implementations()
    {
        try
        {
            ProfilerHub.SetProfiler(new RecordingProfiler());

            Assert.Null(ProfilerHub.GetEditorProfiler());
        }
        finally
        {
            ProfilerHub.SetProfiler(null);
        }
    }

    [Fact]
    public void GetEditorProfiler_returns_the_registered_editor_profiler()
    {
        try
        {
            var profiler = new EditorProfiler();
            ProfilerHub.SetProfiler(profiler);

            Assert.Same(profiler, ProfilerHub.GetEditorProfiler());
        }
        finally
        {
            ProfilerHub.SetProfiler(null);
        }
    }

    [Fact]
    public void Editor_only_facade_methods_are_noops_without_an_editor_profiler()
    {
        // Current 处于 NullProfiler 时，所有编辑器门面方法都不应抛出。
        ProfilerHub.Configure(null!);
        ProfilerHub.RegisterMetric(default);
        ProfilerHub.ConfigureDurationThreshold("net.query", 1d, 2d);
        ProfilerHub.ConfigureRateThreshold("combat.spawn", 1L, 2L);
        ProfilerHub.EmitEvent(DiagnosticSeverity.Warning, "net", "net.query", "message");

        Assert.Null(ProfilerHub.GetEditorProfiler());
        Assert.False(ProfilerHub.IsEnabled);
    }

    [Fact]
    public void SaveSession_returns_default_record_without_an_editor_profiler()
    {
        var record = ProfilerHub.SaveSession("label");

        Assert.Null(record.SessionId);
        Assert.Null(record.Label);
        Assert.Equal(0, record.CounterCount);
        Assert.Equal(0, record.SavedTimestamp);
    }

    [Fact]
    public void Configure_applies_options_to_the_registered_editor_profiler()
    {
        try
        {
            var profiler = new EditorProfiler();
            ProfilerHub.SetProfiler(profiler);
            ProfilerHub.Configure(new ProfilerOptions { MaxSamplesPerMetric = 5 });

            Assert.Equal(5, profiler.GetOptions().MaxSamplesPerMetric);
        }
        finally
        {
            ProfilerHub.SetProfiler(null);
        }
    }

    [Fact]
    public void StaticSampling_begins_through_the_current_profiler_and_disposes_safely()
    {
        try
        {
            var profiler = new RecordingProfiler();
            ProfilerHub.SetProfiler(profiler);

            using (StaticSampling.Sample("scope"))
            {
                Assert.Equal(new[] { "Begin:scope" }, profiler.Calls);
            }

            // 桩实现无法伪造有效 ProbeToken（构造器为 internal），
            // Dispose 作用于 default token 时必须保持无害。
            Assert.Equal(new[] { "Begin:scope" }, profiler.Calls);
        }
        finally
        {
            ProfilerHub.SetProfiler(null);
        }
    }

    [Fact]
    public void StaticSampling_on_the_null_profiler_is_a_noop()
    {
        using (StaticSampling.Sample("scope"))
        {
        }

        Assert.Same(NullProfiler.Instance, ProfilerHub.Current);
    }
}
