using System.Collections.Generic;
using AbilityKit.Game.View.Flow;
using Xunit;

namespace AbilityKit.Game.View.Runtime.Tests;

public sealed class PhaseFeatureHostTests
{
    [Fact]
    public void AttachTickGuiAndDetach_DispatchInExpectedOrder()
    {
        var events = new List<string>();
        var first = new TestFeature("first", events);
        var gui = new GuiFeature("gui", events);
        var host = new PhaseFeatureHost<TestContext, IPhaseFeature<TestContext>>();
        var ctx = new TestContext(3);

        host.Add(first, in ctx);
        host.Add(gui, in ctx);
        host.AttachAll(in ctx);
        host.Tick(in ctx, 0.5f);
        host.OnGUI(in ctx);
        host.DetachAll(in ctx);

        Assert.Equal(
            new[]
            {
                "attach:first:3",
                "attach:gui:3",
                "tick:first:0.50",
                "tick:gui:0.50",
                "gui:gui:3",
                "detach:gui:3",
                "detach:first:3"
            },
            events);
    }

    [Fact]
    public void Add_WhenAlreadyAttached_AttachesNewFeatureImmediately()
    {
        var events = new List<string>();
        var host = new PhaseFeatureHost<TestContext, IPhaseFeature<TestContext>>();
        var ctx = new TestContext(9);

        host.AttachAll(in ctx);
        host.Add(new TestFeature("late", events), in ctx);

        Assert.Equal(new[] { "attach:late:9" }, events);
    }

    [Fact]
    public void LifecycleHooks_CanWrapFeatureDispatch()
    {
        var events = new List<string>();
        var first = new TestFeature("first", events);
        var second = new TestFeature("second", events);
        var host = new PhaseFeatureHost<TestContext, IPhaseFeature<TestContext>>(
            attachFeature: (IPhaseFeature<TestContext> feature, in TestContext ctx) =>
            {
                events.Add($"before-attach:{feature.GetType().Name}:{ctx.Value}");
                feature.OnAttach(in ctx);
            },
            detachFeature: (IPhaseFeature<TestContext> feature, in TestContext ctx) =>
            {
                events.Add($"before-detach:{feature.GetType().Name}:{ctx.Value}");
                feature.OnDetach(in ctx);
            },
            tickFeature: (IPhaseFeature<TestContext> feature, in TestContext ctx, float deltaTime) =>
            {
                events.Add($"before-tick:{feature.GetType().Name}:{deltaTime:0.00}");
                feature.Tick(in ctx, deltaTime);
            });
        var ctx = new TestContext(11);

        host.Add(first, in ctx);
        host.Add(second, in ctx);
        host.AttachAll(in ctx);
        host.Tick(in ctx, 0.25f);
        host.Clear(in ctx);
        host.AttachAll(in ctx);
        host.Add(new TestFeature("late", events), in ctx);

        Assert.Equal(
            new[]
            {
                "before-attach:TestFeature:11",
                "attach:first:11",
                "before-attach:TestFeature:11",
                "attach:second:11",
                "before-tick:TestFeature:0.25",
                "tick:first:0.25",
                "before-tick:TestFeature:0.25",
                "tick:second:0.25",
                "before-detach:TestFeature:11",
                "detach:second:11",
                "before-detach:TestFeature:11",
                "detach:first:11",
                "before-attach:TestFeature:11",
                "attach:late:11"
            },
            events);
    }

    [Fact]
    public void AttachAll_WhenFeatureFails_RollsBackAttachedFeaturesAndCanRetry()
    {
        var events = new List<string>();
        var first = new FaultFeature("first", events);
        var second = new FaultFeature("second", events) { ThrowOnAttach = true };
        var host = new PhaseFeatureHost<TestContext, IPhaseFeature<TestContext>>();
        var ctx = new TestContext(5);
        host.Add(first, in ctx);
        host.Add(second, in ctx);

        Assert.Throws<InvalidOperationException>(() => host.AttachAll(in ctx));

        Assert.False(host.IsAttached);
        Assert.Equal(new[] { "attach:first", "attach:second", "detach:first" }, events);

        second.ThrowOnAttach = false;
        host.AttachAll(in ctx);

        Assert.True(host.IsAttached);
        Assert.Equal(
            new[] { "attach:first", "attach:second", "detach:first", "attach:first", "attach:second" },
            events);
    }

    [Fact]
    public void AttachAll_WhenRollbackFails_AggregatesAttachAndRollbackErrors()
    {
        var events = new List<string>();
        var first = new FaultFeature("first", events) { ThrowOnDetach = true };
        var second = new FaultFeature("second", events) { ThrowOnAttach = true };
        var host = new PhaseFeatureHost<TestContext, IPhaseFeature<TestContext>>();
        var ctx = new TestContext(5);
        host.Add(first, in ctx);
        host.Add(second, in ctx);

        var exception = Assert.Throws<AggregateException>(() => host.AttachAll(in ctx));

        Assert.False(host.IsAttached);
        Assert.Equal(2, exception.InnerExceptions.Count);
        Assert.Equal(new[] { "attach:first", "attach:second", "detach:first" }, events);
    }

    [Fact]
    public void DetachAll_WhenFeaturesFail_ContinuesCleanupAndResetsState()
    {
        var events = new List<string>();
        var first = new FaultFeature("first", events) { ThrowOnDetach = true };
        var second = new FaultFeature("second", events) { ThrowOnDetach = true };
        var third = new FaultFeature("third", events);
        var host = new PhaseFeatureHost<TestContext, IPhaseFeature<TestContext>>();
        var ctx = new TestContext(5);
        host.Add(first, in ctx);
        host.Add(second, in ctx);
        host.Add(third, in ctx);
        host.AttachAll(in ctx);
        events.Clear();

        var exception = Assert.Throws<AggregateException>(() => host.DetachAll(in ctx));

        Assert.False(host.IsAttached);
        Assert.Equal(2, exception.InnerExceptions.Count);
        Assert.Equal(new[] { "detach:third", "detach:second", "detach:first" }, events);
    }

    [Fact]
    public void Add_WhenAttachedAndAttachFails_DoesNotPublishFeature()
    {
        var events = new List<string>();
        var host = new PhaseFeatureHost<TestContext, IPhaseFeature<TestContext>>();
        var ctx = new TestContext(5);
        host.AttachAll(in ctx);
        var failing = new FaultFeature("failing", events) { ThrowOnAttach = true };

        Assert.Throws<InvalidOperationException>(() => host.Add(failing, in ctx));

        Assert.Empty(host.Features);
        Assert.True(host.IsAttached);
    }

    [Fact]
    public async Task RemoveAsync_WaitsForAsyncFeatureBeforeRemovingIt()
    {
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var feature = new AsyncFeature(release.Task);
        var host = new PhaseFeatureHost<TestContext, IPhaseFeature<TestContext>>();
        var ctx = new TestContext(5);
        host.Add(feature, in ctx);
        host.AttachAll(in ctx);

        var remove = host.RemoveAsync(feature, ctx);

        Assert.False(remove.IsCompleted);
        Assert.Single(host.Features);
        Assert.Equal(1, feature.AsyncDetachCount);
        Assert.Equal(0, feature.SyncDetachCount);

        release.SetResult(true);
        Assert.True(await remove);

        Assert.Empty(host.Features);
    }

    [Fact]
    public async Task DetachAllAsync_ContinuesAfterFailureAndResetsState()
    {
        var events = new List<string>();
        var host = new PhaseFeatureHost<TestContext, IPhaseFeature<TestContext>>();
        var ctx = new TestContext(5);
        host.Add(new AsyncFeature(Task.CompletedTask, "first", events, throwOnDetach: true), in ctx);
        host.Add(new AsyncFeature(Task.CompletedTask, "second", events), in ctx);
        host.AttachAll(in ctx);

        var exception = await Assert.ThrowsAsync<AggregateException>(
            () => host.DetachAllAsync(ctx));

        Assert.Equal(new[] { "second", "first" }, events);
        Assert.Single(exception.InnerExceptions);
        Assert.False(host.IsAttached);
    }

    private readonly record struct TestContext(int Value);

    private class TestFeature : IPhaseFeature<TestContext>
    {
        private readonly string _id;
        private readonly List<string> _events;

        public TestFeature(string id, List<string> events)
        {
            _id = id;
            _events = events;
        }

        public void OnAttach(in TestContext ctx)
        {
            _events.Add($"attach:{_id}:{ctx.Value}");
        }

        public void OnDetach(in TestContext ctx)
        {
            _events.Add($"detach:{_id}:{ctx.Value}");
        }

        public void Tick(in TestContext ctx, float deltaTime)
        {
            _events.Add($"tick:{_id}:{deltaTime:0.00}");
        }
    }

    private sealed class FaultFeature : IPhaseFeature<TestContext>
    {
        private readonly string _id;
        private readonly List<string> _events;

        public FaultFeature(string id, List<string> events)
        {
            _id = id;
            _events = events;
        }

        public bool ThrowOnAttach { get; set; }
        public bool ThrowOnDetach { get; set; }

        public void OnAttach(in TestContext ctx)
        {
            _events.Add($"attach:{_id}");
            if (ThrowOnAttach) throw new InvalidOperationException($"attach:{_id}");
        }

        public void OnDetach(in TestContext ctx)
        {
            _events.Add($"detach:{_id}");
            if (ThrowOnDetach) throw new InvalidOperationException($"detach:{_id}");
        }

        public void Tick(in TestContext ctx, float deltaTime)
        {
        }
    }

    private sealed class AsyncFeature : IAsyncPhaseFeature<TestContext>
    {
        private readonly Task _release;
        private readonly string? _id;
        private readonly List<string>? _events;
        private readonly bool _throwOnDetach;

        public AsyncFeature(
            Task release,
            string? id = null,
            List<string>? events = null,
            bool throwOnDetach = false)
        {
            _release = release;
            _id = id;
            _events = events;
            _throwOnDetach = throwOnDetach;
        }

        public int AsyncDetachCount { get; private set; }
        public int SyncDetachCount { get; private set; }

        public void OnAttach(in TestContext ctx)
        {
        }

        public void OnDetach(in TestContext ctx)
        {
            SyncDetachCount++;
        }

        public async Task DetachAsync(TestContext ctx)
        {
            AsyncDetachCount++;
            await _release;
            if (_id != null) _events?.Add(_id);
            if (_throwOnDetach) throw new InvalidOperationException(_id);
        }

        public void Tick(in TestContext ctx, float deltaTime)
        {
        }
    }

    private sealed class GuiFeature : TestFeature, IPhaseGuiFeature<TestContext>
    {
        private readonly string _id;
        private readonly List<string> _events;

        public GuiFeature(string id, List<string> events) : base(id, events)
        {
            _id = id;
            _events = events;
        }

        public void OnGUI(in TestContext ctx)
        {
            _events.Add($"gui:{_id}:{ctx.Value}");
        }
    }
}
