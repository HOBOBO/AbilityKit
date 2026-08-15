using AbilityKit.Continuous;
using Xunit;

namespace AbilityKit.Continuous.Tests;

public sealed class ContinuousManagerTests
{
    [Fact]
    public void Manager_tracks_owner_and_lifecycle_until_end()
    {
        var binder = new RecordingBinder();
        var manager = new DefaultContinuousManager(lifecycleBinders: new[] { binder });
        var continuous = new TestContinuous(new TestConfig("buff", 42, true));

        Assert.True(manager.Register(continuous));
        Assert.True(manager.TryActivate(continuous));
        Assert.Single(manager.GetOwnerActiveContinuous(42));
        Assert.True(manager.TryPause(continuous));
        Assert.Empty(manager.GetOwnerActiveContinuous(42));
        Assert.True(manager.TryResume(continuous));
        Assert.True(manager.TryEnd(continuous));

        Assert.Equal(0, manager.TotalCount);
        Assert.Equal(0, manager.ActiveCount);
        Assert.Equal(new[] { "registered", "activated", "paused", "resumed", "ended", "unregistered" }, binder.Calls);
    }

    [Fact]
    public void Non_interruptible_continuous_rejects_interrupt()
    {
        var manager = new DefaultContinuousManager();
        var continuous = new TestContinuous(new TestConfig("aura", 7, false));
        Assert.True(manager.TryActivate(continuous));

        Assert.False(manager.TryInterrupt(continuous, "test"));
        Assert.True(continuous.IsActive);
    }

    private sealed record TestConfig(string Id, long OwnerId, bool CanBeInterrupted) : IContinuousConfig;

    private sealed class TestContinuous : IContinuous
    {
        public TestContinuous(IContinuousConfig config) => Config = config;

        public IContinuousConfig Config { get; }
        public ContinuousState State { get; private set; }
        public bool IsActive => State == ContinuousState.Active;
        public bool IsTerminated => State is ContinuousState.Expired or ContinuousState.Aborted;
        public bool IsPaused => State == ContinuousState.Paused;
        public float ElapsedSeconds => 0f;
        public event Action<IContinuous, ContinuousEndReason>? OnEnded;

        public void Activate() => State = ContinuousState.Active;
        public void Pause() => State = ContinuousState.Paused;
        public void Resume() => State = ContinuousState.Active;

        public void End(ContinuousEndReason reason)
        {
            State = ContinuousState.Expired;
            OnEnded?.Invoke(this, reason);
        }

        public void Abort(string reason)
        {
            State = ContinuousState.Aborted;
            OnEnded?.Invoke(this, ContinuousEndReason.Interrupted);
        }
    }

    private sealed class RecordingBinder : IContinuousLifecycleBinder
    {
        public List<string> Calls { get; } = new();
        public void OnRegistered(IContinuous continuous, IContinuousManager manager) => Calls.Add("registered");
        public void OnActivated(IContinuous continuous, IContinuousManager manager) => Calls.Add("activated");
        public void OnPaused(IContinuous continuous, IContinuousManager manager) => Calls.Add("paused");
        public void OnResumed(IContinuous continuous, IContinuousManager manager) => Calls.Add("resumed");
        public void OnEnded(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager) => Calls.Add("ended");
        public void OnUnregistered(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager) => Calls.Add("unregistered");
    }
}
