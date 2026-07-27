using AbilityKit.Game.Battle.Agent;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class MobaSynchronizationHealthEvaluatorTests
    {
        [Test]
        public void SinglePressureSpike_DoesNotDegrade()
        {
            var evaluator = new MobaSynchronizationHealthEvaluator();

            var snapshot = evaluator.Evaluate(Pressure());

            Assert.That(snapshot.Level, Is.EqualTo(MobaSynchronizationHealthLevel.Healthy));
            Assert.That(snapshot.ConsecutiveUnhealthySamples, Is.EqualTo(1));
            Assert.That(snapshot.Tuning.ShouldApply, Is.False);
        }

        [Test]
        public void SustainedPressure_DegradesAndRecommendsBoundedTuning()
        {
            var evaluator = new MobaSynchronizationHealthEvaluator();

            evaluator.Evaluate(Pressure());
            evaluator.Evaluate(Pressure());
            var snapshot = evaluator.Evaluate(Pressure());

            Assert.That(snapshot.Level, Is.EqualTo(MobaSynchronizationHealthLevel.Degraded));
            Assert.That(snapshot.Tuning.ShouldApply, Is.True);
            Assert.That(snapshot.Tuning.ResetDefaults, Is.False);
            Assert.That(snapshot.Tuning.MaxPredictionAheadFrames, Is.EqualTo(8));
            Assert.That(snapshot.Tuning.MinPredictionWindow, Is.EqualTo(3));
            Assert.That(snapshot.Tuning.BacklogEwmaAlpha, Is.EqualTo(0.25f));
        }

        [Test]
        public void AuthoritativeRecovery_EscalatesImmediately()
        {
            var evaluator = new MobaSynchronizationHealthEvaluator();

            var snapshot = evaluator.Evaluate(Healthy(isRecoveringState: true));

            Assert.That(snapshot.Level, Is.EqualTo(MobaSynchronizationHealthLevel.Critical));
            Assert.That(snapshot.Tuning.ShouldApply, Is.True);
            Assert.That(snapshot.Tuning.MaxPredictionAheadFrames, Is.EqualTo(12));
        }

        [Test]
        public void NewReplayTimeout_EscalatesAfterConsecutiveCriticalSamples()
        {
            var evaluator = new MobaSynchronizationHealthEvaluator();
            evaluator.Evaluate(Healthy(totalReplayTimeouts: 2));

            var first = evaluator.Evaluate(Healthy(totalReplayTimeouts: 3));
            var second = evaluator.Evaluate(Healthy(totalReplayTimeouts: 4));

            Assert.That(first.Level, Is.EqualTo(MobaSynchronizationHealthLevel.Healthy));
            Assert.That(first.ReplayTimeoutDelta, Is.EqualTo(1));
            Assert.That(second.ReplayTimeoutDelta, Is.EqualTo(1));
            Assert.That(second.Level, Is.EqualTo(MobaSynchronizationHealthLevel.Critical));
        }

        [Test]
        public void HealthyRecovery_UsesHysteresisThenResetsDefaults()
        {
            var evaluator = new MobaSynchronizationHealthEvaluator();
            evaluator.Evaluate(Pressure());
            evaluator.Evaluate(Pressure());
            evaluator.Evaluate(Pressure());

            var recovering = evaluator.Evaluate(Healthy());
            var second = evaluator.Evaluate(Healthy());
            var third = evaluator.Evaluate(Healthy());
            var recovered = evaluator.Evaluate(Healthy());

            Assert.That(recovering.Level, Is.EqualTo(MobaSynchronizationHealthLevel.Recovering));
            Assert.That(second.Level, Is.EqualTo(MobaSynchronizationHealthLevel.Recovering));
            Assert.That(third.Level, Is.EqualTo(MobaSynchronizationHealthLevel.Recovering));
            Assert.That(recovered.Level, Is.EqualTo(MobaSynchronizationHealthLevel.Healthy));
            Assert.That(recovered.Tuning.ShouldApply, Is.True);
            Assert.That(recovered.Tuning.ResetDefaults, Is.True);
        }

        [Test]
        public void CounterReset_DoesNotReportFalseDelta()
        {
            var evaluator = new MobaSynchronizationHealthEvaluator();
            evaluator.Evaluate(Healthy(totalRollbacks: 12, totalReconcileMismatches: 7));

            var snapshot = evaluator.Evaluate(Healthy(totalRollbacks: 0, totalReconcileMismatches: 0));

            Assert.That(snapshot.RollbackDelta, Is.Zero);
            Assert.That(snapshot.MismatchDelta, Is.Zero);
            Assert.That(snapshot.Level, Is.EqualTo(MobaSynchronizationHealthLevel.Healthy));
        }

        [Test]
        public void TuningCooldown_DoesNotRepeatRecommendationAtSameLevel()
        {
            var evaluator = new MobaSynchronizationHealthEvaluator();
            evaluator.Evaluate(Pressure());
            evaluator.Evaluate(Pressure());
            var degraded = evaluator.Evaluate(Pressure());

            var next = evaluator.Evaluate(Pressure());

            Assert.That(degraded.Tuning.ShouldApply, Is.True);
            Assert.That(next.Level, Is.EqualTo(MobaSynchronizationHealthLevel.Degraded));
            Assert.That(next.Tuning.ShouldApply, Is.False);
        }

        private static MobaSynchronizationHealthSample Healthy(
            bool isRecoveringState = false,
            long totalRollbacks = 0,
            long totalReplayTimeouts = 0,
            long totalReconcileMismatches = 0)
        {
            return new MobaSynchronizationHealthSample(
                isRecoveringState,
                unacknowledgedInputFrames: 0,
                snapshotFrameLag: 0,
                interpolationStarved: false,
                bufferedSnapshots: 3,
                playbackDelayTicks: 2,
                predictionBacklog: 0f,
                predictionWindowStalled: false,
                predictionIdealFrameStalled: false,
                replaying: false,
                totalRollbacks,
                totalRollbackRestoreFailures: 0,
                totalReplayTimeouts,
                totalReconcileMismatches,
                maxPredictionAheadFrames: 6,
                minPredictionWindow: 2,
                backlogEwmaAlpha: 0.2f);
        }

        private static MobaSynchronizationHealthSample Pressure(long totalReplayTimeouts = 0)
        {
            return new MobaSynchronizationHealthSample(
                isRecoveringState: false,
                unacknowledgedInputFrames: 8,
                snapshotFrameLag: 8,
                interpolationStarved: false,
                bufferedSnapshots: 2,
                playbackDelayTicks: 4,
                predictionBacklog: 2f,
                predictionWindowStalled: false,
                predictionIdealFrameStalled: false,
                replaying: false,
                totalRollbacks: 0,
                totalRollbackRestoreFailures: 0,
                totalReplayTimeouts,
                totalReconcileMismatches: 0,
                maxPredictionAheadFrames: 6,
                minPredictionWindow: 2,
                backlogEwmaAlpha: 0.2f);
        }
    }
}
