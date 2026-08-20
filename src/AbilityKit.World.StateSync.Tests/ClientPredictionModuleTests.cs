using System.Collections.Generic;
using AbilityKit.Ability.StateSync;
using AbilityKit.Ability.StateSync.Buffer;
using AbilityKit.Ability.StateSync.Client;
using AbilityKit.Ability.StateSync.Prediction;
using AbilityKit.Core.Buffers;
using Xunit;

namespace AbilityKit.World.StateSync.Tests;

public sealed class ClientPredictionModuleTests
{
    [Fact]
    public void Initialize_UsesConfigForDefaultBufferAssembly()
    {
        var module = new ClientPredictionModule();

        module.Initialize(CreateConfig(
            localPlayerId: 7,
            inputBufferCapacity: 41,
            snapshotCapacity: 17,
            enableRollback: true));

        Assert.Equal(ClientPredictionModuleBufferFeatures.All, module.BufferOptions.Features);
        Assert.Equal(41, module.BufferOptions.InputBufferCapacity);
        Assert.Equal(17, module.BufferOptions.EntitySnapshotCapacity);
        Assert.True(module.InputBufferEnabled);
        Assert.True(module.EntitySnapshotHistoryEnabled);
        Assert.True(module.RollbackEnabled);
    }

    [Fact]
    public void DefaultAssembly_PredictsInputAndCapturesEntitySnapshot()
    {
        var module = new ClientPredictionModule();
        module.Initialize(CreateConfig());
        var entity = new TestEntity(7, isLocalPlayer: true);
        module.RegisterEntity(entity);

        module.SubmitInput(new MoveCommand(2));
        module.Tick(0);

        var state = Assert.IsType<EntityPredictionState>(module.GetEntityState(7));
        Assert.Equal(2f, state.CurrentSlots.GetFloat("position"));
        Assert.NotNull(state.GetSnapshot(0));
    }

    [Fact]
    public void DisabledBuffers_SkipInputPredictionSnapshotsAndRollback()
    {
        var module = new ClientPredictionModule(
            ClientPredictionModuleBufferOptions.Disabled,
            enableRollback: true);
        module.Initialize(CreateConfig());
        module.RegisterEntity(new TestEntity(7, isLocalPlayer: true));

        module.SubmitInput(new MoveCommand(2));
        module.Tick(0);

        var state = Assert.IsType<EntityPredictionState>(module.GetEntityState(7));
        Assert.False(module.InputBufferEnabled);
        Assert.False(module.EntitySnapshotHistoryEnabled);
        Assert.False(module.RollbackEnabled);
        Assert.False(state.SnapshotHistoryEnabled);
        Assert.False(state.RollbackEnabled);
        Assert.Equal(0f, state.CurrentSlots.GetFloat("position"));
        Assert.Null(state.GetSnapshot(0));
    }

    [Fact]
    public void EntitySnapshotHistory_CanBeEnabledWithoutInputBufferOrRollback()
    {
        var options = new ClientPredictionModuleBufferOptions(
            ClientPredictionModuleBufferFeatures.EntitySnapshotHistory,
            inputBufferCapacity: 0,
            entitySnapshotCapacity: 2);
        var module = new ClientPredictionModule(options, enableRollback: false);
        module.Initialize(CreateConfig());
        module.RegisterEntity(new TestEntity(7, isLocalPlayer: true));
        var state = Assert.IsType<EntityPredictionState>(module.GetEntityState(7));

        state.CurrentSlots.Set("position", 1f);
        state.CaptureSnapshot(1);
        state.CurrentSlots.Set("position", 2f);
        state.CaptureSnapshot(2);
        state.CurrentSlots.Set("position", 3f);
        state.CaptureSnapshot(3);

        Assert.False(module.InputBufferEnabled);
        Assert.True(module.EntitySnapshotHistoryEnabled);
        Assert.False(module.RollbackEnabled);
        Assert.Null(state.GetSnapshot(1));
        Assert.Equal(2f, state.GetSnapshot(2)!.GetFloat("position"));
        Assert.Equal(3f, state.GetSnapshot(3)!.GetFloat("position"));
    }

    [Fact]
    public void Rollback_CanBeDisabledWhileBothBuffersRemainEnabled()
    {
        var module = new ClientPredictionModule(enableRollback: false);
        module.Initialize(CreateConfig(enableRollback: true));
        var entity = new TestEntity(7, isLocalPlayer: true);
        module.RegisterEntity(entity);
        var state = Assert.IsType<EntityPredictionState>(module.GetEntityState(7));
        var rollbackEvents = 0;
        module.OnRollback += _ => rollbackEvents++;

        state.Predict(new MoveCommand(1), 1);
        state.CaptureSnapshot(1);
        state.Predict(new MoveCommand(1), 2);
        module.ApplyServerSnapshot(1, new[]
        {
            new ServerEntitySnapshot { EntityId = 7, Frame = 1, Data = new byte[] { 9 } }
        });

        Assert.Equal(ClientPredictionModuleBufferFeatures.All, module.BufferOptions.Features);
        Assert.False(module.RollbackEnabled);
        Assert.False(state.RollbackEnabled);
        Assert.Equal(0, rollbackEvents);
        Assert.Equal(9f, state.CurrentSlots.GetFloat("position"));
    }

    [Fact]
    public void BufferOptions_ForwardIdsAndCapacitiesToCustomFactories()
    {
        var inputPlayerId = -1;
        var inputCapacity = 0;
        var snapshotEntityId = -1;
        var snapshotCapacity = 0;
        var options = new ClientPredictionModuleBufferOptions(
            ClientPredictionModuleBufferFeatures.All,
            inputBufferCapacity: 11,
            entitySnapshotCapacity: 13,
            inputBufferFactory: (playerId, capacity) =>
            {
                inputPlayerId = playerId;
                inputCapacity = capacity;
                return new InputBuffer<IInputCommand>(playerId, capacity);
            },
            entitySnapshotStoreFactory: (entityId, capacity) =>
            {
                snapshotEntityId = entityId;
                snapshotCapacity = capacity;
                return new DictionarySnapshotStore(capacity);
            });
        var module = new ClientPredictionModule(options);

        module.Initialize(CreateConfig(localPlayerId: 42));
        module.RegisterEntity(new TestEntity(7, isLocalPlayer: true));

        Assert.Equal(42, inputPlayerId);
        Assert.Equal(11, inputCapacity);
        Assert.Equal(7, snapshotEntityId);
        Assert.Equal(13, snapshotCapacity);
    }

    [Fact]
    public void CapacityControls_CanResizeClientHistoriesIndependently()
    {
        var module = new ClientPredictionModule();
        module.Initialize(CreateConfig());
        module.RegisterEntity(new TestEntity(7, isLocalPlayer: true));

        var inputCapacity = Assert.IsAssignableFrom<IBufferCapacityControl>(
            module.InputBufferCapacityControl);
        var snapshotCapacity = Assert.IsAssignableFrom<IBufferCapacityControl>(
            module.GetEntitySnapshotCapacityControl(7));

        Assert.True(inputCapacity.TrySetCapacity(5));
        Assert.True(snapshotCapacity.TrySetCapacity(3));
        Assert.Equal(5, inputCapacity.Capacity);
        Assert.Equal(3, snapshotCapacity.Capacity);
    }

    [Fact]
    public void RingBackedOptions_AssembleResizableClientHistories()
    {
        var module = new ClientPredictionModule(
            ClientPredictionModuleBufferOptions.CreateRingBacked(11, 13));
        module.Initialize(CreateConfig());
        module.RegisterEntity(new TestEntity(7, isLocalPlayer: true));

        Assert.Equal(11, module.InputBufferCapacityControl!.Capacity);
        Assert.Equal(13, module.GetEntitySnapshotCapacityControl(7)!.Capacity);
        Assert.True(module.InputBufferCapacityControl.TrySetCapacity(7));
        Assert.True(module.GetEntitySnapshotCapacityControl(7)!.TrySetCapacity(9));
    }

    private static ClientPredictionConfig CreateConfig(
        int localPlayerId = 7,
        int inputBufferCapacity = 128,
        int snapshotCapacity = 30,
        bool enableRollback = true)
    {
        return new ClientPredictionConfig
        {
            LocalPlayerId = localPlayerId,
            MaxInputBufferSize = inputBufferCapacity,
            MaxPredictionFrames = snapshotCapacity,
            EnableRollback = enableRollback
        };
    }

    private sealed class MoveCommand : IInputCommand
    {
        public MoveCommand(int amount) => Amount = amount;
        public int Amount { get; }
    }

    private sealed class TestEntity : IPredictableEntity
    {
        private readonly StateSlots _slots = new StateSlots();
        private readonly IReadOnlyList<IClientPredictionHandler> _handlers;

        public TestEntity(int entityId, bool isLocalPlayer)
        {
            EntityId = entityId;
            IsLocalPlayer = isLocalPlayer;
            _slots.Set("position", 0f);
            _handlers = new IClientPredictionHandler[] { new MovementHandler() };
        }

        public int EntityId { get; }
        public bool IsLocalPlayer { get; }
        public string EntityType => "test";

        public IReadOnlyList<IClientPredictionHandler> GetPredictionHandlers() => _handlers;

        public StateSlots GetStateSlots() => _slots;

        public void RestoreFromPredictedState(StateSlots slots)
        {
            _slots.OverwriteFrom(slots);
        }

        public void ApplyServerState(ServerEntitySnapshot snapshot)
        {
            _slots.Set("position", snapshot.Data != null && snapshot.Data.Length > 0
                ? snapshot.Data[0]
                : 0f);
        }
    }

    private sealed class MovementHandler : IClientPredictionHandler
    {
        public string Name => "movement";
        public PredictionStrategy Strategy => PredictionStrategy.OptimisticWithRollback;
        public IReadOnlyList<string> RequiredSlots { get; } = new[] { "position" };

        public void PredictLocal(IInputCommand input, StateSlots slots, int frame)
        {
            slots.Set("position", slots.GetFloat("position") + ((MoveCommand)input).Amount);
        }

        public PredictionResult Validate(StateSlots predicted, ServerEntitySnapshot server)
        {
            return PredictionResult.Ok();
        }

        public void ApplyServerState(StateSlots server, StateSlots current)
        {
        }
    }
}
