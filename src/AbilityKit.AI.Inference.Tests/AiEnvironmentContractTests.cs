using AbilityKit.AI.Abstractions;
using AbilityKit.Demo.Shooter.AI;
using Xunit;

namespace AbilityKit.AI.Inference.Tests;

public sealed class AiEnvironmentContractTests
{
    [Fact]
    public void Specs_WithBlankIdsAndNegativeLengths_CreateEmptyBuffers()
    {
        var observation = new AiObservationBuffer(new AiObservationSpec(" ", -1));
        var action = new AiActionBuffer(new AiActionSpec(" ", -1, -2));

        Assert.Equal("observation", observation.Spec.Id);
        Assert.Equal(0, observation.Length);
        Assert.Empty(observation.Values);
        Assert.Equal("action", action.Spec.Id);
        Assert.Empty(action.Continuous);
        Assert.Empty(action.Discrete);
    }

    [Fact]
    public void ObservationBuffer_UsesFloatStorageAndClearsItInPlace()
    {
        var observation = new AiObservationBuffer(new AiObservationSpec("flags", 2, AiSpaceValueType.Boolean));
        var values = observation.Values;
        observation[0] = 1f;
        values[1] = 1f;

        Assert.Equal(1f, values[0]);
        Assert.Equal(1f, observation[1]);
        observation.Clear();

        Assert.Same(values, observation.Values);
        Assert.Equal(new[] { 0f, 0f }, values);
        Assert.Equal(AiSpaceValueType.Boolean, observation.Spec.ValueType);
    }

    [Fact]
    public void ActionBuffer_ClearPreservesBothArrays()
    {
        var action = new AiActionBuffer(new AiActionSpec("move", 1, 1));
        var continuous = action.Continuous;
        var discrete = action.Discrete;
        continuous[0] = 0.5f;
        discrete[0] = 2;

        action.Clear();

        Assert.Same(continuous, action.Continuous);
        Assert.Same(discrete, action.Discrete);
        Assert.Equal(0f, Assert.Single(continuous));
        Assert.Equal(0, Assert.Single(discrete));
    }

    [Fact]
    public void ShooterEnvironment_ResetsAndStepsWithReusableBuffers()
    {
        IAiEnvironment environment = new ShooterAiTrainingEnvironment(new ShooterAiEnvironmentOptions(
            controlledPlayerId: 1, maxObservedEnemies: 2, maxObservedProjectiles: 2, enableEnemyWaves: false));
        var options = new AiEpisodeOptions(seed: 12, maxSteps: 1);
        var action = new AiActionBuffer(environment.ActionSpec);
        Assert.Throws<InvalidOperationException>(() => environment.Step(action));

        var initial = environment.Reset(options);
        var next = environment.Step(action);
        var restarted = environment.Reset(options);

        Assert.Equal(environment.ObservationSpec.Length, initial.Observation.Length);
        Assert.Same(initial.Observation, next.Observation);
        Assert.Same(initial.Observation, restarted.Observation);
        Assert.Equal(0, initial.StepIndex);
        Assert.False(initial.Done || initial.Truncated);
        Assert.Equal(1, next.StepIndex);
        Assert.False(next.Done);
        Assert.True(next.Truncated);
        Assert.Equal(0, restarted.StepIndex);
        Assert.False(restarted.Done || restarted.Truncated);
    }
}
