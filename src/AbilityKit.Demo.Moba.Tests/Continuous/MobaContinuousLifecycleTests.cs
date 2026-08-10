using System;
using System.Collections.Generic;
using AbilityKit.Core.Continuous;
using AbilityKit.Demo.Moba.Services;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Continuous;

public sealed class MobaContinuousLifecycleTests
{
    [Fact]
    public void Runtime_base_transitions_active_pause_resume_and_end()
    {
        var runtime = new TestContinuous(new TestContinuousConfig(ownerId: 101));
        var ended = new List<ContinuousEndReason>();
        runtime.OnEnded += (_, reason) => ended.Add(reason);

        Assert.Equal(ContinuousState.Inactive, runtime.State);
        Assert.Equal(0f, runtime.ElapsedSeconds);

        runtime.Activate();
        runtime.Advance(1.25f);
        runtime.Advance(0f);
        runtime.Pause();
        runtime.Resume();
        runtime.End(ContinuousEndReason.Completed);

        Assert.Equal(ContinuousState.Expired, runtime.State);
        Assert.True(runtime.IsTerminated);
        Assert.Equal(1.25f, runtime.ElapsedSeconds);
        Assert.Equal(new[] { ContinuousEndReason.Completed }, ended);
    }

    [Fact]
    public void Runtime_base_interrupts_when_activation_is_rejected()
    {
        var runtime = new TestContinuous(new TestContinuousConfig(ownerId: 102), allowActivate: false);
        var ended = new List<ContinuousEndReason>();
        runtime.OnEnded += (_, reason) => ended.Add(reason);

        runtime.Activate();

        Assert.Equal(ContinuousState.Aborted, runtime.State);
        Assert.True(runtime.IsTerminated);
        Assert.Single(ended, ContinuousEndReason.Interrupted);
    }

    [Fact]
    public void Manager_registers_activates_pauses_resumes_and_ends_continuous_runtime()
    {
        var manager = new DefaultContinuousManager();
        var binder = new RecordingBinder();
        manager.AddLifecycleBinder(binder);

        var runtime = new TestContinuous(new TestContinuousConfig(ownerId: 200));

        Assert.True(manager.Register(runtime));
        Assert.Equal(1, manager.TotalCount);
        Assert.Equal(0, manager.ActiveCount);

        Assert.True(manager.TryActivate(runtime));
        Assert.Equal(1, manager.ActiveCount);
        Assert.Equal(ContinuousState.Active, runtime.State);

        Assert.True(manager.TryPause(runtime));
        Assert.Equal(0, manager.ActiveCount);
        Assert.Equal(ContinuousState.Paused, runtime.State);

        Assert.True(manager.TryResume(runtime));
        Assert.Equal(1, manager.ActiveCount);
        Assert.Equal(ContinuousState.Active, runtime.State);

        Assert.True(manager.TryEnd(runtime, ContinuousEndReason.Completed));
        Assert.Equal(0, manager.ActiveCount);
        Assert.Equal(0, manager.TotalCount);
        Assert.Equal(ContinuousState.Expired, runtime.State);
        Assert.Equal("registered,activated,paused,resumed,ended:Completed,unregistered:Completed", string.Join(",", binder.Events));
    }

    [Fact]
    public void Manager_blocks_activation_when_owner_active_tags_conflict()
    {
        var manager = new DefaultContinuousManager();
        var blockingTags = new TestTagContainer("stun");
        manager.AddAdmissionPolicy(new BlockByOwnerActiveTagsPolicy(blockingTags));

        var first = new TestContinuous(new TestTaggedContinuousConfig(ownerId: 300, tags: new TestTagContainer("stun")));
        var second = new TestContinuous(new TestTaggedContinuousConfig(ownerId: 300, tags: new TestTagContainer("stun")));

        Assert.True(manager.Register(first));
        Assert.True(manager.TryActivate(first));
        Assert.True(manager.Register(second));

        Assert.False(manager.TryActivate(second));
        Assert.Equal("Blocked by active continuous tags", manager.LastRejectReason);
        Assert.Equal(1, manager.ActiveCount);
        Assert.Equal(2, manager.TotalCount);
    }

    [Fact]
    public void Manager_enumerates_active_continuous_in_registration_order()
    {
        var manager = new DefaultContinuousManager();
        var first = new TestContinuous(new TestContinuousConfig(ownerId: 401));
        var second = new TestContinuous(new TestContinuousConfig(ownerId: 402));
        var third = new TestContinuous(new TestContinuousConfig(ownerId: 403));

        Assert.True(manager.Register(first));
        Assert.True(manager.Register(second));
        Assert.True(manager.Register(third));
        Assert.True(manager.TryActivate(third));
        Assert.True(manager.TryActivate(first));
        Assert.True(manager.TryActivate(second));

        Assert.Equal(new IContinuous[] { first, second, third }, manager.GetAllActiveContinuous());

        Assert.True(manager.TryPause(first));
        Assert.True(manager.TryResume(first));
        Assert.Equal(new IContinuous[] { first, second, third }, manager.GetAllActiveContinuous());
    }

    [Fact]
    public void Manager_rolls_back_registration_when_lifecycle_binder_throws()
    {
        var manager = new DefaultContinuousManager();
        var recordingBinder = new RecordingBinder();
        var throwingBinder = new ThrowingBinder(throwOnRegistered: true);
        manager.AddLifecycleBinder(recordingBinder);
        manager.AddLifecycleBinder(throwingBinder);
        var runtime = new TestContinuous(new TestContinuousConfig(ownerId: 500));

        Assert.Throws<InvalidOperationException>(() => manager.Register(runtime));

        Assert.Equal(0, manager.TotalCount);
        Assert.Equal(0, manager.ActiveCount);
        Assert.Empty(manager.GetAllContinuous());
        Assert.Empty(manager.GetOwnerContinuous(500));
        Assert.Equal("registered,unregistered:CleanedUp", string.Join(",", recordingBinder.Events));

        Assert.True(manager.RemoveLifecycleBinder(throwingBinder));
        Assert.True(manager.Register(runtime));
        Assert.Equal(1, manager.TotalCount);
    }

    [Fact]
    public void Manager_unregisters_ended_continuous_when_ended_binder_throws()
    {
        var manager = new DefaultContinuousManager(lifecycleBinders: new[] { new ThrowingBinder(throwOnEnded: true) });
        var runtime = new TestContinuous(new TestContinuousConfig(ownerId: 501));
        Assert.True(manager.TryActivate(runtime));

        Assert.Throws<InvalidOperationException>(() => runtime.End(ContinuousEndReason.Completed));

        Assert.True(runtime.IsTerminated);
        Assert.Equal(0, manager.TotalCount);
        Assert.Equal(0, manager.ActiveCount);
        Assert.Empty(manager.GetOwnerContinuous(501));
    }

    [Fact]
    public void Tick_processor_catches_up_and_preserves_interval_remainder()
    {
        var runtime = new TestPeriodicContinuous(intervalSeconds: 0.25f, initialRemainingSeconds: 0.25f);
        var handler = new CountingIntervalHandler();
        var processor = new MobaContinuousTickProcessor(new[] { handler });
        runtime.Activate();

        processor.Tick(runtime, 1.1f);

        Assert.Equal(4, handler.Count);
        Assert.Equal(0.15f, runtime.IntervalRemainingSeconds, precision: 5);

        processor.Tick(runtime, 0.1f);
        Assert.Equal(4, handler.Count);
        Assert.Equal(0.05f, runtime.IntervalRemainingSeconds, precision: 5);
    }

    [Fact]
    public void Tick_processor_caps_each_call_and_preserves_backlog()
    {
        var runtime = new TestPeriodicContinuous(intervalSeconds: 0.1f, initialRemainingSeconds: 0.1f);
        var handler = new CountingIntervalHandler();
        var processor = new MobaContinuousTickProcessor(new[] { handler });
        runtime.Activate();

        processor.Tick(runtime, 10f);

        Assert.Equal(MobaContinuousTickProcessor.MaxIntervalExecutionsPerTick, handler.Count);
        Assert.True(runtime.IntervalRemainingSeconds < 0f);

        processor.Tick(runtime, 0.01f);
        Assert.Equal(MobaContinuousTickProcessor.MaxIntervalExecutionsPerTick * 2, handler.Count);
        Assert.True(runtime.IntervalRemainingSeconds < 0f);
    }

    [Fact]
    public void Tick_processor_disables_invalid_intervals()
    {
        var handler = new CountingIntervalHandler();
        var processor = new MobaContinuousTickProcessor(new[] { handler });
        var invalidIntervals = new[] { 0f, -1f, float.NaN, float.PositiveInfinity };

        foreach (var interval in invalidIntervals)
        {
            var runtime = new TestPeriodicContinuous(interval, initialRemainingSeconds: 5f);
            runtime.Activate();

            processor.Tick(runtime, 1f);

            Assert.Equal(0f, runtime.IntervalRemainingSeconds);
        }

        Assert.Equal(0, handler.Count);
    }

    private sealed class TestContinuous : MobaContinuousRuntimeBase
    {
        private readonly TestContinuousConfig _config;
        private readonly bool _allowActivate;

        public TestContinuous(TestContinuousConfig config, bool allowActivate = true)
        {
            _config = config;
            _allowActivate = allowActivate;
        }

        public override IContinuousConfig Config => _config;

        protected override bool OnActivating() => _allowActivate;

        public void Advance(float deltaTimeSeconds)
        {
            AdvanceElapsed(deltaTimeSeconds);
        }
    }

    private class TestContinuousConfig : IContinuousConfig
    {
        public TestContinuousConfig(long ownerId)
        {
            OwnerId = ownerId;
        }

        public string Id => $"continuous.{OwnerId}";
        public long OwnerId { get; }
        public bool CanBeInterrupted => true;
    }

    private sealed class TestTaggedContinuousConfig : TestContinuousConfig, ITagConfig
    {
        public TestTaggedContinuousConfig(long ownerId, ITagContainer tags)
            : base(ownerId)
        {
            Tags = tags;
        }

        public ITagContainer Tags { get; }
        public ITagContainer PauseByTags => TestTagContainer.Empty;
        public ITagContainer BlockByTags => TestTagContainer.Empty;
    }

    private sealed class TestTagContainer : ITagContainer
    {
        public static readonly TestTagContainer Empty = new TestTagContainer();
        private readonly HashSet<string> _tags = new(StringComparer.Ordinal);

        public TestTagContainer(params string[] tags)
        {
            if (tags != null)
            {
                for (var i = 0; i < tags.Length; i++)
                {
                    var tag = tags[i];
                    if (!string.IsNullOrWhiteSpace(tag))
                    {
                        _tags.Add(tag);
                    }
                }
            }
        }

        public bool HasTag(string tag) => _tags.Contains(tag);
        public bool HasAny(ITagContainer other)
        {
            if (other == null || other.Count == 0)
            {
                return false;
            }

            if (other is TestTagContainer testOther)
            {
                foreach (var tag in testOther._tags)
                {
                    if (_tags.Contains(tag))
                    {
                        return true;
                    }
                }

                return false;
            }

            return other.Count > 0 && _tags.Count > 0;
        }
        public int Count => _tags.Count;
    }

    private sealed class RecordingBinder : IContinuousLifecycleBinder
    {
        public readonly List<string> Events = new();

        public void OnRegistered(IContinuous continuous, IContinuousManager manager) => Events.Add("registered");
        public void OnActivated(IContinuous continuous, IContinuousManager manager) => Events.Add("activated");
        public void OnPaused(IContinuous continuous, IContinuousManager manager) => Events.Add("paused");
        public void OnResumed(IContinuous continuous, IContinuousManager manager) => Events.Add("resumed");
        public void OnEnded(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager) => Events.Add($"ended:{reason}");
        public void OnUnregistered(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager) => Events.Add($"unregistered:{reason}");
    }

    private sealed class ThrowingBinder : IContinuousLifecycleBinder
    {
        private readonly bool _throwOnRegistered;
        private readonly bool _throwOnEnded;

        public ThrowingBinder(bool throwOnRegistered = false, bool throwOnEnded = false)
        {
            _throwOnRegistered = throwOnRegistered;
            _throwOnEnded = throwOnEnded;
        }

        public void OnRegistered(IContinuous continuous, IContinuousManager manager)
        {
            if (_throwOnRegistered) throw new InvalidOperationException("registration binder failed");
        }

        public void OnActivated(IContinuous continuous, IContinuousManager manager) { }
        public void OnPaused(IContinuous continuous, IContinuousManager manager) { }
        public void OnResumed(IContinuous continuous, IContinuousManager manager) { }

        public void OnEnded(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager)
        {
            if (_throwOnEnded) throw new InvalidOperationException("ended binder failed");
        }

        public void OnUnregistered(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager) { }
    }

    private sealed class TestPeriodicContinuous : MobaContinuousRuntimeBase, IMobaContinuousIntervalState, IMobaContinuousExecutionContextProvider
    {
        private readonly TestPeriodicConfig _config;

        public TestPeriodicContinuous(float intervalSeconds, float initialRemainingSeconds)
        {
            _config = new TestPeriodicConfig(intervalSeconds);
            IntervalRemainingSeconds = initialRemainingSeconds;
        }

        public override IContinuousConfig Config => _config;
        public float IntervalRemainingSeconds { get; set; }

        public bool TryGetCombatExecutionContext(out MobaCombatExecutionContext context)
        {
            context = default;
            return true;
        }

        public bool TryGetContextSource(out MobaContextSourceView source)
        {
            source = default;
            return false;
        }
    }

    private sealed class TestPeriodicConfig : TestContinuousConfig, IMobaContinuousPeriodicConfig
    {
        public TestPeriodicConfig(float intervalSeconds)
            : base(ownerId: 600)
        {
            IntervalSeconds = intervalSeconds;
        }

        public float IntervalSeconds { get; }
        public IReadOnlyList<int> IntervalEffectIds { get; } = new[] { 1 };
    }

    private sealed class CountingIntervalHandler : IMobaContinuousIntervalHandler
    {
        public int Count { get; private set; }

        public bool CanHandle(IContinuous continuous) => continuous is TestPeriodicContinuous;

        public void OnInterval(IContinuous continuous, IMobaContinuousPeriodicConfig periodicConfig, in MobaCombatExecutionContext executionContext)
        {
            Count++;
        }
    }
}
