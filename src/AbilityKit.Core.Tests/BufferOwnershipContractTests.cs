using System.Runtime.CompilerServices;
using AbilityKit.Core.Buffers;
using Xunit;

namespace AbilityKit.Core.Tests;

public sealed class BufferOwnershipContractTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void ListAppendDoesNotAllocateWhenFirstReachingFullCapacity(int capacity)
    {
        var warmup = new ListSequentialBuffer<int>(capacity);
        for (var index = 0; index <= capacity; index++) warmup.AddLast(index);

        var buffer = new ListSequentialBuffer<int>(capacity);
        for (var index = 0; index < capacity; index++) buffer.AddLast(index);
        var before = GC.GetAllocatedBytesForCurrentThread();
        buffer.AddLast(capacity);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.Equal(Enumerable.Range(1, capacity), Enumerable.Range(0, capacity).Select(index => buffer[index]));

        Assert.True(buffer.TrySetCapacity(capacity + 2));
        buffer.AddLast(capacity + 1);
        buffer.AddLast(capacity + 2);
        before = GC.GetAllocatedBytesForCurrentThread();
        buffer.AddLast(capacity + 3);
        allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.Equal(Enumerable.Range(2, capacity + 2), Enumerable.Range(0, buffer.Count).Select(index => buffer[index]));
    }

    [Fact]
    public void AllBackendsReleaseReferencesAfterRemovalWithoutDisposingElements()
    {
        var cases = new List<(string Name, object Buffer, WeakReference Reference)>();
        foreach (var ring in new[] { false, true })
        {
            foreach (var operation in new[] { "replace", "evict", "first", "middle", "before", "after", "clear", "shrink", "ignore" })
                cases.Add(CreateFrameCase(ring, operation));
            foreach (var operation in new[] { "evict", "clear", "shrink" })
                cases.Add(CreateSequenceCase(ring, operation));
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        foreach (var item in cases) Assert.False(item.Reference.IsAlive, item.Name);
        GC.KeepAlive(cases);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StoredReferencesRemainSharedAndNullIsAValidFrameValue(bool ring)
    {
        IFrameIndexedBuffer<List<int>?> frames = ring
            ? new RingFrameIndexedBuffer<List<int>?>(2)
            : new SparseFrameIndexedBuffer<List<int>?>(2);
        var value = new List<int> { 1 };
        frames.Store(int.MinValue, value);
        frames.Store(int.MaxValue, null);
        value.Add(2);

        Assert.True(frames.TryGet(int.MinValue, out var retained));
        Assert.Same(value, retained);
        Assert.Equal(new[] { 1, 2 }, retained);
        Assert.True(frames.TryGet(int.MaxValue, out var empty));
        Assert.Null(empty);
        Assert.False(frames.TryGet(0, out var missing));
        Assert.Null(missing);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (string, object, WeakReference) CreateFrameCase(bool ring, string operation)
    {
        IFrameIndexedBuffer<CallerOwnedValue> buffer = ring
            ? new RingFrameIndexedBuffer<CallerOwnedValue>(3)
            : new SparseFrameIndexedBuffer<CallerOwnedValue>(3);
        /*
         * 先让环形后端的头部跨过数组末尾，再验证不同移除路径。
         */
        for (var frame = 1; frame <= 5; frame++) buffer.Store(frame, new CallerOwnedValue());
        var target = new CallerOwnedValue();
        var reference = new WeakReference(target);
        var key = operation == "after" ? 5 : operation == "ignore" ? 1 : operation == "middle" ? 4 : 3;
        buffer.Store(key, target);
        switch (operation)
        {
            case "replace": buffer.Store(key, new CallerOwnedValue()); break;
            case "evict": buffer.Store(6, new CallerOwnedValue()); break;
            case "first":
            case "middle": Assert.True(buffer.Remove(key)); break;
            case "before": buffer.RemoveBefore(4); break;
            case "after": buffer.RemoveAfter(4); break;
            case "clear": buffer.Clear(); break;
            case "shrink": Assert.True(buffer.TrySetCapacity(1)); break;
            case "ignore": break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }

        Assert.Equal(0, target.DisposeCount);
        return ($"frame/{ring}/{operation}", buffer, reference);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (string, object, WeakReference) CreateSequenceCase(bool ring, string operation)
    {
        ISequentialBuffer<CallerOwnedValue> buffer = ring
            ? new RingSequentialBuffer<CallerOwnedValue>(3)
            : new ListSequentialBuffer<CallerOwnedValue>(3);
        for (var index = 0; index < 5; index++) buffer.AddLast(new CallerOwnedValue());
        var target = new CallerOwnedValue();
        var reference = new WeakReference(target);
        buffer.AddLast(target);
        buffer.AddLast(new CallerOwnedValue());
        buffer.AddLast(new CallerOwnedValue());
        switch (operation)
        {
            case "evict": buffer.AddLast(new CallerOwnedValue()); break;
            case "clear": buffer.Clear(); break;
            case "shrink": Assert.True(buffer.TrySetCapacity(1)); break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }

        Assert.Equal(0, target.DisposeCount);
        return ($"sequence/{ring}/{operation}", buffer, reference);
    }

    private sealed class CallerOwnedValue : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }
}
