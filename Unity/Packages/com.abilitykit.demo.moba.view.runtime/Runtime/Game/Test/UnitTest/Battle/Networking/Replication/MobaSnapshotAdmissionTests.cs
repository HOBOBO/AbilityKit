using AbilityKit.Game.Battle.Agent;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class MobaSnapshotAdmissionTests
    {
        [Test]
        public void DeltaBeforeFullBaseline_RequestsResync()
        {
            var admission = new MobaSnapshotAdmission();
            admission.Reset(42UL);

            var result = admission.Admit(42UL, 10, isFullSnapshot: false);

            Assert.That(result.Status, Is.EqualTo(MobaSnapshotAdmissionStatus.BaselineRequired));
            Assert.That(result.Accepted, Is.False);
            Assert.That(result.ShouldRequestFullResync, Is.True);
            Assert.That(admission.HasBaseline, Is.False);
        }

        [Test]
        public void WrongWorld_IsRejectedWithoutReplacingBaseline()
        {
            var admission = new MobaSnapshotAdmission();
            admission.Reset(42UL);
            Assert.That(admission.Admit(42UL, 10, isFullSnapshot: true).Accepted, Is.True);

            var result = admission.Admit(43UL, 11, isFullSnapshot: true);

            Assert.That(result.Status, Is.EqualTo(MobaSnapshotAdmissionStatus.WrongWorld));
            Assert.That(result.ShouldRequestFullResync, Is.False);
            Assert.That(admission.LastAcceptedFrame, Is.EqualTo(10));
        }

        [Test]
        public void FullBaselineThenAdvancingDelta_AreAccepted()
        {
            var admission = new MobaSnapshotAdmission(maxDeltaFrameGap: 5);
            admission.Reset(42UL);

            var full = admission.Admit(42UL, 10, isFullSnapshot: true);
            var delta = admission.Admit(42UL, 15, isFullSnapshot: false);

            Assert.That(full.Accepted, Is.True);
            Assert.That(delta.Accepted, Is.True);
            Assert.That(admission.HasBaseline, Is.True);
            Assert.That(admission.LastAcceptedFrame, Is.EqualTo(15));
        }

        [TestCase(10)]
        [TestCase(9)]
        public void DuplicateOrStaleSnapshot_IsDroppedWithoutResync(int frame)
        {
            var admission = new MobaSnapshotAdmission();
            admission.Reset(42UL);
            Assert.That(admission.Admit(42UL, 10, isFullSnapshot: true).Accepted, Is.True);

            var result = admission.Admit(42UL, frame, isFullSnapshot: false);

            Assert.That(result.Status, Is.EqualTo(MobaSnapshotAdmissionStatus.StaleOrDuplicate));
            Assert.That(result.ShouldRequestFullResync, Is.False);
            Assert.That(admission.LastAcceptedFrame, Is.EqualTo(10));
        }

        [Test]
        public void ExcessiveDeltaGap_InvalidatesBaselineAndRequiresFullSnapshot()
        {
            var admission = new MobaSnapshotAdmission(maxDeltaFrameGap: 5);
            admission.Reset(42UL);
            Assert.That(admission.Admit(42UL, 10, isFullSnapshot: true).Accepted, Is.True);

            var gap = admission.Admit(42UL, 16, isFullSnapshot: false);
            var nextDelta = admission.Admit(42UL, 17, isFullSnapshot: false);
            var replacementFull = admission.Admit(42UL, 18, isFullSnapshot: true);

            Assert.That(gap.Status, Is.EqualTo(MobaSnapshotAdmissionStatus.FrameGapTooLarge));
            Assert.That(gap.ShouldRequestFullResync, Is.True);
            Assert.That(nextDelta.Status, Is.EqualTo(MobaSnapshotAdmissionStatus.BaselineRequired));
            Assert.That(replacementFull.Accepted, Is.True);
            Assert.That(admission.LastAcceptedFrame, Is.EqualTo(18));
        }

        [Test]
        public void RequireFullBaseline_RejectsDeltasUntilNewFullSnapshot()
        {
            var admission = new MobaSnapshotAdmission();
            admission.Reset(42UL);
            Assert.That(admission.Admit(42UL, 10, isFullSnapshot: true).Accepted, Is.True);

            admission.RequireFullBaseline();

            Assert.That(admission.Admit(42UL, 11, isFullSnapshot: false).Status,
                Is.EqualTo(MobaSnapshotAdmissionStatus.BaselineRequired));
            Assert.That(admission.Admit(42UL, 12, isFullSnapshot: true).Accepted, Is.True);
        }

        [Test]
        public void UnsupportedSchemaVersion_InvalidatesExistingBaseline()
        {
            var admission = new MobaSnapshotAdmission();
            admission.Reset(42UL);
            Assert.That(admission.Admit(
                42UL,
                10,
                isFullSnapshot: true,
                GatewayStateSyncSnapshot.CurrentSchemaVersion).Accepted, Is.True);

            var unsupported = admission.Admit(
                42UL,
                11,
                isFullSnapshot: false,
                GatewayStateSyncSnapshot.CurrentSchemaVersion + 1);
            var nextDelta = admission.Admit(
                42UL,
                12,
                isFullSnapshot: false,
                GatewayStateSyncSnapshot.CurrentSchemaVersion);

            Assert.That(unsupported.Status,
                Is.EqualTo(MobaSnapshotAdmissionStatus.UnsupportedSchemaVersion));
            Assert.That(unsupported.ShouldRequestFullResync, Is.True);
            Assert.That(admission.HasBaseline, Is.False);
            Assert.That(nextDelta.Status,
                Is.EqualTo(MobaSnapshotAdmissionStatus.BaselineRequired));
        }

        [Test]
        public void MaterializedDelta_PreservesUnchangedActorsAndAppliesRemoval()
        {
            var state = new MobaAuthoritativeSnapshotState();
            var full = new GatewayStateSyncSnapshot(
                42UL,
                10,
                1d,
                isFullSnapshot: true,
                new[]
                {
                    Actor(2, 20f),
                    Actor(1, 10f),
                    Actor(3, 30f)
                },
                GatewayStateSyncSnapshot.CurrentSchemaVersion);
            var delta = new GatewayStateSyncSnapshot(
                42UL,
                11,
                2d,
                isFullSnapshot: false,
                new[] { Actor(2, 25f) },
                GatewayStateSyncSnapshot.CurrentSchemaVersion,
                new[] { 3 });

            var baseline = state.Apply(in full);
            var materialized = state.Apply(in delta);

            Assert.That(baseline.Actors, Has.Length.EqualTo(3));
            Assert.That(baseline.Actors[0].ActorId, Is.EqualTo(1));
            Assert.That(materialized.IsFullSnapshot, Is.True);
            Assert.That(materialized.Actors, Has.Length.EqualTo(2));
            Assert.That(materialized.Actors[0].ActorId, Is.EqualTo(1));
            Assert.That(materialized.Actors[0].X, Is.EqualTo(10f));
            Assert.That(materialized.Actors[1].ActorId, Is.EqualTo(2));
            Assert.That(materialized.Actors[1].X, Is.EqualTo(25f));
            Assert.That(materialized.RemovedActorIds, Is.Empty);
            Assert.That(state.ActorCount, Is.EqualTo(2));
        }

        [Test]
        public void MaterializedState_ResetRemovesPreviousTimeline()
        {
            var state = new MobaAuthoritativeSnapshotState();
            var full = new GatewayStateSyncSnapshot(
                42UL,
                10,
                1d,
                isFullSnapshot: true,
                new[] { Actor(1, 10f) });
            state.Apply(in full);

            state.Reset();

            var replacement = new GatewayStateSyncSnapshot(
                42UL,
                20,
                2d,
                isFullSnapshot: true,
                System.Array.Empty<GatewayStateSyncActorSnapshot>());
            var materialized = state.Apply(in replacement);
            Assert.That(materialized.Actors, Is.Empty);
            Assert.That(state.ActorCount, Is.Zero);
        }

        private static GatewayStateSyncActorSnapshot Actor(int actorId, float x)
        {
            return new GatewayStateSyncActorSnapshot(
                actorId,
                x,
                0f,
                0f,
                0f,
                0f,
                0f,
                100f,
                100f,
                teamId: 1);
        }
    }
}
