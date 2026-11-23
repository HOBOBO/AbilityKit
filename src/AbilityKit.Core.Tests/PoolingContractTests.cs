using AbilityKit.Core.Pooling;
using Xunit;

namespace AbilityKit.Core.Tests;

public sealed class PoolingContractTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConstructorFailureDestroysEarlierPrewarmedItems(bool failRelease)
    {
        var failure = new InvalidOperationException("预热失败");
        var items = new List<Item>();
        var attempts = 0;
        var options = new ObjectPoolOptions<Item>(() =>
        {
            if (++attempts == 3 && !failRelease) throw failure;
            var item = new Item();
            items.Add(item);
            return item;
        })
        {
            DefaultCapacity = 3,
            OnRelease = _ => { if (attempts == 3) throw failure; },
        };

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => new ObjectPool<Item>(options)));
        Assert.Equal(failRelease ? 3 : 2, items.Count);
        Assert.All(items, item => Assert.Equal(1, item.DestroyCount));
    }

    [Fact]
    public void ConstructorCleanupContinuesAndPreservesInitializationAndCleanupFailures()
    {
        var initialization = new InvalidOperationException("创建失败");
        var destruction = new InvalidOperationException("销毁失败");
        var items = new List<Item>();
        var options = new ObjectPoolOptions<Item>(() =>
        {
            if (items.Count == 2) throw initialization;
            var item = new Item();
            items.Add(item);
            return item;
        })
        {
            DefaultCapacity = 3,
            OnDestroy = _ => throw destruction,
        };

        var exception = Assert.Throws<AggregateException>(() => new ObjectPool<Item>(options));

        Assert.Equal(new Exception[] { initialization, destruction, destruction }, exception.Flatten().InnerExceptions);
        Assert.All(items, item => Assert.Equal(1, item.DestroyCount));
    }

    [Fact]
    public void OverflowDestructionRejectsReentrantReturnOfTheSameItem()
    {
        ObjectPool<Item>? pool = null;
        Exception? nested = null;
        pool = new ObjectPool<Item>(new ObjectPoolOptions<Item>(() => new Item())
        {
            MaxSize = 1,
            OnDestroy = item =>
            {
                pool!.Clear();
                nested = Record.Exception(() => pool.Release(item));
            },
        });
        var first = pool.Get();
        var overflow = pool.Get();
        pool.Release(first);
        pool.Release(overflow);

        Assert.IsType<InvalidOperationException>(nested);
        Assert.Equal(1, overflow.DestroyCount);
        Assert.Equal(0, pool.InactiveCount);
        Assert.Equal(0, pool.ActiveCount);
        Assert.Equal(1, pool.Stats.OverflowDestroyCount);
    }

    [Fact]
    public void ManagerParameterlessTrimUsesEachPoolsConfiguredPolicy()
    {
        var manager = new PoolManager();
        var none = manager.GetOrCreate<Item>("none", new ObjectPoolOptions<Item>(() => new Item())
        {
            DefaultCapacity = 3,
            TrimPolicy = PoolTrimPolicy.KeepNone,
        });
        var one = manager.GetOrCreate<Item>("one", new ObjectPoolOptions<Item>(() => new Item())
        {
            DefaultCapacity = 3,
            TrimPolicy = new PoolTrimPolicy(0, 1),
        });

        Assert.Equal(5, manager.TrimAll());
        Assert.Equal(0, none.InactiveCount);
        Assert.Equal(1, one.InactiveCount);
        Assert.Equal(1, manager.TrimAll(PoolTrimPolicy.KeepNone));
    }

    [Fact]
    public void ReleaseFailureLeavesOwnershipWithCallerAndAllowsRetry()
    {
        var failure = new InvalidOperationException("归还失败");
        var fail = true;
        var pool = new ObjectPool<Item>(new ObjectPoolOptions<Item>(() => new Item())
        {
            OnRelease = _ => { if (fail) throw failure; },
        });
        var item = pool.Get();

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => pool.Release(item)));
        Assert.Equal(0, pool.InactiveCount);
        Assert.Equal(1, pool.ActiveCount);
        Assert.Equal(0, item.DestroyCount);

        fail = false;
        pool.Release(item);
        Assert.Equal(0, pool.ActiveCount);
        Assert.Equal(2, pool.Stats.ReleaseTotal);
        Assert.Same(item, pool.Get());
        Assert.Equal(2, item.ReleaseCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitPrewarmFailureKeepsCompletedItemsAndReleasesReservations(bool failRelease)
    {
        var failure = new InvalidOperationException("显式预热失败");
        var items = new List<Item>();
        var attempts = 0;
        var fail = true;
        var pool = new ObjectPool<Item>(new ObjectPoolOptions<Item>(() =>
        {
            if (++attempts == 3 && fail && !failRelease) throw failure;
            var item = new Item();
            items.Add(item);
            return item;
        })
        {
            MaxSize = 4,
            OnRelease = _ => { if (attempts == 3 && fail) throw failure; },
        });

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => pool.Prewarm(4)));
        Assert.Equal(2, pool.InactiveCount);
        Assert.Equal(0, pool.ActiveCount);
        Assert.All(items.Take(2), item => Assert.Equal(0, item.DestroyCount));
        if (failRelease) Assert.Equal(1, items[2].DestroyCount);

        fail = false;
        pool.Prewarm(4);
        Assert.Equal(4, pool.InactiveCount);
        Assert.Equal(0, pool.ActiveCount);
        Assert.Equal(0, pool.Stats.ReleaseTotal);
    }

    [Fact]
    public void GetFailurePreservesBothDestroyFailuresAndCallbackOrder()
    {
        var getFailure = new InvalidOperationException("获取失败");
        var itemFailure = new InvalidOperationException("对象销毁失败");
        var delegateFailure = new InvalidOperationException("委托销毁失败");
        var calls = new List<string>();
        var item = new Item
        {
            OnGet = () => calls.Add("对象获取"),
            OnDestroy = () => { calls.Add("对象销毁"); throw itemFailure; },
        };
        var pool = new ObjectPool<Item>(new ObjectPoolOptions<Item>(() => item)
        {
            OnGet = _ => { calls.Add("委托获取"); throw getFailure; },
            OnDestroy = _ => { calls.Add("委托销毁"); throw delegateFailure; },
        });

        var exception = Assert.Throws<AggregateException>(() => pool.Get());

        Assert.Equal(new Exception[] { getFailure, itemFailure, delegateFailure }, exception.Flatten().InnerExceptions);
        Assert.Equal(new[] { "对象获取", "委托获取", "对象销毁", "委托销毁" }, calls);
        Assert.Equal(1, item.DestroyCount);
        Assert.Equal(0, pool.ActiveCount);
        Assert.Equal(0, pool.InactiveCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BulkCleanupCommitsSnapshotBeforeCallbacksAndKeepsReentrantReturns(bool trim)
    {
        var newestFailure = new InvalidOperationException("后归还对象销毁失败");
        var oldestFailure = new InvalidOperationException("先归还对象销毁失败");
        var calls = new List<Item>();

        var cleanupPool = new ObjectPool<Item>(new ObjectPoolOptions<Item>(() => new Item())
        {
            OnDestroy = item => calls.Add(item),
        });

        var oldest = cleanupPool.Get();
        oldest.OnDestroy = () => throw oldestFailure;
        var newest = cleanupPool.Get();
        var borrowed = cleanupPool.Get();
        newest.OnDestroy = () =>
        {
            var stats = cleanupPool.Stats;
            Assert.Equal(0, stats.InactiveCount);
            Assert.Equal(1, stats.ActiveCount);
            Assert.Equal(trim ? 0 : 2, stats.ClearDestroyCount);
            Assert.Equal(trim ? 2 : 0, stats.TrimDestroyCount);
            Assert.Throws<InvalidOperationException>(() => cleanupPool.Release(oldest));
            cleanupPool.Release(borrowed);
            throw newestFailure;
        };
        cleanupPool.Release(oldest);
        cleanupPool.Release(newest);

        var failure = Assert.Throws<AggregateException>(() =>
        {
            if (trim) cleanupPool.ForceTrim(PoolTrimPolicy.KeepNone);
            else cleanupPool.Clear(destroy: true);
        });

        Assert.Equal(new Exception[] { newestFailure, oldestFailure }, failure.InnerExceptions);
        Assert.Equal(new[] { newest, oldest }, calls);
        Assert.Equal(1, cleanupPool.InactiveCount);
        Assert.Same(borrowed, cleanupPool.Get());
    }

    [Fact]
    public void NeverTrimSkipsRegularTrimButAllowsForceTrimAndClear()
    {
        var pool = new ObjectPool<Item>(new ObjectPoolOptions<Item>(() => new Item())
        {
            DefaultCapacity = 3,
            NeverTrim = true,
            TrimPolicy = PoolTrimPolicy.KeepNone,
        });

        Assert.Equal(0, pool.Trim());
        Assert.Equal(0, pool.Trim(PoolTrimPolicy.KeepNone));
        Assert.Equal(2, pool.ForceTrim(new PoolTrimPolicy(0, 1)));
        pool.Clear(destroy: true);
        Assert.Equal(0, pool.InactiveCount);
        Assert.Equal(2, pool.Stats.TrimDestroyCount);
        Assert.Equal(1, pool.Stats.ClearDestroyCount);
    }

    [Fact]
    public void KeepAllRetainsHistoricalDefaultCapacityTarget()
    {
        var pool = new ObjectPool<Item>(new ObjectPoolOptions<Item>(() => new Item())
        {
            DefaultCapacity = 1,
        });
        pool.Prewarm(2);

        Assert.Equal(2, pool.Trim(PoolTrimPolicy.KeepAll));
        Assert.Equal(1, pool.InactiveCount);
    }

    [Fact]
    public void CopiedPooledHandleDoesNotProvideIdempotentReturn()
    {
        default(PooledObject<Item>).Dispose();
        var pool = new ObjectPool<Item>(new ObjectPoolOptions<Item>(() => new Item()));
        var handle = pool.GetPooled();
        var copy = handle;

        handle.Dispose();

        Assert.Throws<InvalidOperationException>(() => copy.Dispose());
        Assert.Equal(1, pool.InactiveCount);
        Assert.Equal(2, pool.Stats.ReleaseTotal);
    }

    [Fact]
    public void ScopeDisposeFailureClosesWrapperWithoutReclaimingActiveItemsOrRetrying()
    {
        var failure = new InvalidOperationException("作用域清理失败");
        var scope = new PoolScope("契约测试");
        var destroyCount = 0;
        var pool = scope.GetPool(new ObjectPoolOptions<Item>(() => new Item())
        {
            DefaultCapacity = 2,
            OnDestroy = _ => { destroyCount++; throw failure; },
        });
        var active = pool.Get();

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => scope.Dispose()));
        Assert.True(scope.IsDisposed);
        scope.Dispose();
        Assert.Equal(1, destroyCount);
        Assert.Throws<ObjectDisposedException>(() => scope.GetPool(new ObjectPoolOptions<Item>(() => new Item())));
        Assert.Equal(0, active.DestroyCount);
        Assert.Equal(1, pool.ActiveCount);

        pool.Release(active);
        Assert.Same(active, pool.Get());
    }

    private sealed class Item : IPoolable
    {
        public int DestroyCount { get; private set; }
        public int ReleaseCount { get; private set; }
        public Action? OnGet { get; init; }
        public Action? OnDestroy { get; set; }
        public void OnPoolGet() => OnGet?.Invoke();
        public void OnPoolRelease() => ReleaseCount++;
        public void OnPoolDestroy()
        {
            DestroyCount++;
            OnDestroy?.Invoke();
        }
    }
}
