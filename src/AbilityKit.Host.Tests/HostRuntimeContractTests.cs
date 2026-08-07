using System.Collections.Concurrent;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Builder.Components;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;
using Xunit;

namespace AbilityKit.Host.Tests;

public sealed class HostRuntimeContractTests
{
    [Fact]
    public void Broadcast_uses_stable_connection_snapshot_when_send_disconnects()
    {
        var runtime = new HostRuntime(new TestWorldManager());
        var first = new TestConnection("first");
        var second = new TestConnection("second");
        first.OnSend = _ => runtime.Disconnect(first.ClientId);
        runtime.Connect(first);
        runtime.Connect(second);

        runtime.Broadcast(new TestMessage());
        runtime.Broadcast(new TestMessage());

        Assert.Equal(1, first.SendCount);
        Assert.Equal(2, second.SendCount);
    }

    [Fact]
    public void SendTo_does_not_publish_after_send_when_transport_fails()
    {
        var options = new HostRuntimeOptions();
        var afterSendCount = 0;
        options.AfterSendMessage.Add((_, _) => afterSendCount++);
        options.OnAfterSendMessage = (_, _) => afterSendCount++;
        var runtime = new HostRuntime(new TestWorldManager(), options);
        var connection = new TestConnection("failing") { SendException = new InvalidOperationException("send failed") };

        runtime.SendTo(connection, new TestMessage());

        Assert.Equal(0, afterSendCount);
    }

    [Fact]
    public async Task Fixed_step_driver_does_not_overlap_ticks()
    {
        var manager = new BlockingWorldManager();
        var runtime = new HostRuntime(manager);
        var driver = new FixedStepTimeDriver { FrameRate = 1000 };
        driver.Attach(runtime, new HostRuntimeOptions());

        driver.Start();
        await manager.FirstTickStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);
        manager.ReleaseTick.Set();
        await Task.Delay(20);
        driver.Stop();

        Assert.Equal(1, manager.MaximumConcurrentTicks);
    }

    private sealed class TestMessage : ServerMessage;

    private sealed class TestConnection : IServerConnection
    {
        private int _sendCount;

        public TestConnection(string id)
        {
            ClientId = new ServerClientId(id);
        }

        public ServerClientId ClientId { get; }
        public Action<ServerMessage>? OnSend { get; set; }
        public Exception? SendException { get; set; }
        public int SendCount => Volatile.Read(ref _sendCount);

        public void Send(ServerMessage message)
        {
            Interlocked.Increment(ref _sendCount);
            OnSend?.Invoke(message);
            if (SendException != null)
                throw SendException;
        }
    }

    private class TestWorldManager : IWorldManager
    {
        private readonly ConcurrentDictionary<WorldId, IWorld> _worlds = new();

        public IReadOnlyDictionary<WorldId, IWorld> Worlds => _worlds;

        public virtual IWorld Create(WorldCreateOptions options) => throw new NotSupportedException();
        public bool TryGet(WorldId id, out IWorld world) => _worlds.TryGetValue(id, out world!);
        public virtual bool Destroy(WorldId id) => _worlds.TryRemove(id, out _);
        public virtual void Tick(float deltaTime) { }
        public void DisposeAll() => _worlds.Clear();
    }

    private sealed class BlockingWorldManager : TestWorldManager
    {
        private int _concurrentTicks;
        private int _maximumConcurrentTicks;

        public TaskCompletionSource FirstTickStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim ReleaseTick { get; } = new(false);
        public int MaximumConcurrentTicks => Volatile.Read(ref _maximumConcurrentTicks);

        public override void Tick(float deltaTime)
        {
            var concurrent = Interlocked.Increment(ref _concurrentTicks);
            UpdateMaximum(concurrent);
            FirstTickStarted.TrySetResult();
            ReleaseTick.Wait(TimeSpan.FromSeconds(5));
            Interlocked.Decrement(ref _concurrentTicks);
        }

        private void UpdateMaximum(int value)
        {
            var current = Volatile.Read(ref _maximumConcurrentTicks);
            while (value > current)
            {
                var observed = Interlocked.CompareExchange(ref _maximumConcurrentTicks, value, current);
                if (observed == current)
                    return;
                current = observed;
            }
        }
    }
}
