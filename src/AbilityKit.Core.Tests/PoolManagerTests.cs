using AbilityKit.Core.Pooling;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AbilityKit.Core.Tests;

public sealed class PoolManagerTests
{
    [Fact]
    public void Pool_config_provider_info_normalizes_missing_source()
    {
        var info = new PoolConfigProviderInfo("test", null, priority: 1, registrationOrder: 2);

        Assert.Equal(string.Empty, info.Source);
        Assert.DoesNotContain("source=", info.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Pool_config_center_registers_provider_without_source_metadata()
    {
        var provider = new EmptyPoolConfigProvider();
        PoolConfigCenter.ClearProviders();
        try
        {
            using var registration = PoolConfigCenter.RegisterProvider(provider, "test", source: null);

            Assert.Equal(string.Empty, registration.Info.Source);
            Assert.Equal(string.Empty, Assert.Single(PoolConfigCenter.GetProviderInfos()).Source);
        }
        finally
        {
            PoolConfigCenter.ClearProviders();
        }
    }

    [Fact]
    public void GetOrCreate_constructs_one_pool_for_concurrent_requests()
    {
        var manager = new PoolManager();
        var createCount = 0;
        var pools = new ConcurrentBag<ObjectPool<PoolItem>>();
        var options = new ObjectPoolOptions<PoolItem>(() =>
        {
            Interlocked.Increment(ref createCount);
            return new PoolItem();
        })
        {
            DefaultCapacity = 1,
        };

        Parallel.For(0, 16, _ => pools.Add(manager.GetOrCreate(PoolKey.Default, options)));

        Assert.Single(pools.Distinct());
        Assert.Equal(1, createCount);
    }

    [Fact]
    public void ClearAll_snapshots_registry_before_destroy_callbacks_reenter()
    {
        var manager = new PoolManager();
        var reentered = false;
        var pool = manager.GetOrCreate(
            PoolKey.Default,
            new ObjectPoolOptions<PoolItem>(() => new PoolItem())
            {
                DefaultCapacity = 1,
                OnDestroy = _ =>
                {
                    reentered = true;
                    manager.GetOrCreate(new PoolKey("reentered"), new ObjectPoolOptions<PoolItem>(() => new PoolItem()));
                },
            });

        manager.ClearAll(destroy: true);

        Assert.True(reentered);
        Assert.False(manager.TryGet(PoolKey.Default, out ObjectPool<PoolItem> _));
        Assert.True(manager.TryGet(new PoolKey("reentered"), out ObjectPool<PoolItem> _));
        Assert.Equal(0, pool.InactiveCount);
    }

    [Fact]
    public void ClearAll_continues_after_one_pool_destroy_failure()
    {
        var manager = new PoolManager();
        var destroyed = 0;
        manager.GetOrCreate(new PoolKey("first"), new ObjectPoolOptions<PoolItem>(() => new PoolItem())
        {
            DefaultCapacity = 1,
            OnDestroy = _ => throw new InvalidOperationException("first failed"),
        });
        manager.GetOrCreate(new PoolKey("second"), new ObjectPoolOptions<PoolItem>(() => new PoolItem())
        {
            DefaultCapacity = 1,
            OnDestroy = _ => Interlocked.Increment(ref destroyed),
        });

        var exception = Assert.Throws<InvalidOperationException>(() => manager.ClearAll(destroy: true));

        Assert.Equal("first failed", exception.Message);
        Assert.Equal(1, destroyed);
        Assert.False(manager.TryGet(new PoolKey("first"), out ObjectPool<PoolItem> _));
        Assert.False(manager.TryGet(new PoolKey("second"), out ObjectPool<PoolItem> _));
    }

    [Fact]
    public void Registered_pool_get_has_zero_steady_state_allocation()
    {
        var manager = new PoolManager();
        var pool = manager.GetOrCreate(
            PoolKey.Default,
            new ObjectPoolOptions<PoolItem>(() => new PoolItem()) { MaxSize = 1 });
        var item = pool.Get();
        pool.Release(item);

        for (var i = 0; i < 64; i++)
        {
            item = pool.Get();
            pool.Release(item);
        }

        long allocatedBytes = 0;
        for (var i = 0; i < 256; i++)
        {
            var allocationStart = GC.GetAllocatedBytesForCurrentThread();
            item = pool.Get();
            allocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocationStart;
            pool.Release(item);
        }

        Assert.Equal(0, allocatedBytes);
    }

    [Fact]
    public void Untyped_release_handle_tracks_an_item_moved_to_another_pool()
    {
        var manager = new PoolManager();
        var first = manager.GetOrCreate(
            new PoolKey("first"),
            new ObjectPoolOptions<PoolItem>(() => new PoolItem()));
        var second = manager.GetOrCreate(
            new PoolKey("second"),
            new ObjectPoolOptions<PoolItem>(() => new PoolItem()));
        var item = first.Get();
        second.Release(item);

        item = second.Get();
        Assert.True(manager.TryRelease(item));

        Assert.Equal(0, first.InactiveCount);
        Assert.Equal(1, second.InactiveCount);
    }

    [Fact]
    public async Task ClearAll_removes_registry_before_waiting_for_inflight_pool_construction()
    {
        using var prewarmCallbackEntered = new ManualResetEventSlim();
        using var allowPrewarmCallback = new ManualResetEventSlim();
        var manager = new PoolManager();
        var markerKey = new PoolKey("marker");
        manager.GetOrCreate(markerKey, new ObjectPoolOptions<PoolItem>(() => new PoolItem()));
        var options = new ObjectPoolOptions<PoolItem>(() => new PoolItem())
        {
            DefaultCapacity = 1,
            OnRelease = _ =>
            {
                prewarmCallbackEntered.Set();
                allowPrewarmCallback.Wait(TimeSpan.FromSeconds(5));
            },
        };

        var getTask = Task.Run(() => manager.GetOrCreate(PoolKey.Default, options));
        Assert.True(prewarmCallbackEntered.Wait(TimeSpan.FromSeconds(2)));
        var clearTask = Task.Run(() => manager.ClearAll(destroy: true));
        Assert.True(SpinWait.SpinUntil(() => !manager.TryGet(markerKey, out ObjectPool<PoolItem> _), TimeSpan.FromSeconds(2)));

        allowPrewarmCallback.Set();
        var removedPool = await getTask.WaitAsync(TimeSpan.FromSeconds(2));
        await clearTask.WaitAsync(TimeSpan.FromSeconds(2));

        var replacement = manager.GetOrCreate(PoolKey.Default, new ObjectPoolOptions<PoolItem>(() => new PoolItem()));
        Assert.NotSame(removedPool, replacement);
        Assert.Equal(0, removedPool.InactiveCount);
    }

    [Fact]
    public void PoolRegistry_ClearAll_continues_after_one_scope_fails()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var failingName = "failing-" + suffix;
        var succeedingName = "succeeding-" + suffix;
        var destroyed = 0;
        var failingScope = PoolRegistry.GetOrCreateScope(failingName);
        var succeedingScope = PoolRegistry.GetOrCreateScope(succeedingName);
        failingScope.GetPool(new ObjectPoolOptions<PoolItem>(() => new PoolItem())
        {
            DefaultCapacity = 1,
            OnDestroy = _ => throw new InvalidOperationException("scope failed"),
        });
        succeedingScope.GetPool(new ObjectPoolOptions<PoolItem>(() => new PoolItem())
        {
            DefaultCapacity = 1,
            OnDestroy = _ => Interlocked.Increment(ref destroyed),
        });

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => PoolRegistry.ClearAll(destroy: true, includeGlobal: false));

            Assert.Equal("scope failed", exception.Message);
            Assert.Equal(1, destroyed);
        }
        finally
        {
            PoolRegistry.DestroyScope(failingName, destroy: false);
            PoolRegistry.DestroyScope(succeedingName, destroy: false);
        }
    }

    private sealed class PoolItem
    {
    }

    private sealed class EmptyPoolConfigProvider : IPoolConfigProvider
    {
        public bool TryGetConfig(PoolConfigRequest request, out PoolItemConfig config)
        {
            config = PoolItemConfig.Unspecified;
            return false;
        }
    }
}
