using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using AbilityKit.Diagnostics;
using AbilityKit.Demo.Moba.Console;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>
/// 冒烟性能分析管线自校验：安装 EditorProfiler → 采集嵌套探针 → 导出火焰图产物。
/// 保证 ABILITYKIT_MOBA_PROFILE=1 的冒烟采样路径（MobaConsoleProfiling + 导出器投影工程）可用。
/// </summary>
public sealed class MobaConsoleProfilingTests
{
    [Fact]
    [Trait("Gate", "MobaConsoleSmoke")]
    [Trait("Category", "MobaConsole")]
    [Trait("Category", "Smoke")]
    public void Profile_session_exports_flame_graph_artifacts()
    {
        var directory = Path.Combine(Path.GetTempPath(), "abilitykit-moba-profile-tests", Path.GetRandomFileName());
        using var session = MobaConsoleProfileSession.Begin("unit-test", directory);

        Assert.True(ProfilerHub.IsEnabled);
        using (ProfilerHub.Begin("moba.unit.outer").ToScope())
        {
            using (ProfilerHub.Begin("moba.unit.inner").ToScope()) { }
            ProfilerHub.Increment("moba.unit.counter");
            ProfilerHub.Record("moba.unit.standalone", 250000);
        }

        var export = session.Export();

        Assert.Equal(5, export.Files.Count);
        foreach (var file in export.Files)
        {
            Assert.True(File.Exists(file), file);
        }

        var flameSvg = File.ReadAllText(FirstFile(export, ".flame.svg"));
        Assert.StartsWith("<svg", flameSvg);
        Assert.Contains("moba.unit.outer", flameSvg);

        var folded = File.ReadAllText(FirstFile(export, ".folded"));
        Assert.Contains("moba.unit.outer;moba.unit.inner", folded);

        using var speedscope = JsonDocument.Parse(File.ReadAllText(FirstFile(export, ".speedscope.json")));
        Assert.True(speedscope.RootElement.GetProperty("shared").GetProperty("frames").GetArrayLength() > 0);

        Assert.True(export.FlameNodeCount > 0);
        Assert.True(export.CounterCount > 0);
        Assert.True(export.DurationCount > 0);
    }

    [Fact]
    [Trait("Gate", "MobaConsoleSmoke")]
    [Trait("Category", "MobaConsole")]
    public void Profile_session_dispose_restores_null_profiler()
    {
        var directory = Path.Combine(Path.GetTempPath(), "abilitykit-moba-profile-tests", Path.GetRandomFileName());
        var session = MobaConsoleProfileSession.Begin("unit-test-teardown", directory);

        Assert.True(ProfilerHub.IsEnabled);
        session.Dispose();

        Assert.False(ProfilerHub.IsEnabled);
        Assert.True(Directory.GetFiles(directory).Length > 0, "Dispose must still export artifacts before restoring the null profiler.");
    }

    private static string FirstFile(MobaConsoleProfileExport export, string suffix)
    {
        return export.Files.First(f => f.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }
}
