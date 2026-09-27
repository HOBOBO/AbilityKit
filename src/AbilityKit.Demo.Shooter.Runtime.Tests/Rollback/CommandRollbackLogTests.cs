using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests.Rollback;

public sealed class CommandRollbackLogTests
{
    private const int TestCommandType = 101;
    private const int PayloadVersion = 1;

    [Fact]
    public void RollbackToPassesOwnedPayloadAndRunsStrictlyInReverseOrder()
    {
        var log = new CommandRollbackLog();
        var handler = new RecordingHandler(TestCommandType, PayloadVersion);
        var handlers = CreateRegistry(handler);

        log.Record(new FrameIndex(7), TestCommandType, PayloadVersion, Encode(1));
        var checkpoint = log.CreateCheckpoint();

        var secondPayload = Encode(2);
        log.Record(new FrameIndex(7), TestCommandType, PayloadVersion, secondPayload);
        log.Record(new FrameIndex(7), TestCommandType, PayloadVersion, Encode(3));
        secondPayload[0] = 99;

        var count = log.RollbackTo(in checkpoint, handlers, new FrameIndex(7));

        Assert.Equal(2, count);
        Assert.Equal(new[] { 3, 2 }, handler.RolledBackValues);
        Assert.Equal(new long[] { 2, 1 }, handler.CommandOrders);
        Assert.Equal(1, log.Count);
        Assert.Equal(1, log.NextOrder);
        Assert.Equal(1, Decode(log.GetRecord(0).Payload));
    }

    [Fact]
    public void SameFrameCheckpointSeparatesCommandsRecordedBeforeAndAfterCapture()
    {
        var log = new CommandRollbackLog();
        var handler = new RecordingHandler(TestCommandType, PayloadVersion);
        var handlers = CreateRegistry(handler);
        var frame = new FrameIndex(12);

        log.Record(frame, TestCommandType, PayloadVersion, Encode(10));
        var checkpoint = log.CreateCheckpoint();
        log.Record(frame, TestCommandType, PayloadVersion, Encode(20));

        log.RollbackTo(in checkpoint, handlers, frame);

        Assert.Equal(new[] { 20 }, handler.RolledBackValues);
        Assert.Equal(10, Decode(log.GetRecord(0).Payload));
    }

    [Fact]
    public void PrepareRollbackRejectsMissingHandlerBeforeExecutingAnyCommand()
    {
        var log = new CommandRollbackLog();
        var registered = new RecordingHandler(TestCommandType, PayloadVersion);
        var handlers = CreateRegistry(registered);
        var checkpoint = log.CreateCheckpoint();
        log.Record(new FrameIndex(1), TestCommandType, PayloadVersion, Encode(1));
        log.Record(new FrameIndex(2), 999, PayloadVersion, Encode(2));

        var error = Assert.Throws<InvalidOperationException>(() =>
            log.RollbackTo(in checkpoint, handlers, new FrameIndex(0)));

        Assert.Contains("handler not found", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(registered.RolledBackValues);
        Assert.Equal(2, log.Count);
        Assert.False(log.IsFaulted);
    }

    [Fact]
    public void PrepareRollbackRejectsUnsupportedPayloadVersionBeforeExecution()
    {
        var log = new CommandRollbackLog();
        var handler = new RecordingHandler(TestCommandType, PayloadVersion);
        var handlers = CreateRegistry(handler);
        var checkpoint = log.CreateCheckpoint();
        log.Record(new FrameIndex(1), TestCommandType, 2, Encode(1));

        Assert.Throws<InvalidOperationException>(() =>
            log.PrepareRollback(in checkpoint, handlers, new FrameIndex(0)));

        Assert.Empty(handler.RolledBackValues);
        Assert.Equal(1, log.Count);
    }

    [Fact]
    public void ClearAdvancesEpochAndInvalidatesOldCheckpoint()
    {
        var log = new CommandRollbackLog();
        var handler = new RecordingHandler(TestCommandType, PayloadVersion);
        var handlers = CreateRegistry(handler);
        var checkpoint = log.CreateCheckpoint();

        log.Clear();
        log.Record(new FrameIndex(1), TestCommandType, PayloadVersion, Encode(1));

        Assert.NotEqual(checkpoint.Epoch, log.Epoch);
        Assert.Throws<InvalidOperationException>(() =>
            log.PrepareRollback(in checkpoint, handlers, new FrameIndex(0)));
    }

    [Fact]
    public void TrimBeforeRejectsCheckpointThatCrossesDiscardedCommands()
    {
        var log = new CommandRollbackLog();
        var handler = new RecordingHandler(TestCommandType, PayloadVersion);
        var handlers = CreateRegistry(handler);
        var checkpoint = log.CreateCheckpoint();
        log.Record(new FrameIndex(1), TestCommandType, PayloadVersion, Encode(1));
        log.Record(new FrameIndex(2), TestCommandType, PayloadVersion, Encode(2));

        log.TrimBefore(new FrameIndex(2));

        Assert.Throws<InvalidOperationException>(() =>
            log.PrepareRollback(in checkpoint, handlers, new FrameIndex(0)));
        Assert.Equal(1, log.Count);
    }

    [Fact]
    public void HandlerCannotRecordAnotherCommandDuringRollback()
    {
        var log = new CommandRollbackLog();
        InvalidOperationException? reentryError = null;
        var handler = new CallbackHandler(TestCommandType, () =>
        {
            reentryError = Assert.Throws<InvalidOperationException>(() =>
                log.Record(new FrameIndex(2), TestCommandType, PayloadVersion, Encode(99)));
        });
        var handlers = CreateRegistry(handler);
        var checkpoint = log.CreateCheckpoint();
        log.Record(new FrameIndex(1), TestCommandType, PayloadVersion, Encode(1));

        log.RollbackTo(in checkpoint, handlers, new FrameIndex(0));

        Assert.NotNull(reentryError);
        Assert.Contains("while rollback", reentryError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, log.Count);
    }

    [Fact]
    public void HandlerFailureFaultsJournalUntilWorldIsRebuiltAndLogIsCleared()
    {
        var log = new CommandRollbackLog();
        var handler = new ThrowingHandler(TestCommandType);
        var handlers = CreateRegistry(handler);
        var checkpoint = log.CreateCheckpoint();
        log.Record(new FrameIndex(1), TestCommandType, PayloadVersion, Encode(1));

        Assert.Throws<InvalidOperationException>(() =>
            log.RollbackTo(in checkpoint, handlers, new FrameIndex(0)));

        Assert.True(log.IsFaulted);
        Assert.Throws<InvalidOperationException>(() => log.CreateCheckpoint());
        log.Clear();
        Assert.False(log.IsFaulted);
    }

    [Fact]
    public void CommandRollbackStateProviderRestoresUsingSerializedCheckpoint()
    {
        var log = new CommandRollbackLog();
        var handler = new RecordingHandler(TestCommandType, PayloadVersion);
        var handlers = CreateRegistry(handler);
        log.Record(new FrameIndex(1), TestCommandType, PayloadVersion, Encode(1));

        var provider = new CommandRollbackStateProvider(log, handlers);
        var registry = new RollbackRegistry();
        registry.Register(provider);
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(8));
        Assert.True(coordinator.CaptureAndStore(new FrameIndex(1)));

        log.Record(new FrameIndex(2), TestCommandType, PayloadVersion, Encode(2));

        Assert.True(coordinator.TryRestore(new FrameIndex(1), out var result));
        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { 2 }, handler.RolledBackValues);
        Assert.Equal(1, log.Count);
    }

    [Fact]
    public void StateProviderRejectsMalformedCheckpointDuringCoordinatorPreflight()
    {
        var log = new CommandRollbackLog();
        var handler = new RecordingHandler(TestCommandType, PayloadVersion);
        var handlers = CreateRegistry(handler);
        var provider = new CommandRollbackStateProvider(log, handlers);
        var registry = new RollbackRegistry();
        registry.Register(provider);
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(8));
        var snapshot = new WorldRollbackSnapshot(
            WorldRollbackSnapshotCodec.CurrentVersion,
            new FrameIndex(4),
            new[] { new WorldRollbackSnapshotEntry(provider.Key, new byte[] { 1, 2, 3 }) });

        Assert.False(coordinator.TryRestore(in snapshot, out var result));
        Assert.Equal(RollbackOperationStatus.ProviderFailed, result.Status);
        Assert.Equal(0, log.Count);
    }

    private static RollbackCommandHandlerRegistry CreateRegistry(IRollbackCommandHandler handler)
    {
        var handlers = new RollbackCommandHandlerRegistry();
        handlers.Register(handler);
        handlers.Seal();
        return handlers;
    }

    private static byte[] Encode(int value)
    {
        return BitConverter.GetBytes(value);
    }

    private static int Decode(ReadOnlyMemory<byte> payload)
    {
        return BitConverter.ToInt32(payload.ToArray(), 0);
    }

    private sealed class RecordingHandler : IRollbackCommandHandler
    {
        private readonly int _payloadVersion;

        public RecordingHandler(int commandType, int payloadVersion)
        {
            CommandType = commandType;
            _payloadVersion = payloadVersion;
        }

        public int CommandType { get; }

        public List<int> RolledBackValues { get; } = new();

        public List<long> CommandOrders { get; } = new();

        public bool CanRollback(int payloadVersion) => payloadVersion == _payloadVersion;

        public void ValidateRollback(
            in RollbackCommandContext context,
            int payloadVersion,
            ReadOnlyMemory<byte> payload)
        {
            Assert.Equal(sizeof(int), payload.Length);
        }

        public void Rollback(
            in RollbackCommandContext context,
            int payloadVersion,
            ReadOnlyMemory<byte> payload)
        {
            RolledBackValues.Add(Decode(payload));
            CommandOrders.Add(context.CommandOrder);
        }
    }

    private sealed class CallbackHandler : IRollbackCommandHandler
    {
        private readonly Action _callback;

        public CallbackHandler(int commandType, Action callback)
        {
            CommandType = commandType;
            _callback = callback;
        }

        public int CommandType { get; }

        public bool CanRollback(int payloadVersion) => payloadVersion == PayloadVersion;

        public void ValidateRollback(
            in RollbackCommandContext context,
            int payloadVersion,
            ReadOnlyMemory<byte> payload)
        {
        }

        public void Rollback(
            in RollbackCommandContext context,
            int payloadVersion,
            ReadOnlyMemory<byte> payload)
        {
            _callback();
        }
    }

    private sealed class ThrowingHandler : IRollbackCommandHandler
    {
        public ThrowingHandler(int commandType)
        {
            CommandType = commandType;
        }

        public int CommandType { get; }

        public bool CanRollback(int payloadVersion) => true;

        public void ValidateRollback(
            in RollbackCommandContext context,
            int payloadVersion,
            ReadOnlyMemory<byte> payload)
        {
        }

        public void Rollback(
            in RollbackCommandContext context,
            int payloadVersion,
            ReadOnlyMemory<byte> payload)
        {
            throw new InvalidOperationException("simulated rollback failure");
        }
    }
}
