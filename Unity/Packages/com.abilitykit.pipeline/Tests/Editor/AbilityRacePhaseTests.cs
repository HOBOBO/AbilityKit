#if UNITY_EDITOR
using NUnit.Framework;

namespace AbilityKit.Pipeline.Editor.Tests
{
    public sealed class AbilityRacePhaseTests
    {
        [Test]
        public void FirstCompletedBranch_WinsAndInterruptsRemainingBranches()
        {
            var context = new TestContext();
            var winner = new TestPhase("winner", updatesToComplete: 1);
            var loser = new TestPhase("loser", updatesToComplete: 3);
            var race = new AbilityRacePhase<TestContext>(new AbilityPipelinePhaseId("race"));
            race.AddSubPhase(winner);
            race.AddSubPhase(loser);

            race.Execute(context);
            race.OnUpdate(context, 0.02f);

            Assert.That(race.IsComplete, Is.True);
            Assert.That(winner.IsComplete, Is.True);
            Assert.That(winner.WasInterrupted, Is.False);
            Assert.That(loser.WasInterrupted, Is.True);
            Assert.That(context.IsAborted, Is.False);
        }

        [Test]
        public void Interrupt_StopsAllBranchesWithoutAbortingSharedContext()
        {
            var context = new TestContext();
            var first = new TestPhase("first", updatesToComplete: 2);
            var second = new TestPhase("second", updatesToComplete: 2);
            var race = new AbilityRacePhase<TestContext>(new AbilityPipelinePhaseId("race"));
            race.AddSubPhase(first);
            race.AddSubPhase(second);

            race.Execute(context);
            race.OnInterrupt(context);

            Assert.That(first.WasInterrupted, Is.True);
            Assert.That(second.WasInterrupted, Is.True);
            Assert.That(race.IsComplete, Is.True);
            Assert.That(context.IsAborted, Is.False);
        }

        [Test]
        public void PipelineInterrupt_InterruptsEachRaceBranchExactlyOnce()
        {
            var context = new TestContext();
            var first = new TestPhase("first", updatesToComplete: 2);
            var second = new TestPhase("second", updatesToComplete: 2);
            var race = new AbilityRacePhase<TestContext>(new AbilityPipelinePhaseId("race"));
            race.AddSubPhase(first);
            race.AddSubPhase(second);
            var pipeline = new TestPipeline();
            pipeline.AddPhase(race);
            var run = pipeline.Start(new TestConfig(), context);

            run.Tick(0f);
            run.Interrupt();

            Assert.That(first.InterruptCount, Is.EqualTo(1));
            Assert.That(second.InterruptCount, Is.EqualTo(1));
            Assert.That(run.State, Is.EqualTo(EAbilityPipelineState.Failed));
        }

        private sealed class TestContext : AAbilityPipelineContext
        {
        }

        private sealed class TestPhase : AbilityPipelinePhaseBase<TestContext>, IInterruptiblePhase<TestContext>
        {
            private readonly int _updatesToComplete;
            private int _updates;

            public TestPhase(string id, int updatesToComplete) : base(id)
            {
                _updatesToComplete = updatesToComplete;
            }

            public bool WasInterrupted { get; private set; }
            public int InterruptCount { get; private set; }

            protected override void OnExecute(TestContext context)
            {
            }

            public override void OnUpdate(TestContext context, float deltaTime)
            {
                _updates++;
                if (_updates >= _updatesToComplete) Complete(context);
            }

            public void OnInterrupt(TestContext context)
            {
                InterruptCount++;
                WasInterrupted = true;
                IsComplete = true;
            }
        }

        private sealed class TestPipeline : AbilityPipeline<TestContext>
        {
            protected override void ReleaseContext(TestContext context)
            {
            }
        }

        private sealed class TestConfig : IAbilityPipelineConfig
        {
            public int ConfigId => 1;
            public string ConfigName => "race-test";
            public System.Collections.Generic.IReadOnlyList<IAbilityPhaseConfig> PhaseConfigs =>
                System.Array.Empty<IAbilityPhaseConfig>();
            public bool AllowInterrupt => true;
            public bool AllowPause => true;
        }
    }
}
#endif
