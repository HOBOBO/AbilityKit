using AbilityKit.Core.Eventing;
using Xunit;

namespace AbilityKit.Core.Tests;

public sealed class EventDispatcherTests
{
    [Fact]
    public void Event_key_rejects_null_identifiers_and_argument_types()
    {
        Assert.Throws<ArgumentNullException>(() => new EventKey(null!, typeof(int)));
        Assert.Throws<ArgumentNullException>(() => new EventKey("event", null!));
        Assert.Throws<ArgumentNullException>(() => new EventKey(1, null!));
        Assert.Equal(string.Empty, new EventKey(1, typeof(int)).StringId);
    }

    [Fact]
    public void Generic_event_key_rejects_null_identifier_and_keeps_integer_string_id_non_null()
    {
        Assert.Throws<ArgumentNullException>(() => new EventKey<int>(null!));
        Assert.Equal(string.Empty, new EventKey<int>(1).StringId);
    }

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

    [Fact]
    public void Once_listener_is_removed_before_reentrant_publish()
    {
        var dispatcher = new EventDispatcher();
        var calls = 0;
        dispatcher.Subscribe<int>(3, value =>
        {
            calls++;
            dispatcher.Publish(3, value + 1, autoReleaseArgs: false);
        }, once: true);

        dispatcher.Publish(3, 0, autoReleaseArgs: false);

        Assert.Equal(1, calls);
    }

    [Fact]
    public void Once_listener_can_add_a_replacement_after_reentrant_publish()
    {
        var dispatcher = new EventDispatcher();
        var calls = new List<string>();
        dispatcher.Subscribe<int>(4, _ =>
        {
            calls.Add("once");
            dispatcher.Publish(4, 1, autoReleaseArgs: false);
            dispatcher.Subscribe<int>(4, _ => calls.Add("replacement"), priority: 10);
        }, once: true);

        dispatcher.Publish(4, 0, autoReleaseArgs: false);
        dispatcher.Publish(4, 0, autoReleaseArgs: false);

        Assert.Equal(new[] { "once", "replacement" }, calls);
    }

    [Fact]
    public void Clear_removes_listeners_and_stale_subscriptions_cannot_remove_replacements()
    {
        var dispatcher = new EventDispatcher();
        var staleCalls = 0;
        var replacementCalls = 0;
        var stale = dispatcher.Subscribe<int>(5, _ => staleCalls++);

        dispatcher.Clear();
        dispatcher.Subscribe<int>(5, _ => replacementCalls++);
        stale.Unsubscribe();
        dispatcher.Publish(5, 0, autoReleaseArgs: false);

        Assert.Equal(0, staleCalls);
        Assert.Equal(1, replacementCalls);
    }

    [Fact]
    public void Global_clear_forwards_to_the_shared_dispatcher()
    {
        var calls = 0;
        GlobalEventDispatcher.Clear();
        try
        {
            GlobalEventDispatcher.Subscribe<int>("test.global-clear", _ => calls++);

            GlobalEventDispatcher.Clear();
            GlobalEventDispatcher.Publish("test.global-clear", 0, autoReleaseArgs: false);

            Assert.Equal(0, calls);
        }
        finally
        {
            GlobalEventDispatcher.Clear();
        }
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
