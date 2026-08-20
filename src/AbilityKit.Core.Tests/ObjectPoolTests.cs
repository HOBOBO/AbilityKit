using AbilityKit.Core.Pooling;
using System;
using System.Threading;
using System.Threading.Tasks;
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

    [Fact]
    public void Get_callback_failure_discards_the_item_and_preserves_counts()
    {
        PoolItem? created = null;
        var pool = new ObjectPool<PoolItem>(new ObjectPoolOptions<PoolItem>(() => created = new PoolItem())
        {
            OnGet = _ => throw new InvalidOperationException("get failed"),
        });

        var exception = Assert.Throws<InvalidOperationException>(() => pool.Get());

        Assert.Equal("get failed", exception.Message);
        Assert.NotNull(created);
        Assert.Equal(1, created.DestroyCount);
        Assert.Equal(1, pool.Stats.CreatedTotal);
        Assert.Equal(1, pool.Stats.GetTotal);
        Assert.Equal(0, pool.ActiveCount);
        Assert.Equal(0, pool.InactiveCount);
    }

    [Fact]
    public void Prewarm_callback_failure_discards_the_created_item()
    {
        PoolItem? created = null;
        var pool = new ObjectPool<PoolItem>(new ObjectPoolOptions<PoolItem>(() => created = new PoolItem())
        {
            OnRelease = _ => throw new InvalidOperationException("prewarm failed"),
        });

        var exception = Assert.Throws<InvalidOperationException>(() => pool.Prewarm(1));

        Assert.Equal("prewarm failed", exception.Message);
        Assert.NotNull(created);
        Assert.Equal(1, created.DestroyCount);
        Assert.Equal(1, pool.Stats.CreatedTotal);
        Assert.Equal(0, pool.ActiveCount);
        Assert.Equal(0, pool.InactiveCount);
    }

    [Fact]
    public void Clear_destroy_continues_after_a_destroy_callback_failure()
    {
        var callbackCount = 0;
        var pool = new ObjectPool<PoolItem>(new ObjectPoolOptions<PoolItem>(() => new PoolItem())
        {
            DefaultCapacity = 3,
            OnDestroy = _ =>
            {
                callbackCount++;
                if (callbackCount == 1) throw new InvalidOperationException("destroy failed");
            },
        });

        var exception = Assert.Throws<InvalidOperationException>(() => pool.Clear(destroy: true));

        Assert.Equal("destroy failed", exception.Message);
        Assert.Equal(3, callbackCount);
        Assert.Equal(3, pool.Stats.ClearDestroyCount);
        Assert.Equal(0, pool.ActiveCount);
        Assert.Equal(0, pool.InactiveCount);
    }

    [Fact]
    public void Trim_continues_after_a_destroy_callback_failure()
    {
        var callbackCount = 0;
        var pool = new ObjectPool<PoolItem>(new ObjectPoolOptions<PoolItem>(() => new PoolItem())
        {
            DefaultCapacity = 3,
            OnDestroy = _ =>
            {
                callbackCount++;
                if (callbackCount == 1) throw new InvalidOperationException("trim failed");
            },
        });

        var exception = Assert.Throws<InvalidOperationException>(() => pool.ForceTrim(PoolTrimPolicy.KeepNone));

        Assert.Equal("trim failed", exception.Message);
        Assert.Equal(3, callbackCount);
        Assert.Equal(3, pool.Stats.TrimDestroyCount);
        Assert.Equal(0, pool.ActiveCount);
        Assert.Equal(0, pool.InactiveCount);
    }

    [Fact]
    public void Destroy_invokes_option_callback_when_poolable_callback_fails()
    {
        var optionCallbackCount = 0;
        var pool = new ObjectPool<ThrowingDestroyPoolItem>(
            new ObjectPoolOptions<ThrowingDestroyPoolItem>(() => new ThrowingDestroyPoolItem())
            {
                DefaultCapacity = 1,
                OnDestroy = _ => optionCallbackCount++,
            });

        var exception = Assert.Throws<InvalidOperationException>(() => pool.Clear(destroy: true));

        Assert.Equal("poolable destroy failed", exception.Message);
        Assert.Equal(1, optionCallbackCount);
        Assert.Equal(1, pool.Stats.ClearDestroyCount);
        Assert.Equal(0, pool.InactiveCount);
    }

    [Fact]
    public void Overflow_counter_is_committed_before_destroy_callback_failure()
    {
        var pool = new ObjectPool<PoolItem>(new ObjectPoolOptions<PoolItem>(() => new PoolItem())
        {
            MaxSize = 1,
            OnDestroy = _ => throw new InvalidOperationException("overflow destroy failed"),
        });
        var first = pool.Get();
        var second = pool.Get();
        pool.Release(first);

        var exception = Assert.Throws<InvalidOperationException>(() => pool.Release(second));

        Assert.Equal("overflow destroy failed", exception.Message);
        Assert.Equal(1, pool.Stats.OverflowDestroyCount);
        Assert.Equal(0, pool.ActiveCount);
        Assert.Equal(1, pool.InactiveCount);
    }

    [Fact]
    public async Task Get_callback_runs_without_holding_pool_lock()
    {
        using var callbackEntered = new ManualResetEventSlim();
        using var allowCallback = new ManualResetEventSlim();
        var pool = new ObjectPool<PoolItem>(new ObjectPoolOptions<PoolItem>(() => new PoolItem())
        {
            OnGet = _ =>
            {
                callbackEntered.Set();
                allowCallback.Wait(TimeSpan.FromSeconds(5));
            },
        });

        var getTask = Task.Run(() => pool.Get());
        Assert.True(callbackEntered.Wait(TimeSpan.FromSeconds(2)));
        var statsTask = Task.Run(() => pool.Stats);

        _ = await statsTask.WaitAsync(TimeSpan.FromSeconds(2));
        allowCallback.Set();
        _ = await getTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Concurrent_releases_run_callbacks_concurrently_but_respect_max_size()
    {
        using var callbacksEntered = new CountdownEvent(2);
        using var allowCallbacks = new ManualResetEventSlim();
        var pool = new ObjectPool<PoolItem>(new ObjectPoolOptions<PoolItem>(() => new PoolItem())
        {
            MaxSize = 1,
            OnRelease = _ =>
            {
                callbacksEntered.Signal();
                allowCallbacks.Wait(TimeSpan.FromSeconds(5));
            },
        });
        var first = pool.Get();
        var second = pool.Get();

        var firstRelease = Task.Run(() => pool.Release(first));
        var secondRelease = Task.Run(() => pool.Release(second));

        Assert.True(callbacksEntered.Wait(TimeSpan.FromSeconds(2)));
        allowCallbacks.Set();
        await Task.WhenAll(firstRelease, secondRelease);

        Assert.Equal(1, pool.InactiveCount);
        Assert.Equal(0, pool.ActiveCount);
        Assert.Equal(1, pool.Stats.OverflowDestroyCount);
    }

    [Fact]
    public async Task Concurrent_prewarm_and_release_respect_reserved_capacity()
    {
        using var prewarmCallbackEntered = new ManualResetEventSlim();
        using var callbacksEntered = new CountdownEvent(2);
        using var allowCallbacks = new ManualResetEventSlim();
        var callbackCount = 0;
        var pool = new ObjectPool<PoolItem>(new ObjectPoolOptions<PoolItem>(() => new PoolItem())
        {
            MaxSize = 1,
            OnRelease = _ =>
            {
                if (Interlocked.Increment(ref callbackCount) == 1)
                {
                    prewarmCallbackEntered.Set();
                }

                callbacksEntered.Signal();
                allowCallbacks.Wait(TimeSpan.FromSeconds(5));
            },
        });
        var checkedOut = pool.Get();

        var prewarmTask = Task.Run(() => pool.Prewarm(1));
        Assert.True(prewarmCallbackEntered.Wait(TimeSpan.FromSeconds(2)));
        var releaseTask = Task.Run(() => pool.Release(checkedOut));

        Assert.True(callbacksEntered.Wait(TimeSpan.FromSeconds(2)));
        allowCallbacks.Set();
        await Task.WhenAll(prewarmTask, releaseTask);

        Assert.Equal(1, pool.InactiveCount);
        Assert.Equal(0, pool.ActiveCount);
        Assert.Equal(1, pool.Stats.OverflowDestroyCount);
    }

    [Fact]
    public async Task Duplicate_release_is_rejected_while_release_callback_is_running()
    {
        using var callbackEntered = new ManualResetEventSlim();
        using var allowCallback = new ManualResetEventSlim();
        var pool = new ObjectPool<PoolItem>(new ObjectPoolOptions<PoolItem>(() => new PoolItem())
        {
            OnRelease = _ =>
            {
                callbackEntered.Set();
                allowCallback.Wait(TimeSpan.FromSeconds(5));
            },
        });
        var item = pool.Get();
        var firstRelease = Task.Run(() => pool.Release(item));

        Assert.True(callbackEntered.Wait(TimeSpan.FromSeconds(2)));
        var duplicate = Assert.Throws<InvalidOperationException>(() => pool.Release(item));
        allowCallback.Set();
        await firstRelease.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Contains("transitioning", duplicate.Message, StringComparison.Ordinal);
        Assert.Equal(1, pool.InactiveCount);
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

    private sealed class ThrowingDestroyPoolItem : IPoolable
    {
        public void OnPoolGet()
        {
        }

        public void OnPoolRelease()
        {
        }

        public void OnPoolDestroy() => throw new InvalidOperationException("poolable destroy failed");
    }
}
