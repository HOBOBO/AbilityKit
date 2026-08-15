using AbilityKit.Core.Pooling;
using Xunit;

namespace AbilityKit.Core.Tests;

public sealed class ObjectPoolTests
{
    [Fact]
    public void Pool_runs_lifecycle_and_reuses_released_instance()
    {
        var pool = new ObjectPool<PoolItem>(new ObjectPoolOptions<PoolItem>(() => new PoolItem())
        {
            MaxSize = 2,
            CollectionCheck = true,
        });

        var item = pool.Get();
        pool.Release(item);
        var reused = pool.Get();

        Assert.Same(item, reused);
        Assert.Equal(2, item.GetCount);
        Assert.Equal(1, item.ReleaseCount);
        Assert.Equal(1, pool.Stats.HitCount);
        Assert.Equal(1, pool.Stats.MissCount);
    }

    [Fact]
    public void Collection_check_rejects_duplicate_release_in_all_builds()
    {
        var pool = new ObjectPool<PoolItem>(new ObjectPoolOptions<PoolItem>(() => new PoolItem())
        {
            CollectionCheck = true,
        });
        var item = pool.Get();
        pool.Release(item);

        Assert.Throws<InvalidOperationException>(() => pool.Release(item));
        Assert.Equal(1, pool.InactiveCount);
    }

    [Fact]
    public void Collection_check_uses_reference_identity()
    {
        var pool = new ObjectPool<ValueEqualPoolItem>(new ObjectPoolOptions<ValueEqualPoolItem>(() => new ValueEqualPoolItem())
        {
            CollectionCheck = true,
        });
        var first = pool.Get();
        var second = pool.Get();

        pool.Release(first);
        pool.Release(second);

        Assert.Equal(2, pool.InactiveCount);
    }

    [Fact]
    public void Overflow_destroys_item_instead_of_retaining_it()
    {
        var pool = new ObjectPool<PoolItem>(new ObjectPoolOptions<PoolItem>(() => new PoolItem())
        {
            MaxSize = 1,
        });
        var first = pool.Get();
        var second = pool.Get();

        pool.Release(first);
        pool.Release(second);

        Assert.Equal(1, pool.InactiveCount);
        Assert.Equal(1, second.DestroyCount);
        Assert.Equal(1, pool.Stats.OverflowDestroyCount);
    }

    private class PoolItem : IPoolable
    {
        public int GetCount { get; private set; }
        public int ReleaseCount { get; private set; }
        public int DestroyCount { get; private set; }

        public void OnPoolGet() => GetCount++;
        public void OnPoolRelease() => ReleaseCount++;
        public void OnPoolDestroy() => DestroyCount++;
    }

    private sealed class ValueEqualPoolItem : PoolItem
    {
        public override bool Equals(object? obj) => obj is ValueEqualPoolItem;
        public override int GetHashCode() => 1;
    }
}
