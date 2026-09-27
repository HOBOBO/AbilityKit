using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.Core.Tests;

public sealed class SessionTickLoopControllerTests
{
    [Fact]
    public void FixedStepBudget_LargeDeltaClampsWorkAndRetainsBacklog()
    {
        var result = FixedStepBudgetPolicy.Evaluate(
            accumulatorSeconds: 0f,
            deltaTime: 2f,
            fixedDeltaSeconds: 0.1f);

        Assert.Equal(FixedStepBudgetPolicy.MaxStepsPerUpdate, result.Steps);
        Assert.Equal(5, result.BacklogSteps);
        Assert.Equal(1f, result.DroppedSeconds, precision: 5);
        Assert.Equal(0.5f, result.AccumulatorSeconds, precision: 5);
        Assert.True(result.OverBudget);
        Assert.False(result.InvalidDelta);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(-0.1f)]
    public void FixedStepBudget_InvalidDeltaDoesNotAdvance(
        float deltaTime)
    {
        var result = FixedStepBudgetPolicy.Evaluate(
            accumulatorSeconds: 0.05f,
            deltaTime,
            fixedDeltaSeconds: 0.1f);

        Assert.Equal(0, result.Steps);
        Assert.Equal(0.05f, result.AccumulatorSeconds);
        Assert.True(result.InvalidDelta);
    }

    [Fact]
    public void FixedStepBudget_LargeFiniteValuesDoNotOverflowTelemetry()
    {
        var result = FixedStepBudgetPolicy.Evaluate(
            accumulatorSeconds: float.MaxValue,
            deltaTime: float.MaxValue,
            fixedDeltaSeconds: 1f);

        Assert.True(float.IsFinite(result.DroppedSeconds));
        Assert.Equal(float.MaxValue, result.DroppedSeconds);
        Assert.Equal(FixedStepBudgetPolicy.MaxStepsPerUpdate, result.Steps);
        Assert.Equal(5, result.BacklogSteps);
    }

    [Fact]
    public void MainTick_DrivesTransportFramesAndAuxiliaryPortsInOrder()
    {
        var state = new SessionTickLoopState();
        var port = new RecordingTickPort
        {
            FixedDeltaSeconds = 0.1f,
        };
        var clock = new ManualTickClock
        {
            NowSeconds = 10d,
        };
        var controller = new SessionTickLoopController(
            state,
            port,
            clock);

        controller.MainTick(0.25f);

        Assert.Equal(
            new[]
            {
                "transport",
                "simulation",
                "simulation",
                "remote",
                "confirmed",
                "presentation",
            },
            port.Calls);
        Assert.Equal(new[] { 0f }, port.TransportDeltas);
        Assert.Equal(new[] { 0.1f, 0.1f }, port.SimulationDeltas);
        Assert.Equal(2, state.LastFrame);
        Assert.Equal(2, state.LastUpdateSteps);
        Assert.Equal(0.05f, state.AccumulatorSeconds, precision: 5);
    }

    [Fact]
    public void MainTick_InvalidDeltaIsSanitizedForAuxiliaryPorts()
    {
        var state = new SessionTickLoopState();
        var port = new RecordingTickPort
        {
            FixedDeltaSeconds = 0.1f,
        };
        var clock = new ManualTickClock
        {
            NowSeconds = 5d,
        };
        var controller = new SessionTickLoopController(
            state,
            port,
            clock);
        controller.MainTick(0f);
        port.Clear();

        clock.NowSeconds = 5.5d;
        controller.MainTick(float.NaN);

        Assert.Equal(new[] { 0.5f }, port.TransportDeltas);
        Assert.Equal(new[] { 0f }, port.RemoteDeltas);
        Assert.Equal(new[] { 0f }, port.ConfirmedDeltas);
        Assert.Equal(new[] { 0f }, port.PresentationDeltas);
        Assert.Empty(port.SimulationDeltas);
        Assert.Equal(1, state.InvalidDeltaCount);
    }

    [Fact]
    public void MainTick_SessionGapResetsTransportElapsedTime()
    {
        var state = new SessionTickLoopState();
        var port = new RecordingTickPort
        {
            FixedDeltaSeconds = 1f,
        };
        var clock = new ManualTickClock
        {
            NowSeconds = 1d,
        };
        var controller = new SessionTickLoopController(
            state,
            port,
            clock);

        controller.MainTick(0f);
        clock.NowSeconds = 2d;
        controller.MainTick(0f);
        port.HasSession = false;
        clock.NowSeconds = 100d;
        controller.MainTick(0f);
        port.HasSession = true;
        clock.NowSeconds = 101d;
        controller.MainTick(0f);

        Assert.Equal(new[] { 0f, 1f, 0f }, port.TransportDeltas);
    }

    [Fact]
    public void MainTick_WithoutSessionDoesNotDriveAnyPort()
    {
        var state = new SessionTickLoopState();
        var port = new RecordingTickPort
        {
            HasSession = false,
        };
        var controller = new SessionTickLoopController(
            state,
            port,
            new ManualTickClock());

        controller.MainTick(1f);

        Assert.Empty(port.Calls);
        Assert.Equal(0, state.LastFrame);
    }

    private sealed class ManualTickClock : ISessionTickClock
    {
        public double NowSeconds { get; set; }
    }

    private sealed class RecordingTickPort : ISessionTickLoopPort
    {
        public bool HasSession { get; set; } = true;

        public float FixedDeltaSeconds { get; set; } = 0.1f;

        public List<string> Calls { get; } = new();

        public List<float> TransportDeltas { get; } = new();

        public List<float> SimulationDeltas { get; } = new();

        public List<float> RemoteDeltas { get; } = new();

        public List<float> ConfirmedDeltas { get; } = new();

        public List<float> PresentationDeltas { get; } = new();

        public void TickTransport(float elapsedSeconds)
        {
            Calls.Add("transport");
            TransportDeltas.Add(elapsedSeconds);
        }

        public void TickSimulationFrame(float fixedDeltaSeconds)
        {
            Calls.Add("simulation");
            SimulationDeltas.Add(fixedDeltaSeconds);
        }

        public void TickRemoteDrivenSimulation(float deltaTime)
        {
            Calls.Add("remote");
            RemoteDeltas.Add(deltaTime);
        }

        public void TickConfirmedSimulation(float deltaTime)
        {
            Calls.Add("confirmed");
            ConfirmedDeltas.Add(deltaTime);
        }

        public void TickPresentation(float deltaTime)
        {
            Calls.Add("presentation");
            PresentationDeltas.Add(deltaTime);
        }

        public void Clear()
        {
            Calls.Clear();
            TransportDeltas.Clear();
            SimulationDeltas.Clear();
            RemoteDeltas.Clear();
            ConfirmedDeltas.Clear();
            PresentationDeltas.Clear();
        }
    }
}
