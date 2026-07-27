using AbilityKit.Game.Battle.Agent;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class MobaSynchronizationHealthEvaluatorTests
{
    [Fact]
    public void SustainedPressureDegradesAndSingleSpikeDoesNot()
    {
        var evaluator = new MobaSynchronizationHealthEvaluator();

        var first = evaluator.Evaluate(Pressure());
        evaluator.Evaluate(Pressure());
        var third = evaluator.Evaluate(Pressure());

        Assert.Equal(MobaSynchronizationHealthLevel.Healthy, first.Level);
        Assert.False(first.Tuning.ShouldApply);
        Assert.Equal(MobaSynchronizationHealthLevel.Degraded, third.Level);
        Assert.True(third.Tuning.ShouldApply);
        Assert.Equal(8, third.Tuning.MaxPredictionAheadFrames);
    }

    [Fact]
    public void AuthoritativeRecoveryEscalatesImmediately()
    {
        var evaluator = new MobaSynchronizationHealthEvaluator();

        var snapshot = evaluator.Evaluate(Healthy(isRecoveringState: true));

        Assert.Equal(MobaSynchronizationHealthLevel.Critical, snapshot.Level);
        Assert.True(snapshot.Tuning.ShouldApply);
        Assert.Equal(12, snapshot.Tuning.MaxPredictionAheadFrames);
    }

    [Fact]
    public void RecoveryUsesHysteresisAndResetsDefaults()
    {
        var evaluator = new MobaSynchronizationHealthEvaluator();
        evaluator.Evaluate(Pressure());
        evaluator.Evaluate(Pressure());
        evaluator.Evaluate(Pressure());

        var recovering = evaluator.Evaluate(Healthy());
        evaluator.Evaluate(Healthy());
        evaluator.Evaluate(Healthy());
        var recovered = evaluator.Evaluate(Healthy());

        Assert.Equal(MobaSynchronizationHealthLevel.Recovering, recovering.Level);
        Assert.Equal(MobaSynchronizationHealthLevel.Healthy, recovered.Level);
        Assert.True(recovered.Tuning.ShouldApply);
        Assert.True(recovered.Tuning.ResetDefaults);
    }

    [Fact]
    public void CounterResetDoesNotCreateFalseDelta()
    {
        var evaluator = new MobaSynchronizationHealthEvaluator();
        evaluator.Evaluate(Healthy(totalRollbacks: 12, totalMismatches: 7));

        var snapshot = evaluator.Evaluate(Healthy());

        Assert.Equal(0, snapshot.RollbackDelta);
        Assert.Equal(0, snapshot.MismatchDelta);
        Assert.Equal(MobaSynchronizationHealthLevel.Healthy, snapshot.Level);
    }

    private static MobaSynchronizationHealthSample Healthy(
        bool isRecoveringState = false,
        long totalRollbacks = 0,
        long totalMismatches = 0)
        => Sample(isRecoveringState, 0, 0, totalRollbacks, totalMismatches);

    private static MobaSynchronizationHealthSample Pressure()
        => Sample(false, 8, 8, 0, 0);

    private static MobaSynchronizationHealthSample Sample(
        bool isRecoveringState,
        int unacknowledgedFrames,
        int snapshotLag,
        long totalRollbacks,
        long totalMismatches)
    {
        return new MobaSynchronizationHealthSample(
            isRecoveringState,
            unacknowledgedFrames,
            snapshotLag,
            interpolationStarved: false,
            bufferedSnapshots: 2,
            playbackDelayTicks: 2,
            predictionBacklog: 0f,
            predictionWindowStalled: false,
            predictionIdealFrameStalled: false,
            replaying: false,
            totalRollbacks,
            totalRollbackRestoreFailures: 0,
            totalReplayTimeouts: 0,
            totalMismatches,
            maxPredictionAheadFrames: 6,
            minPredictionWindow: 2,
            backlogEwmaAlpha: 0.2f);
    }
}
