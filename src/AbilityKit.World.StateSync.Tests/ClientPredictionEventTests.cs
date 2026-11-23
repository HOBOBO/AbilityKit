using AbilityKit.Ability.StateSync;
using AbilityKit.Ability.StateSync.Client;
using AbilityKit.Ability.StateSync.Prediction;
using Xunit;

namespace AbilityKit.World.StateSync.Tests;

public sealed class ClientPredictionEventTests
{
    [Fact]
    public void CorrectionNotifiesRollbackThenSnapshotAndDefersSlotChangesToTick()
    {
        using var module = CreateModule();
        var entity = new TypedEntity(7) { AuthoritativePosition = 9f };
        module.RegisterEntity(entity);
        var state = module.GetEntityState(7);
        state.CurrentSlots.Set("position", 1f);
        state.CaptureSnapshot(0);
        state.AdvanceFrame();
        module.Tick(5);

        var order = new List<string>();
        var changes = new List<StateChangeEvent>();
        state.OnRollback += (fromFrame, toFrame) =>
        {
            order.Add("entity rollback");
            Assert.Equal((1, 0), (fromFrame, toFrame));
            Assert.Equal(1f, state.CurrentSlots.GetFloat("position"));
        };
        module.OnRollback += notice =>
        {
            order.Add("module rollback");
            Assert.Equal((7, 5, 0), (notice.EntityId, notice.FromFrame, notice.ToFrame));
            Assert.Equal(RollbackReason.ServerCorrection, notice.Reason);
            Assert.Equal(ConflictLevel.None, notice.Level);
            Assert.Equal(9f, state.CurrentSlots.GetFloat("position"));
        };
        module.OnSnapshotApplied += (entityId, frame) =>
        {
            order.Add("snapshot");
            Assert.Equal((7, 0), (entityId, frame));
            Assert.Equal(5, module.CurrentFrame);
            Assert.Equal(0, state.CurrentFrame);
        };
        module.OnStateChanged += change =>
        {
            changes.Add(change);
            Assert.Equal(5, module.ConfirmedFrame);
        };

        module.ApplyServerSnapshot(0, new[] { Snapshot(7) });

        Assert.Equal(new[] { "entity rollback", "module rollback", "snapshot" }, order);
        Assert.Empty(changes);
        Assert.Equal((0, 5), (module.CurrentFrame, module.ConfirmedFrame));

        module.Tick(1);

        Assert.Collection(changes,
            change => AssertSlotChange(change, 0f, 1f),
            change => AssertSlotChange(change, 1f, 9f));
        Assert.Equal(1, module.ConfirmedFrame);
    }

    [Fact]
    public void MissingHistoryStillNotifiesSnapshotButNotRollback()
    {
        using var module = CreateModule();
        module.RegisterEntity(new TypedEntity(7) { AuthoritativePosition = 4f });
        module.GetEntityState(7).AdvanceFrame();
        var snapshots = new List<(int EntityId, int Frame)>();
        var rollbacks = new List<RollbackEvent>();
        module.OnSnapshotApplied += (entityId, frame) => snapshots.Add((entityId, frame));
        module.OnRollback += rollbacks.Add;

        module.ApplyServerSnapshot(0, new[] { Snapshot(99), Snapshot(7) });

        Assert.Empty(rollbacks);
        Assert.Equal((7, 0), Assert.Single(snapshots));
        Assert.Equal(4f, module.GetPredictedSlots(7).GetFloat("position"));
    }

    [Fact]
    public void SubscriberFailureKeepsAppliedEntityAndStopsRemainingBatch()
    {
        using var module = CreateModule();
        module.RegisterEntity(new TypedEntity(7) { AuthoritativePosition = 4f });
        module.RegisterEntity(new TypedEntity(8) { AuthoritativePosition = 8f });
        module.Tick(3);
        var error = new InvalidOperationException("subscriber error");
        module.OnSnapshotApplied += (_, _) => throw error;

        Assert.Same(error, Assert.Throws<InvalidOperationException>(() =>
            module.ApplyServerSnapshot(4, new[] { Snapshot(7), Snapshot(8) })));

        Assert.Equal(4f, module.GetPredictedSlots(7).GetFloat("position"));
        Assert.Equal(0f, module.GetPredictedSlots(8).GetFloat("position"));
        Assert.Equal((3, 3), (module.CurrentFrame, module.ConfirmedFrame));
    }

    private static void AssertSlotChange(StateChangeEvent change, float oldValue, float newValue)
    {
        Assert.Equal((7, 0, "position"), (change.EntityId, change.Frame, change.SlotName));
        Assert.Equal(oldValue, Assert.IsType<float>(change.OldValue));
        Assert.Equal(newValue, Assert.IsType<float>(change.NewValue));
        Assert.True(change.HasChanged);
        Assert.False(change.IsPredicted);
    }

    private static ClientPredictionModule CreateModule()
    {
        var module = new ClientPredictionModule();
        module.Initialize(ClientPredictionConfig.Default);
        return module;
    }

    private static ServerEntitySnapshot Snapshot(int entityId) => new() { EntityId = entityId, Frame = 99 };

    private sealed class TypedEntity : IPredictableEntity
    {
        private readonly StateSlots _slots = new();

        public TypedEntity(int entityId)
        {
            EntityId = entityId;
            _slots.Set("position", 0f);
        }

        public int EntityId { get; }
        public bool IsLocalPlayer => true;
        public string EntityType => "typed-test-entity";
        public float AuthoritativePosition { get; init; }

        public IReadOnlyList<IClientPredictionHandler> GetPredictionHandlers() => Array.Empty<IClientPredictionHandler>();
        public StateSlots GetStateSlots() => _slots;
        public void RestoreFromPredictedState(StateSlots slots) => throw new InvalidOperationException("unexpected entity restore");
        public void ApplyServerState(ServerEntitySnapshot snapshot) => _slots.Set("position", AuthoritativePosition);
    }
}
