using AbilityKit.Core.Snapshots.Routing;
using Xunit;

namespace AbilityKit.World.Snapshot.Tests;

/// <summary>
/// world.snapshot 包快照路由的直接契约测试（脱离 demo）。
/// 覆盖 SnapshotRegistryCatalog 的成功 + 失败 + 边界用例。
/// </summary>
public sealed class SnapshotRoutingTests
{
    [Fact]
    public void Add_then_TryGet_finds_by_id()
    {
        var catalog = new SnapshotRegistryCatalog();
        catalog.Add("hero", (dec, cur, stage, cmd) => { });

        Assert.True(catalog.TryGet("hero", out var registry));
        Assert.NotNull(registry);
        Assert.Equal("hero", registry.RegistryId);
        Assert.Single(catalog.Registries);
    }

    [Fact]
    public void TryGet_unknown_id_returns_false()
    {
        var catalog = new SnapshotRegistryCatalog();
        Assert.False(catalog.TryGet("missing", out var registry));
        Assert.Null(registry);
    }

    [Fact]
    public void TryGet_null_id_returns_false()
    {
        var catalog = new SnapshotRegistryCatalog();
        Assert.False(catalog.TryGet(null!, out var registry));
        Assert.Null(registry);
    }

    [Fact]
    public void Add_duplicate_id_throws()
    {
        var catalog = new SnapshotRegistryCatalog();
        catalog.Add("hero", (dec, cur, stage, cmd) => { });

        Assert.Throws<InvalidOperationException>(() =>
            catalog.Add("hero", (dec, cur, stage, cmd) => { }));
    }

    [Fact]
    public void Add_null_registry_throws()
    {
        var catalog = new SnapshotRegistryCatalog();
        Assert.Throws<ArgumentNullException>(() => catalog.Add((IIdentifiedSnapshotRegistry)null!));
    }
}
