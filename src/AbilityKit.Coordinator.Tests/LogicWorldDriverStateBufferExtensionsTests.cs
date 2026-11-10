using System;
using System.Collections.Generic;
using Xunit;

namespace AbilityKit.Coordinator.Tests;

public sealed class LogicWorldDriverStateBufferExtensionsTests
{
    [Fact]
    public void Uses_buffer_bridge_without_calling_legacy_array_api()
    {
        var driver = new BufferDriver();
        var destination = new List<SnapshotEntityState> { new SnapshotEntityState(1, 0, Array.Empty<byte>()) };

        var count = driver.FillAllEntityStates(destination);

        Assert.Equal(1, count);
        Assert.Equal(2, destination.Count);
        Assert.Equal(42, destination[1].EntityId);
        Assert.Equal(0, driver.LegacyArrayCallCount);
    }

    [Fact]
    public void Falls_back_to_legacy_array_api_for_old_hosts()
    {
        var driver = new LegacyDriver();
        var destination = new List<SnapshotEntityState>();

        var count = driver.FillAllEntityStates(destination);

        Assert.Equal(1, count);
        Assert.Equal(42, Assert.Single(destination).EntityId);
        Assert.Equal(1, driver.LegacyArrayCallCount);
    }

    private sealed class BufferDriver : ILogicWorldDriverBridge, ILogicWorldDriverStateBufferBridge
    {
        public int LegacyArrayCallCount { get; private set; }

        public int CurrentFrame => 0;
        public double LogicTimeSeconds => 0;
        public bool IsRunning => true;
        public void Start() { }
        public void Stop() { }
        public void SubmitInputs(PlayerInput[] inputs) { }
        public void AdvanceFrame(float deltaTime) { }

        public SnapshotEntityState[] GetAllEntityStates()
        {
            LegacyArrayCallCount++;
            throw new InvalidOperationException("The legacy array path should not be used.");
        }

        public int FillAllEntityStates(IList<SnapshotEntityState> buffer)
        {
            buffer.Add(new SnapshotEntityState(42, 0, Array.Empty<byte>()));
            return 1;
        }
    }

    private sealed class LegacyDriver : ILogicWorldDriverBridge
    {
        public int LegacyArrayCallCount { get; private set; }

        public int CurrentFrame => 0;
        public double LogicTimeSeconds => 0;
        public bool IsRunning => true;
        public void Start() { }
        public void Stop() { }
        public void SubmitInputs(PlayerInput[] inputs) { }
        public void AdvanceFrame(float deltaTime) { }

        public SnapshotEntityState[] GetAllEntityStates()
        {
            LegacyArrayCallCount++;
            return new[] { new SnapshotEntityState(42, 0, Array.Empty<byte>()) };
        }
    }
}
