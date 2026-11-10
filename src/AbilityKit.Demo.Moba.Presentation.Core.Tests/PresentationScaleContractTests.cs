using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.Core.Tests;

public sealed class PresentationScaleContractTests
{
    [Fact]
    public void Diagnostic_snapshot_is_sorted_filtered_and_cannot_mutate_the_ledger()
    {
        var resolver = new PresentationScaleResolver();
        resolver.Set(new PresentationScaleSource("z-vfx", PresentationScaleChannel.Vfx, 2f));
        resolver.Set(new PresentationScaleSource("a-both", PresentationScaleChannel.Vfx | PresentationScaleChannel.Avatar, 0.5f));
        resolver.Set(new PresentationScaleSource("model", PresentationScaleChannel.Model, 3f));
        var snapshot = new List<PresentationScaleSource>();
        resolver.CopySources(snapshot, PresentationScaleChannel.Vfx);
        Assert.Equal(new[] { "a-both", "z-vfx" }, snapshot.Select(source => source.SourceId));
        snapshot.Clear();
        Assert.Equal(3, resolver.Count);
        Assert.Equal(1f, resolver.ResolveScalar(PresentationScaleChannel.Vfx));
        resolver.CopySources(snapshot, PresentationScaleChannel.None);
        Assert.Empty(snapshot);
    }

    [Fact]
    public void Resolver_ReplacesAndRemovesNamedSourcesWithoutAccumulation()
    {
        var resolver = new PresentationScaleResolver();
        resolver.Set(new PresentationScaleSource("skin", PresentationScaleChannel.Avatar, 2f));
        resolver.Set(new PresentationScaleSource("aura", PresentationScaleChannel.Avatar, 0.5f));

        var first = resolver.Resolve(PresentationScaleChannel.Avatar, new PresentationVector3(1.5f, 1.5f, 1.5f));
        Assert.Equal(1.5f, first.X);

        resolver.Set(new PresentationScaleSource("skin", PresentationScaleChannel.Avatar, 3f));
        var replaced = resolver.Resolve(PresentationScaleChannel.Avatar, new PresentationVector3(1.5f, 1.5f, 1.5f));
        Assert.Equal(2.25f, replaced.X);

        Assert.True(resolver.Remove("aura"));
        var removed = resolver.Resolve(PresentationScaleChannel.Avatar, new PresentationVector3(1.5f, 1.5f, 1.5f));
        Assert.Equal(4.5f, removed.X);
    }

    [Fact]
    public void Resolver_ComposesVectorSourcesAndDeterministicOverride()
    {
        var resolver = new PresentationScaleResolver();
        resolver.Set(new PresentationScaleSource("base-buff", PresentationScaleChannel.Model,
            new PresentationVector3(2f, 1f, 0.5f)));
        resolver.Set(new PresentationScaleSource("override-a", PresentationScaleChannel.Model,
            1.5f, PresentationScaleMode.Override, priority: 3));
        resolver.Set(new PresentationScaleSource("override-b", PresentationScaleChannel.Model,
            new PresentationVector3(0.5f, 2f, 1f), PresentationScaleMode.Override, priority: 3));

        var result = resolver.Resolve(PresentationScaleChannel.Model, new PresentationVector3(2f, 3f, 4f));
        // Equal priorities use the ordinally greatest SourceId (override-b).
        Assert.Equal(2f, result.X);
        Assert.Equal(6f, result.Y);
        Assert.Equal(2f, result.Z);
    }
}
