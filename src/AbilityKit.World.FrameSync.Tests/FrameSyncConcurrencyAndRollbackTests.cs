using System.Collections.Concurrent;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
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

    [Fact]
    public void Retained_frame_numbers_are_unique_sorted_and_trim_before_is_exclusive()
    {
        var buffer = new FrameCommandBuffer<int, int>();
        buffer.SubmitCommand(30, 1, 30);
        buffer.SubmitCommand(10, 1, 10);
        buffer.SubmitCommand(20, 1, 20);
        buffer.SubmitCommand(20, 2, 200);
        var frames = new List<int>();

        Assert.Equal(3, buffer.CopyRetainedFrameNumbers(frames));
        Assert.Equal(new[] { 10, 20, 30 }, frames);

        buffer.TrimBefore(20);

        Assert.Equal(2, buffer.CopyRetainedFrameNumbers(frames));
        Assert.Equal(new[] { 20, 30 }, frames);
        Assert.False(buffer.TryGetCommand(10, 1, out _));
        Assert.True(buffer.TryGetCommand(20, 2, out var replacement));
        Assert.Equal(200, replacement);
    }

    [Fact]
    public void Submit_and_trim_reuse_frame_index_and_command_storage_without_allocation()
    {
        var buffer = new FrameCommandBuffer<int, int>(8);
        for (var frame = 0; frame < 16; frame++)
        {
            buffer.SubmitCommand(frame, 1, frame);
            buffer.TrimBefore(frame);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 16; frame < 128; frame++)
        {
            buffer.SubmitCommand(frame, 1, frame);
            buffer.TrimBefore(frame);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
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

public sealed class ClientPredictionReplayBoundaryTests
{
    [Fact]
    public void Rollback_replay_uses_the_explicit_replay_input_lifecycle()
    {
        var world = new TestWorld();
        var sink = new ReplayAwareInputSink();
        var registry = new RollbackRegistry();
        registry.Register(new TestWorldStateProvider(world));
        var rollback = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(8));
        var hashes = new WorldStateHashRingBuffer(8);
        var reconciler = new ClientPredictionReconciler(hashes);
        var runner = new ClientPredictionRunner(
            world,
            sink,
            rollback,
            new InputHistoryRingBuffer(8),
            reconciler);
        rollback.CaptureAndStore(new FrameIndex(0));

        runner.TickPredicted(
            new FrameIndex(1),
            1f / 30f,
            new[] { Command(1) },
            _ => new WorldStateHash((uint)world.State));
        runner.TickPredicted(
            new FrameIndex(2),
            1f / 30f,
            new[] { Command(2) },
            _ => new WorldStateHash((uint)world.State));

        Assert.True(runner.OnAuthoritativeStateHash(new FrameIndex(1), new WorldStateHash(999)));

        Assert.Equal(new[] { 1, 2 }, sink.LiveFrames);
        Assert.Equal(new[] { 2 }, sink.ReplayFrames);
        Assert.Equal(new FrameIndex(1).Value, sink.RestoredFrame.Value);
        Assert.Equal(new FrameIndex(2).Value, sink.ReplayToFrame.Value);
        Assert.Equal(1, sink.BeginCount);
        Assert.Equal(1, sink.EndCount);
    }

    private static PlayerInputCommand Command(int frame) => new PlayerInputCommand(
        new FrameIndex(frame),
        new PlayerId("player"),
        1000 + frame,
        new byte[] { (byte)frame });

    private sealed class TestWorld : IWorld
    {
        public WorldId Id { get; } = new WorldId("prediction-test");
        public string WorldType => "test";
        public IWorldResolver Services => null!;
        public int State { get; set; }

        public void Initialize() { }
        public void Tick(float deltaTime) => State++;
        public void Dispose() { }
    }

    private sealed class TestWorldStateProvider : IRollbackStateProvider
    {
        private readonly TestWorld _world;

        public TestWorldStateProvider(TestWorld world) => _world = world;

        public int Key => 1;
        public byte[] Export(FrameIndex frame) => BitConverter.GetBytes(_world.State);
        public void Import(FrameIndex frame, byte[] payload) => _world.State = BitConverter.ToInt32(payload, 0);
    }

    private sealed class ReplayAwareInputSink : IWorldInputReplaySink
    {
        public List<int> LiveFrames { get; } = new List<int>();
        public List<int> ReplayFrames { get; } = new List<int>();
        public int BeginCount { get; private set; }
        public int EndCount { get; private set; }
        public FrameIndex RestoredFrame { get; private set; }
        public FrameIndex ReplayToFrame { get; private set; }

        public void Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs) =>
            LiveFrames.Add(frame.Value);

        public void BeginReplay(FrameIndex restoredFrame, FrameIndex replayToFrame)
        {
            BeginCount++;
            RestoredFrame = restoredFrame;
            ReplayToFrame = replayToFrame;
        }

        public void Replay(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs) =>
            ReplayFrames.Add(frame.Value);

        public void EndReplay() => EndCount++;
        public void Dispose() { }
    }
}

public sealed class RollbackCoordinatorTests
{
    [Fact]
    public void Structural_providers_import_before_field_state_regardless_of_key_order()
    {
        var importOrder = new List<int>();
        var state = new OrderedProvider(1, importOrder);
        var structure = new OrderedStructureProvider(9, importOrder);
        var coordinator = CreateCoordinator(state, structure);
        var snapshot = Snapshot(
            new WorldRollbackSnapshotEntry(1, Array.Empty<byte>()),
            new WorldRollbackSnapshotEntry(9, Array.Empty<byte>()));

        Assert.True(coordinator.TryRestore(snapshot, out var result));
        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { 9, 1 }, importOrder);
    }

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

    private class OrderedProvider : IRollbackStateProvider
    {
        private readonly List<int> _importOrder;

        public OrderedProvider(int key, List<int> importOrder)
        {
            Key = key;
            _importOrder = importOrder;
        }

        public int Key { get; }

        public byte[] Export(FrameIndex frame) => Array.Empty<byte>();

        public void Import(FrameIndex frame, byte[] payload)
        {
            _importOrder.Add(Key);
        }
    }

    private sealed class OrderedStructureProvider : OrderedProvider, IRollbackStructureRestoreProvider
    {
        public OrderedStructureProvider(int key, List<int> importOrder)
            : base(key, importOrder)
        {
        }
    }
}
