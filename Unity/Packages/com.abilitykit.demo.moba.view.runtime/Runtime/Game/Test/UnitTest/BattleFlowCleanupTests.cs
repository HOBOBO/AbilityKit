using System;
using System.Threading.Tasks;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Flow;
using AbilityKit.Game.View.Flow;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattleFlowCleanupTests
    {
        [Test]
        public void ClearFeaturesAsync_ContinuesAfterFailureAndCanRetry()
        {
            var scheduler = CreateScheduler();
            var trailing = new DetachFeature();
            var failing = new DetachFeature { FailOnce = true };
            var leading = new DetachFeature();
            scheduler.AttachFeature(trailing);
            scheduler.AttachFeature(failing);
            scheduler.AttachFeature(leading);

            var error = Assert.Throws<AggregateException>(() =>
                scheduler.ClearFeaturesAsync().GetAwaiter().GetResult());
            Assert.AreEqual(1, error.InnerExceptions.Count);
            Assert.AreEqual(1, trailing.DetachCount);
            Assert.AreEqual(1, leading.DetachCount);
            Assert.AreEqual(1, failing.DetachCount);
            Assert.AreEqual(1, scheduler.Features.Features.Count);

            scheduler.ClearFeaturesAsync().GetAwaiter().GetResult();
            Assert.AreEqual(2, failing.DetachCount);
            Assert.AreEqual(0, scheduler.Features.Features.Count);
        }

        [Test]
        public void ExitCoordinator_ReportsTerminalFailureAndKeepsTaskForShutdown()
        {
            var failure = new InvalidOperationException("detach failed");
            var reported = 0;
            var coordinator = new BattleFlowExitCoordinator(
                () => Task.FromException(failure),
                () => Assert.Fail("End must not run after cleanup failure."),
                () => Assert.Fail("Lobby must not run after cleanup failure."),
                _ => reported++);

            coordinator.RequestEnd();
            Assert.IsTrue(coordinator.Poll());
            Assert.IsTrue(coordinator.HasFailed);
            Assert.IsFalse(coordinator.IsPending);
            Assert.AreEqual(1, reported);
            Assert.Throws<InvalidOperationException>(() =>
                coordinator.PendingTask.GetAwaiter().GetResult());
            coordinator.RequestEnd();
            Assert.AreEqual(1, reported);
        }

        private static FeatureScheduler CreateScheduler() => new FeatureScheduler(
            Log.Sink,
            new FeatureScheduler.Callbacks
            {
                FeatureBinderAttach = _ => { },
                FeatureBinderDetach = _ => { },
                ClearBattleSessionEvents = () => { },
                ExecuteFlowAction = (_, __) => { },
                ExecuteSwitchFlow = (_, __) => { }
            },
            default,
            MobaFlowConfiguration.CreateDefault(),
            new PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature>(),
            new PhaseFeaturePlan<GamePhaseContext, IGamePhaseFeature>());

        private sealed class DetachFeature : IGamePhaseFeature
        {
            public bool FailOnce { get; set; }
            public int DetachCount { get; private set; }
            public void OnAttach(in GamePhaseContext ctx) { }
            public void OnDetach(in GamePhaseContext ctx)
            {
                DetachCount++;
                if (!FailOnce) return;
                FailOnce = false;
                throw new InvalidOperationException("detach failed");
            }
            public void Tick(in GamePhaseContext ctx, float deltaTime) { }
        }
    }
}
