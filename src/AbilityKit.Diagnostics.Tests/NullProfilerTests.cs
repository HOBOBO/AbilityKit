using AbilityKit.Diagnostics;
using Xunit;

namespace AbilityKit.Diagnostics.Tests;

public sealed class NullProfilerTests
{
    [Fact]
    public void Instance_is_a_singleton()
    {
        Assert.Same(NullProfiler.Instance, NullProfiler.Instance);
    }

    [Fact]
    public void IsEnabled_is_always_false()
    {
        Assert.False(NullProfiler.Instance.IsEnabled);
    }

    [Fact]
    public void Begin_returns_a_token_that_is_not_valid()
    {
        var token = NullProfiler.Instance.Begin("pipeline.execute");

        Assert.False(token.IsValid);
    }

    [Fact]
    public void Complete_never_throws_for_any_token()
    {
        var profiler = NullProfiler.Instance;
        var token = profiler.Begin("scope");

        profiler.Complete(token);
        profiler.Complete(default);
    }

    [Fact]
    public void Recording_methods_accept_degenerate_inputs_without_throwing()
    {
        var profiler = NullProfiler.Instance;

        profiler.Record(null!, -1L);
        profiler.Record(string.Empty, long.MaxValue);
        profiler.Increment(null!);
        profiler.Add(null!, long.MinValue);
        profiler.SetGauge(string.Empty, long.MinValue);
        profiler.Sample(null!, double.NaN);
        profiler.Sample("metric", double.PositiveInfinity);
    }

    [Fact]
    public void Default_token_scope_disposes_without_side_effects()
    {
        var scope = NullProfiler.Instance.Begin("scope").ToScope();

        scope.Dispose();
        scope.Dispose(); // double dispose must stay harmless
    }

    [Fact]
    public void Extensions_Sample_on_null_profiler_returns_default_scope()
    {
        IProfiler? nullProfiler = null;

        using var scope = nullProfiler.Sample("scope");
        // Dispose on the default scope is a no-op.
        scope.Dispose();
    }
}
