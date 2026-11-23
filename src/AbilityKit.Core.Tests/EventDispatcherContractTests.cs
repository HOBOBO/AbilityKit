using AbilityKit.Core.Eventing;
using AbilityKit.Core.Pooling;
using Xunit;

namespace AbilityKit.Core.Tests;

/// <summary>
/// 验证事件分发的重入、快照、参数所有权及类型隔离契约。
/// </summary>
public sealed class EventDispatcherContractTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void OnceListenerConsumedByNestedPublishIsNotInvokedAgainByOuterSnapshot(
        bool useStringId, bool throwOnce)
    {
        const string eventName = "contract.reentrant-once";
        var dispatcher = new EventDispatcher();
        var eventId = dispatcher.GetOrRegisterEventId(eventName);
        var onceValues = new List<int>();

        void Publish(int value)
        {
            if (useStringId)
            {
                dispatcher.Publish(eventName, value, autoReleaseArgs: false);
            }
            else
            {
                dispatcher.Publish(eventId, value, autoReleaseArgs: false);
            }
        }

        dispatcher.Subscribe<int>(eventName, value =>
        {
            if (value == 0) Publish(1);
        }, priority: 10);
        dispatcher.Subscribe<int>(eventId, value =>
        {
            onceValues.Add(value);
            if (throwOnce) throw new InvalidOperationException("once callback failed");
        }, once: true);

        Publish(0);
        Publish(2);

        Assert.Equal(new[] { 1 }, onceValues);
    }

    [Fact]
    public void SubscriptionChangesAffectTheNextPublishWhileCurrentSnapshotCompletes()
    {
        var dispatcher = new EventDispatcher();
        var calls = new List<string>();
        var changed = false;
        IEventSubscription? removed = null;
        dispatcher.Subscribe<int>(1, _ =>
        {
            calls.Add("first");
            if (changed) return;
            changed = true;
            removed!.Unsubscribe();
            dispatcher.Subscribe<int>(1, _ => calls.Add("added"), priority: 5);
        }, priority: 10);
        removed = dispatcher.Subscribe<int>(1, _ => calls.Add("removed"));

        dispatcher.Publish(1, 0, autoReleaseArgs: false);
        dispatcher.Publish(1, 1, autoReleaseArgs: false);

        Assert.Equal(new[] { "first", "removed", "first", "added" }, calls);
    }

    [Fact]
    public void ClearInsideCallbackPreservesCurrentSnapshotAndReplacementChannel()
    {
        var dispatcher = new EventDispatcher();
        var calls = new List<string>();
        var first = dispatcher.Subscribe<int>(2, _ =>
        {
            calls.Add("first");
            dispatcher.Clear();
            dispatcher.Subscribe<int>(2, _ => calls.Add("replacement"));
        }, priority: 10);
        var second = dispatcher.Subscribe<int>(2, _ => calls.Add("second"));

        dispatcher.Publish(2, 0, autoReleaseArgs: false);
        first.Unsubscribe();
        second.Unsubscribe();
        dispatcher.Publish(2, 1, autoReleaseArgs: false);

        Assert.Equal(new[] { "first", "second", "replacement" }, calls);
    }

    [Fact]
    public void NullStringPublishLeavesArgumentOwnershipWithTheCaller()
    {
        var dispatcher = new EventDispatcher();
        var args = new DisposableArgs();

        dispatcher.Publish(null!, args);

        Assert.Equal(0, args.DisposeCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ArgumentReleasePolicyIsPreservedWhenCallbacksOrDisposalFail(
        bool autoRelease, bool throwOnDispose)
    {
        var dispatcher = new EventDispatcher();
        var args = new DisposableArgs(throwOnDispose);
        DisposableArgs? observed = null;
        dispatcher.Subscribe<DisposableArgs>(3,
            _ => throw new InvalidOperationException("handler failed"), priority: 10);
        dispatcher.Subscribe<DisposableArgs>(3, value => observed = value);

        dispatcher.Publish(3, args, autoReleaseArgs: autoRelease);

        Assert.Same(args, observed);
        Assert.Equal(autoRelease ? 1 : 0, args.DisposeCount);
    }

    [Fact]
    public void IdenticalEventIdsWithDifferentArgumentTypesUseSeparateChannels()
    {
        var dispatcher = new EventDispatcher();
        var calls = new List<string>();
        dispatcher.Subscribe<int>(4, value => calls.Add("int:" + value));
        dispatcher.Subscribe<string>(4, value => calls.Add("string:" + value));

        dispatcher.Publish(4, 7, autoReleaseArgs: false);
        dispatcher.Publish(4, "ready", autoReleaseArgs: false);

        Assert.Equal(new[] { "int:7", "string:ready" }, calls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void PublishWithoutListenersPreservesArgumentReleasePrecedence(bool stringId, bool autoRelease)
    {
        var dispatcher = new EventDispatcher();
        var disposable = new DisposablePoolableArgs();
        var poolable = new UntrackedPoolableArgs();

        void Publish<TArgs>(TArgs args)
        {
            if (stringId) dispatcher.Publish("contract.no-listeners", args, autoRelease);
            else dispatcher.Publish(123, args, autoRelease);
        }

        Publish(disposable);
        Publish(poolable);

        Assert.Equal(autoRelease ? 1 : 0, disposable.DisposeCount);
        Assert.Equal(0, disposable.ReleaseCount);
        Assert.Equal(autoRelease ? 1 : 0, poolable.ReleaseCount);
    }

    [Fact]
    public void DispatchersWithTheSameEventIdKeepSubscriptionsAndClearingIsolated()
    {
        var first = new EventDispatcher();
        var second = new EventDispatcher();
        var calls = new List<string>();
        first.Subscribe<int>(12, value => calls.Add($"first:{value}"));
        second.Subscribe<int>(12, value => calls.Add($"second:{value}"));

        first.Publish(12, 1, autoReleaseArgs: false);
        first.Clear();
        second.Publish(12, 2, autoReleaseArgs: false);
        first.Publish(12, 3, autoReleaseArgs: false);

        Assert.Equal(new[] { "first:1", "second:2" }, calls);
    }

    private class UntrackedPoolableArgs : IPoolable
    {
        public int ReleaseCount { get; private set; }

        public void OnPoolGet() { }

        public void OnPoolRelease()
        {
            ReleaseCount++;
            throw new InvalidOperationException("模拟未跟踪参数的归还回调失败");
        }

        public void OnPoolDestroy() { }
    }

    private sealed class DisposablePoolableArgs : UntrackedPoolableArgs, IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
            throw new InvalidOperationException("模拟可释放参数的释放回调失败");
        }
    }

    /// <summary>
    /// 记录参数释放次数，并允许模拟释放回调失败。
    /// </summary>
    private sealed class DisposableArgs : IDisposable
    {
        private readonly bool _throwOnDispose;

        public DisposableArgs(bool throwOnDispose = false)
        {
            _throwOnDispose = throwOnDispose;
        }

        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
            if (_throwOnDispose) throw new InvalidOperationException("disposal failed");
        }
    }
}
