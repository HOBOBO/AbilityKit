using AbilityKit.Core.Buffers;
using System.Runtime.CompilerServices;
using Xunit;

namespace AbilityKit.Core.Tests;

public sealed class BufferStorageTests
{
    public static TheoryData<IFrameIndexedBuffer<string>> FrameBackends => new()
    {
        new SparseFrameIndexedBuffer<string>(3),
        new RingFrameIndexedBuffer<string>(3),
    };

    public static TheoryData<ISequentialBuffer<int>> SequentialBackends => new()
    {
        new ListSequentialBuffer<int>(3),
        new RingSequentialBuffer<int>(3),
    };

    [Theory]
    [MemberData(nameof(FrameBackends))]
    public void FrameBackendsRetainNewestKeysAndSupportOutOfOrderUpdates(
        IFrameIndexedBuffer<string> storage)
    {
        storage.Store(30, "thirty");
        storage.Store(10, "ten");
        storage.Store(20, "twenty");
        storage.Store(20, "replacement");
        storage.Store(40, "forty");

        Assert.Equal(3, storage.Count);
        Assert.Equal(new[] { 20, 30, 40 }, Frames(storage));
        Assert.True(storage.TryGet(20, out var replacement));
        Assert.Equal("replacement", replacement);

        storage.RemoveBefore(30);
        storage.RemoveAfter(30);

        Assert.Equal(new[] { 30 }, Frames(storage));
    }

    [Theory]
    [MemberData(nameof(FrameBackends))]
    public void FrameBackendsReturnNullWhenAReferenceValueIsMissing(IFrameIndexedBuffer<string> storage)
    {
        Assert.False(storage.TryGet(404, out var value));
        Assert.Null(value);
    }

    [Fact]
    public void RingFrameBackendResizesAndRetainsNewestFramesInOrder()
    {
        var storage = new RingFrameIndexedBuffer<int>(4);
        storage.Store(1, 10);
        storage.Store(2, 20);
        storage.Store(3, 30);
        storage.Store(4, 40);

        Assert.True(storage.TrySetCapacity(2));
        Assert.Equal(new[] { 3, 4 }, Frames(storage));

        Assert.True(storage.TrySetCapacity(5));
        storage.Store(5, 50);
        storage.Store(0, 0);

        Assert.Equal(new[] { 0, 3, 4, 5 }, Frames(storage));
    }

    [Fact]
    public void RingFrameBackendMatchesOrderedModelAcrossMixedOperations()
    {
        var random = new Random(1729);
        var storage = new RingFrameIndexedBuffer<int>(5);
        var model = new SortedDictionary<int, int>();
        var capacity = 5;

        for (var step = 0; step < 1000; step++)
        {
            var frame = random.Next(-20, 81);
            switch (random.Next(5))
            {
                case 0:
                case 1:
                    storage.Store(frame, step);
                    model[frame] = step;
                    while (model.Count > capacity) model.Remove(model.Keys.First());
                    break;
                case 2:
                    Assert.Equal(model.Remove(frame), storage.Remove(frame));
                    break;
                case 3:
                    storage.RemoveBefore(frame);
                    foreach (var key in model.Keys.Where(key => key < frame).ToArray()) model.Remove(key);
                    break;
                default:
                    if ((step & 1) == 0)
                    {
                        storage.RemoveAfter(frame);
                        foreach (var key in model.Keys.Where(key => key > frame).ToArray()) model.Remove(key);
                    }
                    else
                    {
                        capacity = random.Next(1, 9);
                        Assert.True(storage.TrySetCapacity(capacity));
                        while (model.Count > capacity) model.Remove(model.Keys.First());
                    }
                    break;
            }

            Assert.Equal(capacity, storage.Capacity);
            Assert.Equal(model.Keys, Frames(storage));
            foreach (var pair in model)
            {
                Assert.True(storage.TryGet(pair.Key, out var value));
                Assert.Equal(pair.Value, value);
            }
        }
    }

    [Fact]
    public void FrameBackendsMatchOrderedModelAcrossExtremeFramesAndRepeatedResizes()
    {
        var random = new Random(104729);
        var sparse = new SparseFrameIndexedBuffer<int>(7);
        var ring = new RingFrameIndexedBuffer<int>(7);
        var backends = new IFrameIndexedBuffer<int>[] { sparse, ring };
        var model = new SortedDictionary<int, int>();
        var capacity = 7;

        for (var step = 0; step < 5000; step++)
        {
            var frame = RandomFrame(random, step);
            switch (random.Next(7))
            {
                case 0:
                case 1:
                    foreach (var backend in backends) backend.Store(frame, step);
                    model[frame] = step;
                    TrimModel(model, capacity);
                    break;
                case 2:
                    var expectedRemoval = model.Remove(frame);
                    foreach (var backend in backends)
                        Assert.Equal(expectedRemoval, backend.Remove(frame));
                    break;
                case 3:
                    foreach (var backend in backends) backend.RemoveBefore(frame);
                    foreach (var key in model.Keys.Where(key => key < frame).ToArray()) model.Remove(key);
                    break;
                case 4:
                    foreach (var backend in backends) backend.RemoveAfter(frame);
                    foreach (var key in model.Keys.Where(key => key > frame).ToArray()) model.Remove(key);
                    break;
                case 5:
                    capacity = random.Next(1, 17);
                    foreach (var backend in backends) Assert.True(backend.TrySetCapacity(capacity));
                    TrimModel(model, capacity);
                    break;
                default:
                    foreach (var backend in backends) backend.Clear();
                    model.Clear();
                    break;
            }

            var probes = new[] { int.MinValue, frame, random.Next(-100, 101), int.MaxValue };
            foreach (var backend in backends)
            {
                Assert.Equal(capacity, backend.Capacity);
                Assert.Equal(model.Keys, Frames(backend));
                foreach (var probe in probes)
                {
                    var expectedIndex = model.Keys.Count(key => key < probe);
                    Assert.Equal(expectedIndex, backend.LowerBound(probe));
                    Assert.Equal(model.ContainsKey(probe), backend.Contains(probe));
                    Assert.Equal(model.TryGetValue(probe, out var expected), backend.TryGet(probe, out var actual));
                    if (model.ContainsKey(probe)) Assert.Equal(expected, actual);
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(SequentialBackends))]
    public void SequentialBackendsEvictOldestAndResizeConsistently(
        ISequentialBuffer<int> storage)
    {
        storage.AddLast(1);
        storage.AddLast(2);
        storage.AddLast(3);
        storage.AddLast(4);

        Assert.Equal(new[] { 2, 3, 4 }, Items(storage));

        Assert.True(storage.TrySetCapacity(2));
        Assert.Equal(new[] { 3, 4 }, Items(storage));

        Assert.True(storage.TrySetCapacity(4));
        storage.AddLast(5);
        Assert.Equal(new[] { 3, 4, 5 }, Items(storage));
    }

    [Fact]
    public void RingSequentialBackendMatchesListModelAcrossWrapAndResize()
    {
        var random = new Random(2718);
        var storage = new RingSequentialBuffer<int>(4);
        var model = new List<int>();
        var capacity = 4;

        for (var step = 0; step < 500; step++)
        {
            if (random.Next(5) < 4)
            {
                storage.AddLast(step);
                model.Add(step);
            }
            else
            {
                capacity = random.Next(1, 9);
                Assert.True(storage.TrySetCapacity(capacity));
            }

            if (model.Count > capacity) model.RemoveRange(0, model.Count - capacity);
            Assert.Equal(capacity, storage.Capacity);
            Assert.Equal(model, Items(storage));
        }
    }

    [Fact]
    public void SequentialBackendsMatchAcrossRepeatedWrapResizeAndClear()
    {
        var random = new Random(130363);
        var list = new ListSequentialBuffer<int>(5);
        var ring = new RingSequentialBuffer<int>(5);
        var backends = new ISequentialBuffer<int>[] { list, ring };
        var model = new List<int>();
        var capacity = 5;

        for (var step = 0; step < 5000; step++)
        {
            switch (random.Next(6))
            {
                case 0:
                case 1:
                case 2:
                case 3:
                    foreach (var backend in backends) backend.AddLast(step);
                    model.Add(step);
                    break;
                case 4:
                    capacity = random.Next(1, 17);
                    foreach (var backend in backends) Assert.True(backend.TrySetCapacity(capacity));
                    break;
                default:
                    foreach (var backend in backends) backend.Clear();
                    model.Clear();
                    break;
            }

            if (model.Count > capacity) model.RemoveRange(0, model.Count - capacity);
            foreach (var backend in backends)
            {
                Assert.Equal(capacity, backend.Capacity);
                Assert.Equal(model, Items(backend));
            }
        }
    }

    [Fact]
    public void RingBackendsReleaseEvictedReferences()
    {
        var (sequence, sequenceReference) = CreateEvictedSequenceReference();
        var (frames, frameReference) = CreateTrimmedFrameReference();

        CollectGarbage();

        Assert.False(sequenceReference.TryGetTarget(out _));
        Assert.False(frameReference.TryGetTarget(out _));
        GC.KeepAlive(sequence);
        GC.KeepAlive(frames);
    }

    [Fact]
    public void InvalidCapacityAndIndexesDoNotMutateBuffers()
    {
        var frameBackends = new IFrameIndexedBuffer<int>[]
        {
            new SparseFrameIndexedBuffer<int>(2),
            new RingFrameIndexedBuffer<int>(2),
        };
        var sequentialBackends = new ISequentialBuffer<int>[]
        {
            new ListSequentialBuffer<int>(2),
            new RingSequentialBuffer<int>(2),
        };

        foreach (var backend in frameBackends)
        {
            backend.Store(int.MinValue, 1);
            backend.Store(int.MaxValue, 2);
            Assert.False(backend.TrySetCapacity(0));
            Assert.False(backend.TrySetCapacity(-1));
            Assert.Equal(2, backend.Capacity);
            Assert.Equal(new[] { int.MinValue, int.MaxValue }, Frames(backend));
            Assert.Throws<ArgumentOutOfRangeException>(() => backend.GetFrameAt(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => backend.GetFrameAt(backend.Count));
        }

        foreach (var backend in sequentialBackends)
        {
            backend.AddLast(1);
            backend.AddLast(2);
            Assert.False(backend.TrySetCapacity(0));
            Assert.False(backend.TrySetCapacity(-1));
            Assert.Equal(2, backend.Capacity);
            Assert.Equal(new[] { 1, 2 }, Items(backend));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = backend[-1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = backend[backend.Count]);
        }
    }

    private static int[] Frames<T>(IFrameIndexedBuffer<T> storage)
    {
        var frames = new int[storage.Count];
        for (var index = 0; index < frames.Length; index++)
            frames[index] = storage.GetFrameAt(index);
        return frames;
    }

    private static T[] Items<T>(ISequentialBuffer<T> storage)
    {
        var items = new T[storage.Count];
        for (var index = 0; index < items.Length; index++) items[index] = storage[index];
        return items;
    }

    private static int RandomFrame(Random random, int step)
    {
        return (step % 97) switch
        {
            0 => int.MinValue,
            1 => int.MaxValue,
            _ => random.Next(-100, 101),
        };
    }

    private static void TrimModel<T>(SortedDictionary<int, T> model, int capacity)
    {
        while (model.Count > capacity) model.Remove(model.Keys.First());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (RingSequentialBuffer<object> Buffer, WeakReference<object> Reference)
        CreateEvictedSequenceReference()
    {
        var buffer = new RingSequentialBuffer<object>(1);
        var value = new object();
        var reference = new WeakReference<object>(value);
        buffer.AddLast(value);
        buffer.AddLast(new object());
        return (buffer, reference);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (RingFrameIndexedBuffer<object> Buffer, WeakReference<object> Reference)
        CreateTrimmedFrameReference()
    {
        var buffer = new RingFrameIndexedBuffer<object>(2);
        var value = new object();
        var reference = new WeakReference<object>(value);
        buffer.Store(1, value);
        buffer.Store(2, new object());
        buffer.TrySetCapacity(1);
        return (buffer, reference);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CollectGarbage()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
