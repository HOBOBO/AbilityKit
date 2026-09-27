using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.World.ECS;
using NUnit.Framework;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class PresentationCuePredictionReconciliationTests
    {
        [Test]
        public void Rejected_prediction_stops_even_when_authority_entry_uses_started_stage()
        {
            var resolver = new BattlePresentationCueResolver();
            var rejected = CreateCue(
                requestKey: "server-cast-q",
                predictionKey: 87,
                predictionState: PresentationCuePredictionState.Rejected,
                confirmedFrame: 121);

            var decision = resolver.Resolve(in rejected);

            Assert.AreEqual(BattlePresentationCueDecisionKind.Stop, decision.Kind);
            Assert.AreEqual(
                BattlePresentationCueRequestKey.FromPredictionKey(87),
                decision.RequestKey);
        }

        [Test]
        public void Prediction_key_correlates_client_and_server_entries_with_different_request_keys()
        {
            var predicted = CreateCue(
                requestKey: "client-local-q",
                predictionKey: 87,
                predictionState: PresentationCuePredictionState.Predicted,
                confirmedFrame: 0);
            var confirmed = CreateCue(
                requestKey: "server-authority-q",
                predictionKey: 87,
                predictionState: PresentationCuePredictionState.ServerConfirmed,
                confirmedFrame: 121);

            Assert.AreEqual(
                BattlePresentationCueRequestKey.From(in predicted),
                BattlePresentationCueRequestKey.From(in confirmed));
        }

        [Test]
        public void Reconciliation_tracker_ignores_older_authority_outcome_and_counts_results()
        {
            var tracker = new BattlePresentationCueReconciliationTracker(capacity: 4);
            var corrected = CreateCue(
                requestKey: "cast-q",
                predictionKey: 87,
                predictionState: PresentationCuePredictionState.Corrected,
                confirmedFrame: 125);
            var staleRejected = CreateCue(
                requestKey: "cast-q",
                predictionKey: 87,
                predictionState: PresentationCuePredictionState.Rejected,
                confirmedFrame: 124);
            var key = BattlePresentationCueRequestKey.From(in corrected);

            Assert.IsTrue(tracker.Accept(key, in corrected));
            Assert.IsFalse(tracker.Accept(key, in staleRejected));
            Assert.AreEqual(1L, tracker.CorrectedCount);
            Assert.AreEqual(0L, tracker.RejectedCount);
            Assert.AreEqual(1L, tracker.StaleUpdateCount);
        }

        [Test]
        public void View_handler_promotes_in_place_and_generation_change_destroys_old_view()
        {
            const int vfxId = 7101;
            var world = new EntityWorld();
            var root = world.Create("prediction-cue-root");
            var vfx = new BattleVfxManager(new VfxDatabase(new Dictionary<int, VfxDTO>
            {
                [vfxId] = new VfxDTO
                {
                    Id = vfxId,
                    Resource = "missing/prediction-cue",
                    DurationMs = 300,
                },
            }));
            var handler = new BattlePresentationCueViewEventHandler(world, null, vfx, in root);
            var predicted = CreateCue(
                requestKey: "client-cast-q",
                predictionKey: 87,
                predictionState: PresentationCuePredictionState.Predicted,
                confirmedFrame: 0);
            var confirmed = CreateCue(
                requestKey: "server-cast-q",
                predictionKey: 87,
                predictionState: PresentationCuePredictionState.ServerConfirmed,
                confirmedFrame: 121);
            var key = BattlePresentationCueRequestKey.From(in predicted);

            try
            {
                handler.HandleSnapshot(new[] { predicted });
                var predictedEntityId = GetActiveEntityId(handler, key);

                handler.HandleSnapshot(new[] { confirmed });
                var confirmedEntityId = GetActiveEntityId(handler, key);
                handler.HandleSnapshot(new[] { confirmed });

                Assert.AreEqual(predictedEntityId, confirmedEntityId);
                Assert.AreEqual(1L, handler.ConfirmedPredictionCount);
                Assert.AreEqual(1L, handler.DuplicatePredictionUpdateCount);

                var previousGeneration = handler.ReconciliationGeneration;
                Assert.IsTrue(handler.BeginReconciliationGeneration(previousGeneration + 1));
                Assert.IsFalse(world.IsAlive(predictedEntityId));
                Assert.Throws<System.InvalidOperationException>(() => GetActiveEntityId(handler, key));
                Assert.IsFalse(handler.BeginReconciliationGeneration(previousGeneration));
            }
            finally
            {
                if (root.IsValid && world.IsAlive(root.Id))
                {
                    world.DestroyRecursive(root.Id);
                }
            }
        }

        private static PresentationCueData CreateCue(
            string requestKey,
            int predictionKey,
            PresentationCuePredictionState predictionState,
            int confirmedFrame)
        {
            return new PresentationCueData(
                stage: PresentationCueStage.Started,
                cueKind: "skill.cast",
                cueVfxId: "",
                cueSfxId: "",
                templateId: 0,
                vfxId: 7101,
                sfxId: 0,
                requestKey: requestKey,
                sourceActorId: 11,
                targetActorId: 22,
                triggerEventId: 501,
                triggerEventName: "SkillCast",
                triggerId: 6001,
                phase: 1,
                priority: 0,
                order: 1,
                actionIndex: -1,
                interruptReason: 0,
                interruptSourceName: null,
                interruptTriggerId: 0,
                interruptConditionPassed: false,
                targets: null,
                positions: null,
                offsetX: 0f,
                offsetY: 0f,
                offsetZ: 0f,
                durationMsOverride: 300,
                scale: 1f,
                colorR: 1f,
                colorG: 1f,
                colorB: 1f,
                colorA: 1f,
                predictionKey: predictionKey,
                predictionState: predictionState,
                predictedFrame: 120,
                confirmedFrame: confirmedFrame);
        }

        private static EC.IEntityId GetActiveEntityId(
            BattlePresentationCueViewEventHandler handler,
            BattlePresentationCueRequestKey requestKey)
        {
            if (handler.TryGetActiveEntityId(requestKey, out var id)) return id;

            throw new System.InvalidOperationException("No active presentation cue entity was found.");
        }
    }
}
