using System.Collections.Generic;
using AbilityKit.Ability.StateSync;
using AbilityKit.Ability.StateSync.Prediction;
using Xunit;

namespace AbilityKit.World.StateSync.Tests;

public sealed class PredictionCoordinatorTests
{
    [Fact]
    public void ApplyServerSnapshot_RollsBackAndReplaysInputsAfterConfirmedFrame()
    {
        var coordinator = new PredictionCoordinator(7);
        var handler = new MovementHandler();
        coordinator.Register(handler);

        coordinator.ProcessInput(new MoveCommand(1));
        coordinator.ProcessInput(new MoveCommand(1));
        coordinator.ProcessInput(new MoveCommand(1));

        var server = new StateSlots();
        server.Set("position", 0f);
        coordinator.ApplyServerSnapshot(1, 7, server);

        Assert.Equal(3, coordinator.CurrentFrame.Value);
        Assert.Equal(1, coordinator.ConfirmedFrame.Value);
        Assert.Equal(2f, coordinator.GetCurrentSlots().GetFloat("position"));
        Assert.Equal(2, handler.ReplayedInputs);
    }

    [Fact]
    public void ApplyServerSnapshot_IgnoresLateConfirmation()
    {
        var coordinator = new PredictionCoordinator(7);
        var handler = new MovementHandler();
        coordinator.Register(handler);

        coordinator.ProcessInput(new MoveCommand(1));
        var server = new StateSlots();
        server.Set("position", 1f);
        coordinator.ApplyServerSnapshot(1, 7, server);
        coordinator.ProcessInput(new MoveCommand(1));

        var late = new StateSlots();
        late.Set("position", -100f);
        coordinator.ApplyServerSnapshot(1, 7, late);

        Assert.Equal(2, coordinator.CurrentFrame.Value);
        Assert.Equal(2f, coordinator.GetCurrentSlots().GetFloat("position"));
    }

    private sealed class MoveCommand : IInputCommand
    {
        public MoveCommand(int direction) => Direction = direction;
        public int Direction { get; }
    }

    private sealed class MovementHandler : IPredictionHandler
    {
        public string Name => "movement";
        public PredictionStrategy Strategy => PredictionStrategy.OptimisticWithRollback;
        public IReadOnlyList<string> RequiredSlots { get; } = new[] { "position" };
        public int ReplayedInputs { get; private set; }
        private bool _replaying;

        public void Predict(IInputCommand input, StateSlots slots, Frame frame)
        {
            var command = (MoveCommand)input;
            slots.Set("position", slots.GetFloat("position") + command.Direction);
            if (_replaying) ReplayedInputs++;
        }

        public PredictionResult Validate(StateSlots predicted, StateSlots server)
        {
            return predicted.GetFloat("position") == server.GetFloat("position")
                ? PredictionResult.Ok()
                : PredictionResult.Major("position mismatch");
        }

        public void ApplyServerState(StateSlots server, StateSlots current)
        {
            _replaying = true;
        }
    }
}
