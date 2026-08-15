using AbilityKit.Core.Eventing;
using Xunit;

namespace AbilityKit.Core.Tests;

public sealed class EventDispatcherTests
{
    [Fact]
    public void String_publish_releases_disposable_args_exactly_once()
    {
        var dispatcher = new EventDispatcher();
        var args = new DisposableArgs();

        dispatcher.Publish("test.release", args);

        Assert.Equal(1, args.DisposeCount);
    }

    [Fact]
    public void Publish_uses_stable_priority_order_and_honors_once()
    {
        var dispatcher = new EventDispatcher();
        var calls = new List<string>();
        dispatcher.Subscribe<int>(1, _ => calls.Add("low"), priority: 0);
        dispatcher.SubscribeOnce<int>("ordered", _ => calls.Add("once"), priority: 10);
        dispatcher.Subscribe<int>("ordered", _ => calls.Add("first"), priority: 10);
        dispatcher.Subscribe<int>("ordered", _ => calls.Add("low"), priority: 0);

        dispatcher.Publish("ordered", 1, autoReleaseArgs: false);
        dispatcher.Publish("ordered", 2, autoReleaseArgs: false);

        Assert.Equal(new[] { "once", "first", "low", "first", "low" }, calls);
    }

    [Fact]
    public void Publish_isolates_handler_failures()
    {
        var dispatcher = new EventDispatcher();
        var called = false;
        dispatcher.Subscribe<int>(2, _ => throw new InvalidOperationException(), priority: 10);
        dispatcher.Subscribe<int>(2, _ => called = true);

        dispatcher.Publish(2, 0, autoReleaseArgs: false);

        Assert.True(called);
    }

    private sealed class DisposableArgs : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
        }
    }
}
