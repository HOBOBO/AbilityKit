using System.Buffers;
using AbilityKit.Core.Buffers;
using Xunit;

namespace AbilityKit.Core.Tests;

public sealed class PooledBufferOwnerTests
{
    [Fact]
    public void Rent_exposes_only_the_requested_logical_length()
    {
        var pool = new TrackingArrayPool<int>(new int[16]);

        using var owner = PooledBufferOwner<int>.Rent(5, pool);

        Assert.Equal(5, owner.Length);
        Assert.Equal(5, owner.Memory.Length);
        Assert.Equal(5, owner.Span.Length);
        Assert.Equal(5, owner.Segment.Count);
        Assert.Equal(16, owner.Segment.Array!.Length);
    }

    [Fact]
    public void Zero_length_does_not_touch_the_pool()
    {
        var pool = new TrackingArrayPool<byte>(new byte[8]);

        var owner = PooledBufferOwner<byte>.Rent(0, pool);
        Assert.Empty(owner.Memory.ToArray());
        owner.Dispose();

        Assert.Equal(0, pool.RentCount);
        Assert.Equal(0, pool.ReturnCount);
        Assert.True(owner.IsDisposed);
    }

    [Fact]
    public void Clear_mode_is_applied_on_rent_and_return()
    {
        var buffer = Enumerable.Repeat(7, 8).ToArray();
        var pool = new TrackingArrayPool<int>(buffer);

        var owner = PooledBufferOwner<int>.Rent(
            3,
            pool,
            PooledBufferClearMode.OnRent | PooledBufferClearMode.OnReturn);

        Assert.All(buffer, value => Assert.Equal(0, value));
        owner.Span.Fill(9);
        owner.Dispose();

        Assert.True(pool.LastClearArray);
        Assert.All(buffer, value => Assert.Equal(0, value));
    }

    [Fact]
    public void Dispose_returns_the_array_only_once_even_when_called_concurrently()
    {
        var pool = new TrackingArrayPool<int>(new int[8]);
        var owner = PooledBufferOwner<int>.Rent(4, pool);

        Parallel.For(0, 32, _ => owner.Dispose());

        Assert.Equal(1, pool.ReturnCount);
        Assert.True(owner.IsDisposed);
        Assert.Equal(4, owner.Length);
    }

    [Fact]
    public void Buffer_views_throw_after_disposal()
    {
        var owner = PooledBufferOwner<int>.Rent(4);
        owner.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = owner.Memory);
        Assert.Throws<ObjectDisposedException>(() => _ = owner.Span.Length);
        Assert.Throws<ObjectDisposedException>(() => _ = owner.Segment);
    }

    [Fact]
    public void Invalid_requests_are_rejected_without_leaking_an_undersized_array()
    {
        var pool = new TrackingArrayPool<int>(new int[2]);

        Assert.Throws<ArgumentOutOfRangeException>(() => PooledBufferOwner<int>.Rent(-1, pool));
        Assert.Throws<ArgumentOutOfRangeException>(() => PooledBufferOwner<int>.Rent(
            1,
            pool,
            (PooledBufferClearMode)4));
        Assert.Throws<InvalidOperationException>(() => PooledBufferOwner<int>.Rent(3, pool));

        Assert.Equal(1, pool.RentCount);
        Assert.Equal(1, pool.ReturnCount);
    }

    private sealed class TrackingArrayPool<T> : ArrayPool<T>
    {
        private readonly T[] _buffer;
        private int _rentCount;
        private int _returnCount;

        public TrackingArrayPool(T[] buffer)
        {
            _buffer = buffer;
        }

        public int RentCount => Volatile.Read(ref _rentCount);
        public int ReturnCount => Volatile.Read(ref _returnCount);
        public bool LastClearArray { get; private set; }

        public override T[] Rent(int minimumLength)
        {
            Interlocked.Increment(ref _rentCount);
            return _buffer;
        }

        public override void Return(T[] array, bool clearArray = false)
        {
            LastClearArray = clearArray;
            if (clearArray)
            {
                Array.Clear(array, 0, array.Length);
            }

            Interlocked.Increment(ref _returnCount);
        }
    }
}
