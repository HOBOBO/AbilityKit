using System.Collections.Concurrent;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using Xunit;

namespace AbilityKit.World.FrameSync.Tests;

public sealed class FrameCommandBufferTests
{
    [Fact]
    public async Task Concurrent_submit_read_and_trim_preserve_consistent_state()
    {
        var buffer = new FrameCommandBuffer<int, int>(32);
        var errors = new ConcurrentQueue<Exception>();

        var writers = Enumerable.Range(0, 4).Select(worker => Task.Run(() =>
        {
            try
            {
                for (var frame = 1; frame <= 1_000; frame++)
                {
                    buffer.SubmitCommand(frame, worker, frame);
                }
            }
            catch (Exception ex)
            {
                errors.Enqueue(ex);
            }
        }));

        var reader = Task.Run(() =>
        {
            try
            {
                var commands = new List<int>();
                var frames = new List<int>();
                for (var frame = 1; frame <= 1_000; frame++)
                {
                    buffer.CopyFrameCommands(frame, commands);
                    buffer.CopyRetainedFrameNumbers(frames);
                    buffer.TrimToWindow(frame);
                }
            }
            catch (Exception ex)
            {
                errors.Enqueue(ex);
            }
        });

        await Task.WhenAll(writers.Append(reader)).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Empty(errors);
        Assert.Equal(1_000, buffer.LatestFrame);
        Assert.True(buffer.OldestRetainedFrame >= 968);
    }

    [Fact]
    public void Frame_dictionary_reads_return_detached_snapshots()
    {
        var buffer = new FrameCommandBuffer<int, int>();
        buffer.SubmitCommand(10, 1, 100);

        var first = buffer.GetFrameCommandsOrEmpty(10);
        var mutable = Assert.IsType<Dictionary<int, int>>(first);
        mutable[1] = 999;
        mutable[2] = 200;

        Assert.True(buffer.TryGetCommand(10, 1, out var original));
        Assert.Equal(100, original);
        Assert.False(buffer.TryGetCommand(10, 2, out _));
    }
}

public sealed class RollbackSnapshotRingBufferTests
{
    [Fact]
    public void Store_and_get_do_not_expose_internal_snapshot_ownership()
    {
        var buffer = new RollbackSnapshotRingBuffer(1);
        var sourcePayload = new byte[] { 1, 2, 3 };
        var sourceEntries = new[] { new WorldRollbackSnapshotEntry(7, sourcePayload) };
        var source = new WorldRollbackSnapshot(1, new FrameIndex(4), sourceEntries);

        buffer.Store(source);
        sourcePayload[0] = 9;
        sourceEntries[0] = new WorldRollbackSnapshotEntry(99, Array.Empty<byte>());

        Assert.True(buffer.TryGet(new FrameIndex(4), out var first));
        Assert.Equal(7, first.Entries[0].Key);
        Assert.Equal(new byte[] { 1, 2, 3 }, first.Entries[0].Payload);

        first.Entries[0].Payload[1] = 8;
        first.Entries[0] = new WorldRollbackSnapshotEntry(100, Array.Empty<byte>());

        Assert.True(buffer.TryGet(new FrameIndex(4), out var second));
        Assert.Equal(7, second.Entries[0].Key);
        Assert.Equal(new byte[] { 1, 2, 3 }, second.Entries[0].Payload);
    }

    [Fact]
    public async Task Concurrent_store_get_and_clear_do_not_corrupt_snapshots()
    {
        var buffer = new RollbackSnapshotRingBuffer(8);
        var errors = new ConcurrentQueue<Exception>();

        var writer = Task.Run(() =>
        {
            try
            {
                for (var frame = 0; frame < 2_000; frame++)
                {
                    buffer.Store(new WorldRollbackSnapshot(
                        1,
                        new FrameIndex(frame),
                        new[] { new WorldRollbackSnapshotEntry(frame, BitConverter.GetBytes(frame)) }));
                }
            }
            catch (Exception ex)
            {
                errors.Enqueue(ex);
            }
        });

        var reader = Task.Run(() =>
        {
            try
            {
                for (var frame = 0; frame < 2_000; frame++)
                {
                    buffer.TryGet(new FrameIndex(frame), out _);
                    if ((frame & 63) == 0) buffer.Clear();
                }
            }
            catch (Exception ex)
            {
                errors.Enqueue(ex);
            }
        });

        await Task.WhenAll(writer, reader).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(errors);
    }
}

public sealed class RollbackCoordinatorTests
{
    [Fact]
    public void Missing_provider_is_rejected_before_any_import()
    {
        var provider = new TestProvider(1);
        var coordinator = CreateCoordinator(provider);
        var snapshot = Snapshot(
            new WorldRollbackSnapshotEntry(1, new byte[] { 1 }),
            new WorldRollbackSnapshotEntry(2, new byte[] { 2 }));

        var success = coordinator.TryRestore(snapshot, out var result);

        Assert.False(success);
        Assert.Equal(RollbackOperationStatus.ProviderMissing, result.Status);
        Assert.Equal(2, result.ProviderKey);
        Assert.Equal(0, provider.ImportCount);
    }

    [Fact]
    public void Provider_failure_reports_partial_restore_progress()
    {
        var first = new TestProvider(1);
        var failing = new TestProvider(2) { ImportException = new InvalidOperationException("broken") };
        var coordinator = CreateCoordinator(first, failing);
        var snapshot = Snapshot(
            new WorldRollbackSnapshotEntry(1, new byte[] { 1 }),
            new WorldRollbackSnapshotEntry(2, new byte[] { 2 }));

        var success = coordinator.TryRestore(snapshot, out var result);

        Assert.False(success);
        Assert.Equal(RollbackOperationStatus.ProviderFailed, result.Status);
        Assert.Equal(2, result.ProviderKey);
        Assert.Equal(1, result.ProviderCount);
        Assert.Contains("partially restored", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, first.ImportCount);
        Assert.Equal(1, failing.ImportCount);
    }

    [Fact]
    public void Observer_exception_does_not_change_capture_or_restore_result()
    {
        var provider = new TestProvider(1) { ExportPayload = new byte[] { 4, 5 } };
        var coordinator = CreateCoordinator(provider);
        var observed = 0;
        coordinator.OperationCompleted += _ => throw new InvalidOperationException("observer failed");
        coordinator.OperationCompleted += _ => observed++;

        Assert.True(coordinator.TryCaptureAndStore(new FrameIndex(3), out var capture));
        Assert.True(capture.IsSuccess);
        Assert.True(coordinator.TryRestore(new FrameIndex(3), out var restore));
        Assert.True(restore.IsSuccess);
        Assert.Equal(1, provider.ImportCount);
        Assert.Equal(3, observed);
    }

    private static RollbackCoordinator CreateCoordinator(params IRollbackStateProvider[] providers)
    {
        var registry = new RollbackRegistry();
        foreach (var provider in providers) registry.Register(provider);
        return new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(4));
    }

    private static WorldRollbackSnapshot Snapshot(params WorldRollbackSnapshotEntry[] entries)
    {
        return new WorldRollbackSnapshot(
            WorldRollbackSnapshotCodec.CurrentVersion,
            new FrameIndex(12),
            entries);
    }

    private sealed class TestProvider : IRollbackStateProvider
    {
        public TestProvider(int key)
        {
            Key = key;
        }

        public int Key { get; }
        public byte[] ExportPayload { get; set; } = Array.Empty<byte>();
        public Exception? ImportException { get; set; }
        public int ImportCount { get; private set; }

        public byte[] Export(FrameIndex frame)
        {
            return ExportPayload;
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            ImportCount++;
            if (ImportException != null) throw ImportException;
        }
    }
}
