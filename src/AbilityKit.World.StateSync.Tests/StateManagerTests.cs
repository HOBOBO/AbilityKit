using AbilityKit.Ability.StateSync;
using AbilityKit.Ability.StateSync.Buffer;
using Xunit;

namespace AbilityKit.World.StateSync.Tests;

public sealed class StateManagerTests
{
    [Fact]
    public void CaptureAndRestoreRoundTripsEntityState()
    {
        var manager = new StateManager(new SnapshotBuffer(4));
        var entity = new TestRollbackable(1) { Value = 10 };
        manager.RegisterRollbackable(entity);

        manager.CaptureState(5);
        entity.Value = 99;

        Assert.True(manager.TryRestore(5));
        Assert.Equal(10, entity.Value);
    }

    [Fact]
    public void CaptureTrimsRollbackDataWithSnapshotRetention()
    {
        var manager = new StateManager(new SnapshotBuffer(2));
        var entity = new TestRollbackable(1);
        manager.RegisterRollbackable(entity);

        for (var frame = 1; frame <= 5; frame++)
        {
            entity.Value = frame;
            manager.CaptureState(frame);
        }

        Assert.Equal(new[] { 4, 5 }, manager.GetCapturedFrames());
        Assert.Equal(2, manager.RetainedRollbackFrameCount);
        Assert.False(manager.TryRestore(1));
        Assert.True(manager.TryRestore(4));
        Assert.Equal(4, entity.Value);
    }

    [Fact]
    public void ClearHistoryMakesRestoreFailAndReleasesRollbackData()
    {
        var manager = new StateManager(new SnapshotBuffer(2));
        manager.RegisterRollbackable(new TestRollbackable(1));
        manager.CaptureState(1);

        manager.ClearHistory();

        Assert.False(manager.TryRestore(1));
        Assert.Equal(0, manager.RetainedRollbackFrameCount);
        Assert.Empty(manager.GetCapturedFrames());
    }

    [Fact]
    public async Task EntityCaptureCanReenterRegistrationWithoutDeadlock()
    {
        var manager = new StateManager(new SnapshotBuffer(2));
        var additional = new TestRollbackable(2) { Value = 20 };
        var entity = new TestRollbackable(1)
        {
            Value = 10,
            OnCreateState = () => manager.RegisterRollbackable(additional),
        };
        manager.RegisterRollbackable(entity);

        var capture = Task.Run(() => manager.CaptureState(1));

        await capture.WaitAsync(TimeSpan.FromSeconds(2));
        manager.CaptureState(2);
        additional.Value = 99;
        Assert.True(manager.TryRestore(2));
        Assert.Equal(20, additional.Value);
    }

    [Fact]
    public async Task LogCallbackCanReenterManagerWithoutDeadlock()
    {
        var manager = new StateManager(new SnapshotBuffer(2));
        manager.Log = _ => manager.GetCapturedFrames();

        var register = Task.Run(() => manager.RegisterRollbackable(new TestRollbackable(1)));

        await register.WaitAsync(TimeSpan.FromSeconds(2));
    }

    private sealed class TestRollbackable : IRollbackable
    {
        public TestRollbackable(long entityId)
        {
            EntityId = entityId;
        }

        public long EntityId { get; }
        public int SnapshotKey => checked((int)EntityId);
        public int Value { get; set; }
        public Action? OnCreateState { get; init; }

        public IRollbackState CreateRollbackState()
        {
            OnCreateState?.Invoke();
            return new TestRollbackState(SnapshotKey, Value);
        }

        public void RestoreFromRollbackState(IRollbackState state)
        {
            Value = ((TestRollbackState)state).Value;
        }
    }

    private sealed class TestRollbackState : IRollbackState
    {
        public TestRollbackState(int snapshotKey, int value)
        {
            SnapshotKey = snapshotKey;
            Value = value;
        }

        public int SnapshotKey { get; }
        public int Value { get; private set; }

        public byte[] Serialize()
        {
            return BitConverter.GetBytes(Value);
        }

        public void Deserialize(byte[] data)
        {
            Value = BitConverter.ToInt32(data, 0);
        }
    }
}
