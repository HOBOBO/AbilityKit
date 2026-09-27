using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.Core.Tests;

public sealed class SessionSnapshotRoutingControllerTests
{
    [Fact]
    public void BuildAndFeed_UseDeclaredPortOrder()
    {
        var port = new RecordingPort();
        var controller =
            new SessionSnapshotRoutingController<string, int>(port);

        controller.Build("first", frame => port.Calls.Add($"receive:{frame}"));
        var fed = controller.TryFeed(7);

        Assert.True(fed);
        Assert.Equal(SessionSnapshotRoutingState.Built, controller.State);
        Assert.Equal(1, controller.Generation);
        Assert.Equal(
            new[]
            {
                "create:first",
                "publish",
                "bind",
                "subscribe",
                "feed:7",
            },
            port.Calls);
    }

    [Fact]
    public void Rebuild_DisposesPreviousRouteInReverseOrder()
    {
        var port = new RecordingPort();
        var controller =
            new SessionSnapshotRoutingController<string, int>(port);
        controller.Build("first", _ => { });
        port.Calls.Clear();

        controller.Build("second", _ => { });

        Assert.Equal(
            new[]
            {
                "unsubscribe",
                "unbind",
                "unpublish",
                "release",
                "create:second",
                "publish",
                "bind",
                "subscribe",
            },
            port.Calls);
        Assert.Equal(2, controller.Generation);
        Assert.Equal(SessionSnapshotRoutingState.Built, controller.State);
    }

    [Fact]
    public void BuildFailure_RollsBackCompletedAndAttemptedPhases()
    {
        var port = new RecordingPort
        {
            FailOnceAt = "bind",
        };
        var controller =
            new SessionSnapshotRoutingController<string, int>(port);

        var exception = Assert.Throws<InvalidOperationException>(
            () => controller.Build("first", _ => { }));

        Assert.Contains("bind", exception.Message);
        Assert.Equal(
            new[]
            {
                "create:first",
                "publish",
                "bind",
                "unbind",
                "unpublish",
                "release",
            },
            port.Calls);
        Assert.Equal(SessionSnapshotRoutingState.Idle, controller.State);
        Assert.Equal(0, controller.Generation);
        Assert.False(controller.TryFeed(1));
    }

    [Fact]
    public void DisposeFailure_RetryContinuesAtFailedPhase()
    {
        var port = new RecordingPort();
        var controller =
            new SessionSnapshotRoutingController<string, int>(port);
        controller.Build("first", _ => { });
        port.Calls.Clear();
        port.FailOnceAt = "unbind";

        var exception =
            Assert.Throws<SessionSnapshotRoutingCleanupException>(
                controller.Dispose);

        Assert.Equal("context binding", exception.Step);
        Assert.Equal(
            SessionSnapshotRoutingState.DisposeFailed,
            controller.State);
        Assert.Equal(new[] { "unsubscribe", "unbind" }, port.Calls);

        controller.Dispose();

        Assert.Equal(
            new[]
            {
                "unsubscribe",
                "unbind",
                "unbind",
                "unpublish",
                "release",
            },
            port.Calls);
        Assert.Equal(SessionSnapshotRoutingState.Idle, controller.State);
    }

    [Fact]
    public void Feed_WhenNotBuilt_DoesNotTouchPort()
    {
        var port = new RecordingPort();
        var controller =
            new SessionSnapshotRoutingController<string, int>(port);

        Assert.False(controller.TryFeed(3));
        Assert.Empty(port.Calls);
    }

    private sealed class RecordingPort :
        ISessionSnapshotRoutingPort<string, int>
    {
        public List<string> Calls { get; } = new();

        public string? FailOnceAt { get; set; }

        public void Create(string context)
        {
            Invoke($"create:{context}", "create");
        }

        public void Publish()
        {
            Invoke("publish");
        }

        public void Bind()
        {
            Invoke("bind");
        }

        public void Subscribe(Action<int> frameReceivedHandler)
        {
            Invoke("subscribe");
        }

        public void Unsubscribe(Action<int> frameReceivedHandler)
        {
            Invoke("unsubscribe");
        }

        public void Unbind()
        {
            Invoke("unbind");
        }

        public void Unpublish()
        {
            Invoke("unpublish");
        }

        public void Release()
        {
            Invoke("release");
        }

        public void Feed(int frame)
        {
            Invoke($"feed:{frame}", "feed");
        }

        private void Invoke(string call, string? failureKey = null)
        {
            Calls.Add(call);
            var key = failureKey ?? call;
            if (!string.Equals(FailOnceAt, key, StringComparison.Ordinal))
            {
                return;
            }

            FailOnceAt = null;
            throw new InvalidOperationException($"{key} failed");
        }
    }
}
