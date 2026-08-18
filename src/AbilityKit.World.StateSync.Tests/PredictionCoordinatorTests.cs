using System.Collections.Generic;
using AbilityKit.Ability.StateSync;
using AbilityKit.Ability.StateSync.Prediction;
using Xunit;

namespace AbilityKit.World.StateSync.Tests;

public sealed class PredictionCoordinatorTests
{
    [Fact]
    public void ApplyServerSnapshot_RollsBackAndReplaysInputsAfterConfirmedFrame()
    {
        var coordinator = new PredictionCoordinator(7);
        var handler = new MovementHandler();
        coordinator.Register(handler);

        coordinator.ProcessInput(new MoveCommand(1));
        coordinator.ProcessInput(new MoveCommand(1));
        coordinator.ProcessInput(new MoveCommand(1));

        var server = new StateSlots();
        server.Set("position", 0f);
        coordinator.ApplyServerSnapshot(1, 7, server);

        Assert.Equal(3, coordinator.CurrentFrame.Value);
        Assert.Equal(1, coordinator.ConfirmedFrame.Value);
        Assert.Equal(2f, coordinator.GetCurrentSlots().GetFloat("position"));
        Assert.Equal(2, handler.ReplayedInputs);
    }

    [Fact]
    public void ApplyServerSnapshot_IgnoresLateConfirmation()
    {
        var coordinator = new PredictionCoordinator(7);
        var handler = new MovementHandler();
        coordinator.Register(handler);

        coordinator.ProcessInput(new MoveCommand(1));
        var server = new StateSlots();
        server.Set("position", 1f);
        coordinator.ApplyServerSnapshot(1, 7, server);
        coordinator.ProcessInput(new MoveCommand(1));

        var late = new StateSlots();
        late.Set("position", -100f);
        coordinator.ApplyServerSnapshot(1, 7, late);

        Assert.Equal(2, coordinator.CurrentFrame.Value);
        Assert.Equal(2f, coordinator.GetCurrentSlots().GetFloat("position"));
    }

    [Fact]
    public void ApplyServerSnapshot_ReplaysMultipleCommandsInTheirOriginalFrames()
    {
        var coordinator = new PredictionCoordinator(7);
        var handler = new MovementHandler();
        coordinator.Register(handler);

        coordinator.ProcessInput(new MoveCommand(1));
        coordinator.ProcessInputs(new IInputCommand[]
        {
            new MoveCommand(2),
            new MoveCommand(3)
        });
        coordinator.ProcessInput(new MoveCommand(4));

        var server = new StateSlots();
        server.Set("position", 0f);
        coordinator.ApplyServerSnapshot(1, 7, server);

        Assert.Equal(3, coordinator.CurrentFrame.Value);
        Assert.Equal(9f, coordinator.GetCurrentSlots().GetFloat("position"));
        Assert.Equal(new[] { 2, 2, 3 }, handler.ReplayedFrames);
    }

    [Fact]
    public void ProcessInputs_PublishesPredictionAppliedOnceForTheCompletedFrame()
    {
        var coordinator = new PredictionCoordinator(7);
        var handler = new MovementHandler();
        coordinator.Register(handler);
        var notifications = 0;
        var notifiedFrame = Frame.Invalid;
        coordinator.OnPredictionApplied += (frame, _) =>
        {
            notifications++;
            notifiedFrame = frame;
        };

        coordinator.ProcessInputs(new IInputCommand[]
        {
            new MoveCommand(1),
            new MoveCommand(2)
        });

        Assert.Equal(1, notifications);
        Assert.Equal(new Frame(1), notifiedFrame);
        Assert.Equal(3f, coordinator.GetCurrentSlots().GetFloat("position"));
    }

    [Fact]
    public void ApplyServerSnapshot_PreservesFramesWithoutCommandsDuringReplay()
    {
        var coordinator = new PredictionCoordinator(7);
        var handler = new MovementHandler();
        coordinator.Register(handler);

        coordinator.ProcessInput(new MoveCommand(1));
        coordinator.ProcessInputs(System.Array.Empty<IInputCommand>());
        coordinator.ProcessInput(new MoveCommand(2));

        var server = new StateSlots();
        server.Set("position", 0f);
        coordinator.ApplyServerSnapshot(1, 7, server);

        Assert.Equal(3, coordinator.CurrentFrame.Value);
        Assert.Equal(2f, coordinator.GetCurrentSlots().GetFloat("position"));
        Assert.Equal(new[] { 3 }, handler.ReplayedFrames);
    }

    [Fact]
    public void Reset_ClearsSnapshotsFromPreviousPredictionTimeline()
    {
        var coordinator = new PredictionCoordinator(7);
        var handler = new MovementHandler();
        coordinator.Register(handler);

        coordinator.ProcessInput(new MoveCommand(1));
        coordinator.Reset();
        Assert.False(coordinator.GetCurrentSlots().Has("position"));
        coordinator.GetCurrentSlots().Set("position", 5f);

        var server = new StateSlots();
        server.Set("position", 5f);
        coordinator.ApplyServerSnapshot(1, 7, server);

        Assert.Equal(0, handler.ServerStateApplications);
        Assert.Equal(5f, coordinator.GetCurrentSlots().GetFloat("position"));
    }

    [Fact]
    public void DefaultBuffers_PreserveThirtyFrameRollbackAssembly()
    {
        var coordinator = new PredictionCoordinator(7);

        Assert.Equal(PredictionCoordinatorBufferFeatures.All, coordinator.BufferOptions.Features);
        Assert.Equal(30, coordinator.BufferOptions.PredictedStateHistoryCapacity);
        Assert.Equal(30, coordinator.BufferOptions.InputHistoryCapacity);
        Assert.True(coordinator.RollbackReplayEnabled);
    }

    [Fact]
    public void DisabledBuffers_PredictAndApplyAuthoritativeCorrectionWithoutRollbackReplay()
    {
        var coordinator = new PredictionCoordinator(
            7,
            bufferOptions: PredictionCoordinatorBufferOptions.Disabled);
        var handler = new MovementHandler();
        coordinator.Register(handler);
        var rollbacks = 0;
        coordinator.OnRollbackExecuted += (_, _) => rollbacks++;

        coordinator.ProcessInput(new MoveCommand(1));
        coordinator.ProcessInput(new MoveCommand(1));

        var server = new StateSlots();
        server.Set("position", 0f);
        coordinator.ApplyServerSnapshot(1, 7, server);

        Assert.False(coordinator.RollbackReplayEnabled);
        Assert.Equal(2, coordinator.CurrentFrame.Value);
        Assert.Equal(1, coordinator.ConfirmedFrame.Value);
        Assert.Equal(0f, coordinator.GetCurrentSlots().GetFloat("position"));
        Assert.Equal(0, rollbacks);
        Assert.Equal(1, handler.ServerStateApplications);
    }

    [Fact]
    public void PredictedStateHistory_CanValidateWithoutInputHistoryOrRollbackReplay()
    {
        var options = new PredictionCoordinatorBufferOptions(
            PredictionCoordinatorBufferFeatures.PredictedStateHistory,
            predictedStateHistoryCapacity: 12,
            inputHistoryCapacity: 0);
        var coordinator = new PredictionCoordinator(7, bufferOptions: options);
        var handler = new MovementHandler();
        coordinator.Register(handler);

        coordinator.ProcessInput(new MoveCommand(1));
        coordinator.ProcessInput(new MoveCommand(1));

        var server = new StateSlots();
        server.Set("position", 1f);
        coordinator.ApplyServerSnapshot(1, 7, server);

        Assert.False(coordinator.RollbackReplayEnabled);
        Assert.Equal(2f, coordinator.GetCurrentSlots().GetFloat("position"));
        Assert.Equal(1, coordinator.ConfirmedFrame.Value);
        Assert.Equal(0, handler.ServerStateApplications);
    }

    [Fact]
    public void RollbackReplay_CanBeDisabledWithoutDisablingHistories()
    {
        var coordinator = new PredictionCoordinator(7, enableRollbackReplay: false);
        var handler = new MovementHandler();
        coordinator.Register(handler);
        var rollbacks = 0;
        coordinator.OnRollbackExecuted += (_, _) => rollbacks++;

        coordinator.ProcessInput(new MoveCommand(1));
        coordinator.ProcessInput(new MoveCommand(1));

        var server = new StateSlots();
        server.Set("position", 0f);
        coordinator.ApplyServerSnapshot(1, 7, server);

        Assert.Equal(PredictionCoordinatorBufferFeatures.All, coordinator.BufferOptions.Features);
        Assert.False(coordinator.RollbackReplayEnabled);
        Assert.Equal(2, coordinator.CurrentFrame.Value);
        Assert.Equal(0f, coordinator.GetCurrentSlots().GetFloat("position"));
        Assert.Equal(0, rollbacks);
    }

    [Fact]
    public void BufferOptions_ForwardCapacitiesToCustomStoreFactories()
    {
        var snapshotCapacity = 0;
        var inputCapacity = 0;
        var options = new PredictionCoordinatorBufferOptions(
            PredictionCoordinatorBufferFeatures.All,
            predictedStateHistoryCapacity: 11,
            inputHistoryCapacity: 13,
            snapshotStoreFactory: capacity =>
            {
                snapshotCapacity = capacity;
                return new DictionarySnapshotStore(capacity);
            },
            inputHistoryFactory: capacity =>
            {
                inputCapacity = capacity;
                return new InputHistory(capacity);
            });

        var coordinator = new PredictionCoordinator(7, bufferOptions: options);

        Assert.True(coordinator.RollbackReplayEnabled);
        Assert.Equal(11, snapshotCapacity);
        Assert.Equal(13, inputCapacity);
    }

    [Fact]
    public void InputFrameBatch_AllowsExternalHistoriesAndDefensivelyCopiesCommands()
    {
        var first = new MoveCommand(1);
        var commands = new IInputCommand[] { first };

        var batch = new InputFrameBatch(new Frame(4), commands);
        commands[0] = new MoveCommand(99);

        Assert.Equal(new Frame(4), batch.Frame);
        Assert.Same(first, batch.Inputs[0]);
    }

    [Fact]
    public void DictionarySnapshotStore_ClearRemovesAllSnapshots()
    {
        var store = new DictionarySnapshotStore(30);
        var state = new StateSlots();
        state.Set("position", 1f);
        store.Record(new Frame(1), state);
        store.Record(new Frame(2), state);

        store.Clear();

        Assert.Null(store.Get(new Frame(1)));
        Assert.Null(store.Get(new Frame(2)));
    }

    [Fact]
    public void DictionarySnapshotStore_ClonesMutableValuesOnWriteAndRead()
    {
        var store = new DictionarySnapshotStore(30);
        var mutable = new MutableState { Value = 1 };
        var state = new StateSlots(new MutableStateCloner());
        state.Set("mutable", mutable);

        store.Record(new Frame(1), state);
        mutable.Value = 2;

        var firstRead = store.Get(new Frame(1))!;
        Assert.Equal(1, firstRead.Get<MutableState>("mutable")!.Value);
        firstRead.Get<MutableState>("mutable")!.Value = 3;

        var secondRead = store.Get(new Frame(1))!;
        Assert.Equal(1, secondRead.Get<MutableState>("mutable")!.Value);
    }

    [Fact]
    public void StateSlots_CloneRejectsMutableReferenceWithoutCloneStrategy()
    {
        var state = new StateSlots();
        state.Set("mutable", new MutableState { Value = 1 });

        var error = Assert.Throws<System.InvalidOperationException>(() => state.Clone());

        Assert.Contains(nameof(IStateSlotValueCloner), error.Message);
        Assert.Contains("mutable", error.Message);
    }

    [Fact]
    public void StateSlots_OverwriteFromDoesNotPartiallyCommitWhenCloneFails()
    {
        var target = new StateSlots();
        target.Set("position", 5f);
        var source = new StateSlots();
        source.Set("copied", 1f);
        source.Set("mutable", new MutableState { Value = 2 });

        Assert.Throws<System.InvalidOperationException>(() => target.OverwriteFrom(source));

        Assert.Equal(5f, target.GetFloat("position"));
        Assert.False(target.Has("copied"));
        Assert.False(target.Has("mutable"));
    }

    private sealed class MoveCommand : IInputCommand
    {
        public MoveCommand(int direction) => Direction = direction;
        public int Direction { get; }
    }

    private sealed class MovementHandler : IPredictionHandler
    {
        public string Name => "movement";
        public PredictionStrategy Strategy => PredictionStrategy.OptimisticWithRollback;
        public IReadOnlyList<string> RequiredSlots { get; } = new[] { "position" };
        public int ReplayedInputs { get; private set; }
        public int ServerStateApplications { get; private set; }
        public List<int> ReplayedFrames { get; } = new();
        private bool _replaying;

        public void Predict(IInputCommand input, StateSlots slots, Frame frame)
        {
            var command = (MoveCommand)input;
            slots.Set("position", slots.GetFloat("position") + command.Direction);
            if (_replaying)
            {
                ReplayedInputs++;
                ReplayedFrames.Add(frame.Value);
            }
        }

        public PredictionResult Validate(StateSlots predicted, StateSlots server)
        {
            return predicted.GetFloat("position") == server.GetFloat("position")
                ? PredictionResult.Ok()
                : PredictionResult.Major("position mismatch");
        }

        public void ApplyServerState(StateSlots server, StateSlots current)
        {
            ServerStateApplications++;
            _replaying = true;
        }
    }

    private sealed class MutableState
    {
        public int Value { get; set; }
    }

    private sealed class MutableStateCloner : IStateSlotValueCloner
    {
        public object Clone(string slotName, object value)
        {
            var mutable = (MutableState)value;
            return new MutableState { Value = mutable.Value };
        }
    }
}
