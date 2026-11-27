using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using AbilityKit.BattleScenario;
using AbilityKit.BattleScenario.Editor;
using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.Game.Flow;
using AbilityKit.Protocol.Moba;
using AbilityKit.Scenario;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using SkillReplayCase = AbilityKit.BattleScenario.Editor.LocalStateSyncReplay.SkillReplayCase;

namespace AbilityKit.Demo.Moba.Editor.BattleScenario.Tests
{
    public sealed class MobaBattleScenarioSyncBackendTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void FormalStateSyncSkillReplay_OnlyPublishesAfterCatchUp(bool accepted)
        {
            var capture = BattleScenarioEditorIntegration.CaptureStateSyncSkillReplay;
            Assert.That(capture, Is.Not.Null);
            var replay = LocalStateSyncReplay.Parse(capture(accepted ? SkillReplayCase.Confirmed : SkillReplayCase.Rejected).Serialize());
            var phases = replay.BuildTimeline().Frames;
            var compare = phases.Single(frame => frame.Phase == LocalStateSyncReplay.FramePhase.Compare);
            var anchor = compare.SkillReference.Skill;
            Assert.That(anchor.OldPrediction.CastElapsedMs, Is.EqualTo(67));
            Assert.That(anchor.Authority.CastElapsedMs, Is.EqualTo(accepted ? (int?)33 : null));
            var steps = phases.Where(frame => frame.SkillStep != null).ToArray();
            Assert.That(steps.Select(frame => frame.SkillStep.Tick), Is.EqualTo(new[] { 13, 14, 15 }));
            Assert.That(steps.Select(frame => frame.SkillStep.State.CastElapsedMs), Is.EqualTo(
                accepted ? new int?[] { 67, 100, 133 } : new int?[] { null, 33, 67 }));
            Assert.That(replay.SkillDelivery.TerminalThroughSequence, Is.EqualTo(2));
            Assert.That(replay.SkillDelivery.ReplayedSequence, Is.EqualTo(3));
            Assert.That(replay.Checkpoints.Last().Skill.Published.CastElapsedMs,
                Is.EqualTo(steps.Last().SkillStep.State.CastElapsedMs));
        }

        [TestCase("HistoryGap", 12, 1, 2, 1, 13, 33)]
        [TestCase("MissingProof", 12, 1, 0, 2, null, 33)]
        [TestCase("GenerationChanged", 15, 2, 0, 0, null, null)]
        public void FormalStateSyncSkillReplay_FallsBackToCommittedAuthorityWithoutReplaying(string reason,
            int appliedTick, int generation, long terminal, int retainedInputs, int? missingTick, int? castElapsed)
        {
            var capture = BattleScenarioEditorIntegration.CaptureStateSyncSkillReplay;
            Assert.That(capture, Is.Not.Null);
            var recipe = (SkillReplayCase)Enum.Parse(typeof(SkillReplayCase), reason);
            var replay = LocalStateSyncReplay.Parse(capture(recipe).Serialize());
            Assert.That(capture(recipe).Serialize(), Is.EqualTo(replay.Serialize()));
            var skill = replay.SkillDelivery;
            var final = replay.Checkpoints.Last();
            Assert.That(skill.Fallback.Reason, Is.EqualTo(recipe));
            Assert.That(final.AppliedTick, Is.EqualTo(appliedTick));
            Assert.That(final.Generation, Is.EqualTo(generation));
            Assert.That(skill.TerminalThroughSequence, Is.EqualTo(terminal));
            Assert.That(skill.Fallback.RetainedInputCount, Is.EqualTo(retainedInputs));
            Assert.That(skill.Fallback.MissingHistoryTick, Is.EqualTo(missingTick));
            Assert.That(skill.ReplayedSequence, Is.Null);
            Assert.That(skill.Steps, Is.Empty);
            Assert.That(final.Skill.OldPrediction.CastElapsedMs, Is.EqualTo(167));
            Assert.That(final.Skill.Published.CastElapsedMs, Is.EqualTo(castElapsed));
            var deliveryFrames = replay.BuildTimeline().Frames.Where(frame => frame.Point.DeliveryTick == skill.DeliveryTick);
            Assert.That(deliveryFrames.Select(frame => frame.Phase), Is.EqualTo(new[] { LocalStateSyncReplay.FramePhase.Checkpoint }));

            skill.ReplayedSequence = skill.PendingSequence;
            Assert.Throws<InvalidDataException>(replay.Validate, "A fallback cannot claim it replayed pending input.");
            skill.ReplayedSequence = null;
            final.AppliedTick--;
            Assert.Throws<InvalidDataException>(replay.Validate, "A fallback cannot display an unadopted Tick.");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FormalStateSyncSkillReplay_RecordsEconomyAndAdmissionAtOriginalInputTick(bool accepted)
        {
            const long rawPerMana = 4294967296L;
            var capture = BattleScenarioEditorIntegration.CaptureStateSyncSkillReplay;
            Assert.That(capture, Is.Not.Null);
            var recipe = accepted ? SkillReplayCase.EconomyConfirmed : SkillReplayCase.EconomyRejected;
            var replay = LocalStateSyncReplay.Parse(capture(recipe).Serialize());
            Assert.That(capture(recipe).Serialize(), Is.EqualTo(replay.Serialize()));
            Assert.That(replay.TickRate, Is.EqualTo(20));
            Assert.That(replay.SkillDelivery.CastEconomy, Is.True);
            Assert.That(replay.SkillDelivery.TerminalThroughSequence, Is.EqualTo(2));
            Assert.That(replay.SkillDelivery.ReplayedSequence, Is.EqualTo(3));
            Assert.That(replay.SkillDelivery.OldInputRejectCode, Is.EqualTo("Skill is cooling down."));
            var frames = replay.BuildTimeline().Frames;
            var anchor = frames.Single(frame => frame.Phase == LocalStateSyncReplay.FramePhase.Anchor).SkillReference;
            Assert.That(anchor.Skill.Authority.ManaRaw, Is.EqualTo((accepted ? 90 : 100) * rawPerMana));
            Assert.That(anchor.Skill.Authority.PrimaryCooldownEndMs, Is.EqualTo(accepted ? 1100 : 0));
            Assert.That(anchor.Skill.OldPrediction.PrimaryCooldownEndMs, Is.EqualTo(1050));
            var steps = frames.Where(frame => frame.SkillStep != null).Select(frame => frame.SkillStep).ToArray();
            Assert.That(steps.Select(step => step.Tick), Is.EqualTo(new[] { 13, 14, 15 }));
            Assert.That(steps.Select(step => step.State.ManaRaw), Is.EqualTo(
                (accepted ? new[] { 90, 90, 90 } : new[] { 100, 90, 90 }).Select(mana => mana * rawPerMana)));
            Assert.That(steps.Select(step => step.State.PrimaryCooldownEndMs), Is.EqualTo(
                accepted ? new long[] { 1100, 1100, 1100 } : new long[] { 0, 1200, 1200 }));
            Assert.That(steps.Select(step => step.State.CastElapsedMs), Is.EqualTo(
                accepted ? new int?[] { 100, 150, 200 } : new int?[] { null, 50, 100 }));
            Assert.That(steps.Select(step => step.InputRejection), Is.EqualTo(new[] { null, accepted ? "Skill is cooling down." : null, null }));
            var published = frames.Single(frame => frame.Phase == LocalStateSyncReplay.FramePhase.Checkpoint &&
                frame.Point.DeliveryTick == 15).Point;
            Assert.That(published.Skill.Published.Matches(steps.Last().State), Is.True);
            var continued = frames.Last().Point;
            Assert.That(continued.DeliveryTick, Is.EqualTo(16));
            Assert.That(continued.AppliedTick, Is.EqualTo(12));
            Assert.That(continued.Skill.Published.ManaRaw, Is.EqualTo(90 * rawPerMana));
            Assert.That(continued.Skill.Published.PrimaryCooldownEndMs, Is.EqualTo(accepted ? 1100 : 1200));
            Assert.That(continued.Skill.Published.CastElapsedMs, Is.EqualTo(accepted ? 250 : 150));

            steps[0].InputRejection = "输入未在该 Tick 执行";
            Assert.Throws<InvalidDataException>(replay.Validate);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FormalStateSyncSkillReplay_DoesNotPublishPartialCastCandidate(bool validProof)
        {
            var scope = new LocalStateSyncCastScope(1, 1, Guid.NewGuid(), Guid.NewGuid(), 1, 7, 1);
            var worlds = new System.Collections.Generic.List<ProbeCastWorld>();
            using var session = new LocalStateSyncCastSession<int, int>(in scope, () =>
            {
                var world = new ProbeCastWorld(worlds.Count == 1 ? 15 : 0);
                worlds.Add(world);
                return world;
            });
            var initial = new LocalStateSyncCastStamp(in scope, 10, 1);
            var baseline = new LocalStateSyncCastAnchor<int, int>(in initial, 100, 100);
            Assert.That(session.Adopt(in baseline, new LocalStateSyncCastTerminalProof(in initial, 1)),
                Is.EqualTo(LocalStateSyncCastResult.Published));
            Assert.That(session.Advance(11, LocalCastPress(11, 2)), Is.True);
            Assert.That(session.Advance(12, null), Is.True);
            Assert.That(session.Advance(13, null), Is.True);
            Assert.That(session.Advance(14, LocalCastPress(14, 3)), Is.True);
            Assert.That(session.Advance(15, null), Is.True);
            var stamp = new LocalStateSyncCastStamp(in scope, 12, 2);
            var staleScope = new LocalStateSyncCastScope(1, 1, scope.Stream, scope.InputGeneration, 1, 7, 2);
            var staleProof = new LocalStateSyncCastStamp(in staleScope, 12, 2);
            var anchor = new LocalStateSyncCastAnchor<int, int>(in stamp, 90, 90);

            Assert.That(session.Adopt(in anchor, new LocalStateSyncCastTerminalProof(validProof ? stamp : staleProof, 2)),
                Is.EqualTo(validProof ? LocalStateSyncCastResult.CandidateFailed : LocalStateSyncCastResult.AuthorityOnly));
            Assert.That(session.PendingCount, Is.EqualTo(validProof ? 1 : 2));
            Assert.That(session.Observation, Is.EqualTo(90));
            Assert.That(session.PublishedTick, Is.EqualTo(12));
            Assert.That(session.HasPrediction, Is.False);
            Assert.That(session.LastReplay, Is.Empty);
            Assert.That(worlds.Count, Is.EqualTo(validProof ? 2 : 1));
            Assert.That(worlds.All(world => world.Disposed), Is.True);
            if (validProof)
                Assert.That(worlds[1].PressedTick, Is.EqualTo(14), "Failure must occur after a candidate has side effects.");
            Assert.That(session.Adopt(in anchor, new LocalStateSyncCastTerminalProof(in stamp, 2)), Is.EqualTo(LocalStateSyncCastResult.Ignored));
            Assert.That(session.Advance(16, LocalCastPress(16, 4)), Is.True);
            Assert.That(session.Observation, Is.EqualTo(90));
            Assert.That(session.PendingCount, Is.EqualTo(validProof ? 2 : 3));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FormalStateSyncSkillReplay_UsesBattleCandidateForBothTerminalOutcomes(bool confirmed)
        {
            var capture = BattleScenarioEditorIntegration.CaptureStateSyncSkillReplay;
            Assert.That(capture, Is.Not.Null);
            var recipe = confirmed ? SkillReplayCase.BattleCastConfirmed : SkillReplayCase.BattleCastRejected;
            var json = capture(recipe).Serialize();
            var replay = LocalStateSyncReplay.Parse(json);
            Assert.That(capture(recipe).Serialize(), Is.EqualTo(json));
            var skill = replay.SkillDelivery;
            Assert.That(skill.FormalBattleCandidate, Is.True);
            Assert.That(skill.TerminalThroughSequence, Is.EqualTo(1));
            Assert.That(skill.ReplayedSequence, Is.EqualTo(2));
            Assert.That(skill.OldInputRejectCode, Is.Null);
            var frames = replay.BuildTimeline().Frames;
            var compare = frames.Single(frame => frame.Phase == LocalStateSyncReplay.FramePhase.Compare);
            var anchor = compare.SkillReference.Skill;
            Assert.That(anchor.Authority.Matches(anchor.OldPrediction), Is.EqualTo(confirmed));
            Assert.That(anchor.OldPrediction.PrimaryCooldownEndMs, Is.Positive);
            Assert.That(anchor.Authority.PrimaryCooldownEndMs, Is.EqualTo(confirmed ? anchor.OldPrediction.PrimaryCooldownEndMs : 0));
            Assert.That(skill.Steps.Select(step => step.Tick), Is.EqualTo(Enumerable.Range(101, 6)));
            Assert.That(skill.Steps.All(step => step.State.PrimaryCooldownEndMs == anchor.Authority.PrimaryCooldownEndMs), Is.True);
            Assert.That(skill.Steps[0].State.SecondaryCooldownEndMs, Is.Zero);
            Assert.That(skill.Steps[1].State.SecondaryCooldownEndMs, Is.Positive);
            Assert.That(skill.Steps[1].State.ManaRaw, Is.LessThan(anchor.Authority.ManaRaw));
            var delivered = frames.Where(frame => frame.Point.DeliveryTick == 106).ToArray();
            Assert.That(delivered.Select(frame => frame.Phase), Is.EqualTo(new[]
                { LocalStateSyncReplay.FramePhase.Compare, LocalStateSyncReplay.FramePhase.Anchor }
                .Concat(Enumerable.Repeat(LocalStateSyncReplay.FramePhase.ReplayTick, 6))
                .Append(LocalStateSyncReplay.FramePhase.Checkpoint)));
            Assert.That(delivered.All(frame => frame.Point.AppliedTick == 100), Is.True);
            Assert.That(delivered.Last().Point.Skill.Published.Matches(skill.Steps.Last().State), Is.True);
            Assert.That(frames.Last().Point.DeliveryTick, Is.EqualTo(107));
            Assert.That(frames.Last().Point.Skill.Published.Matches(frames.Last().Point.Skill.Authority), Is.True);
        }

        [Test]
        public void FormalStateSyncSkillReplay_RestoresWindupWithoutReplayingTerminalPress()
        {
            var capture = BattleScenarioEditorIntegration.CaptureStateSyncSkillReplay;
            Assert.That(capture, Is.Not.Null);
            var json = capture(SkillReplayCase.BattleCastWindup).Serialize();
            Assert.That(capture(SkillReplayCase.BattleCastWindup).Serialize(), Is.EqualTo(json));
            var replay = LocalStateSyncReplay.Parse(json);
            var skill = replay.SkillDelivery;
            Assert.That(skill.FormalWindup, Is.True);
            Assert.That(skill.TerminalThroughSequence, Is.EqualTo(1));
            Assert.That(skill.PendingSequence, Is.Zero);
            Assert.That(skill.ReplayedSequence, Is.Null);
            var frames = replay.BuildTimeline().Frames;
            var comparison = frames.Single(frame => frame.Phase == LocalStateSyncReplay.FramePhase.Compare);
            var anchor = comparison.SkillReference.Skill;
            Assert.That(comparison.SkillReference.DeliveryTick, Is.EqualTo(3));
            Assert.That(anchor.Authority.CastElapsedMs, Is.Positive);
            Assert.That(anchor.Authority.CastElapsedMs, Is.LessThan(anchor.OldPrediction.CastElapsedMs.Value));
            Assert.That(anchor.Authority.PrimaryCooldownEndMs, Is.GreaterThan(anchor.OldPrediction.PrimaryCooldownEndMs));
            Assert.That(skill.Steps.Select(step => step.Tick), Is.EqualTo(Enumerable.Range(4, 6)));
            Assert.That(skill.Steps.Take(5).All(step => step.State.CastElapsedMs.HasValue), Is.True);
            Assert.That(skill.Steps.All(step => step.State.ManaRaw == anchor.Authority.ManaRaw &&
                step.State.PrimaryCooldownEndMs == anchor.Authority.PrimaryCooldownEndMs), Is.True);
            foreach (var step in skill.Steps)
                Assert.That(step.State.Matches(replay.Checkpoints.Single(point => point.DeliveryTick == step.Tick).Skill.Authority), Is.True);
            var delivered = frames.Where(frame => frame.Point.DeliveryTick == 9).ToArray();
            Assert.That(delivered.Select(frame => frame.Phase), Is.EqualTo(new[]
                { LocalStateSyncReplay.FramePhase.Compare, LocalStateSyncReplay.FramePhase.Anchor }
                .Concat(Enumerable.Repeat(LocalStateSyncReplay.FramePhase.ReplayTick, 6))
                .Append(LocalStateSyncReplay.FramePhase.Checkpoint)));
            Assert.That(delivered.All(frame => frame.Point.AppliedTick == 3), Is.True);
            Assert.That(delivered.Last().Point.Skill.Published.Matches(skill.Steps.Last().State), Is.True);
            Assert.That(frames.Last().Point.DeliveryTick, Is.EqualTo(10));
            Assert.That(frames.Last().Point.Skill.Published.CastElapsedMs, Is.Null);
            Assert.That(frames.Last().Point.Skill.Published.Matches(frames.Last().Point.Skill.Authority), Is.True);
        }

        [Test]
        public void FormalStateSyncBattleWorld_ReportsBuildPhases()
        {
            MobaStateSyncClientReplay.MeasureSettledBattleCastWorld();
            for (var sample = 1; sample <= 6; sample++)
            {
                var timing = MobaStateSyncClientReplay.MeasureSettledBattleCastWorld();
                Assert.That(timing.ConfigLoadMs, Is.GreaterThan(0d));
                TestContext.WriteLine(FormattableString.Invariant($"sample {sample}: service and blueprint setup={timing.ServiceSetupMs:F3} ms, world construction={timing.WorldConstructionMs:F3} ms, initialize={timing.InitializeMs:F3} ms, nested config load={timing.ConfigLoadMs:F3} ms"));
            }
        }

        [Test]
        public void FormalStateSyncFlight_RendersBeforeAndAfterRejection()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("3D 预览验收需要 Unity 图形设备。");

            var replay = MobaStateSyncClientReplay.CaptureSkill(SkillReplayCase.BattleFlightReject);
            var frames = replay.BuildTimeline(inspectSteps: false).Frames
                .Where(frame => frame.Point.DeliveryTick == replay.SkillDelivery.DeliveryTick).ToArray();
            Assert.That(frames.Length, Is.EqualTo(2));
            Assert.That(frames[0].FlightPrediction, Is.Not.Null);
            Assert.That(frames[1].FlightPrediction, Is.Null);
            using var preview = AbilityKit.Game.Flow.BattleActorReplayPreview.Create(replay.ActorModelId.Value);
            Assert.That(preview, Is.Not.Null);
            var beforePose = ActorPose(frames[0].PredictionPose);
            var sample = frames[0].FlightPrediction;
            var flight = new Vector3(sample.X, sample.Y, sample.Z) * 0.001f;
            var bounds = new Bounds(beforePose.position, Vector3.zero);
            bounds.Encapsulate(flight);
            var rect = new Rect(0, 0, 640, 360);
            Texture2D before = null;
            Texture2D after = null;
            try
            {
                preview.SetFlightPositions(null, flight);
                before = preview.RenderStatic(rect, bounds, null, beforePose);
                preview.SetFlightPositions(null, null);
                after = preview.RenderStatic(rect, bounds, null, ActorPose(frames[1].PredictionPose));
                Assert.That(before, Is.Not.Null);
                Assert.That(after, Is.Not.Null);
                var beforePixels = before.GetPixels32();
                var afterPixels = after.GetPixels32();
                Assert.That(afterPixels.Distinct().Count(), Is.GreaterThan(24), "追平后模型画面不应是纯色。");
                Assert.That(beforePixels.Zip(afterPixels, (left, right) => !left.Equals(right)).Count(changed => changed),
                    Is.GreaterThan(24), "拒绝后弹体消失应改变画面。");

                var output = Environment.GetEnvironmentVariable("ABILITYKIT_STATE_REPLAY_ARTIFACTS");
                if (!string.IsNullOrWhiteSpace(output) && Directory.Exists(output))
                {
                    File.WriteAllBytes(Path.Combine(output, "flight-reject-before.png"), before.EncodeToPNG());
                    File.WriteAllBytes(Path.Combine(output, "flight-reject-after.png"), after.EncodeToPNG());
                }
            }
            finally
            {
                if (before != null) UnityEngine.Object.DestroyImmediate(before);
                if (after != null) UnityEngine.Object.DestroyImmediate(after);
            }

            static Pose ActorPose(LocalStateSyncReplay.ActorPose pose) => new Pose(
                new Vector3(pose.X, pose.Y, pose.Z), new Quaternion(pose.Qx, pose.Qy, pose.Qz, pose.Qw));
        }

        [TestCase(nameof(SkillReplayCase.BattleFlightRestore))]
        [TestCase(nameof(SkillReplayCase.BattleFlightReplay))]
        [TestCase(nameof(SkillReplayCase.BattleFlightReject))]
        public void FormalStateSyncFlight_ProjectsCommittedAuthorityAndPublishedPrediction(string recipeName)
        {
            var recipe = (SkillReplayCase)Enum.Parse(typeof(SkillReplayCase), recipeName);
            var capture = BattleScenarioEditorIntegration.CaptureStateSyncSkillReplay;
            Assert.That(capture, Is.Not.Null);
            var json = capture(recipe).Serialize();
            Assert.That(capture(recipe).Serialize(), Is.EqualTo(json));
            var replay = LocalStateSyncReplay.Parse(json);
            var skill = replay.SkillDelivery;
            Assert.That(skill.FormalFlight, Is.True);
            Assert.That(replay.ActorModelId, Is.GreaterThan(0));
            Assert.That(replay.Checkpoints.All(point => point.AuthorityPose?.IsValid == true &&
                point.Skill.OldPrediction.Pose?.IsValid == true && point.Skill.Published.Pose?.IsValid == true), Is.True);
            Assert.That(skill.Steps.Select(step => step.Tick), Is.EqualTo(Enumerable.Range(4, 4)));
            foreach (var step in skill.Steps)
                Assert.That(step.State.Matches(replay.Checkpoints[step.Tick].Skill.Authority), Is.True);

            var frames = replay.BuildTimeline(inspectSteps: false).Frames;
            var delivery = frames.Where(frame => frame.Point.DeliveryTick == 7).ToArray();
            Assert.That(delivery.Select(frame => frame.Phase), Is.EqualTo(new[]
                { LocalStateSyncReplay.FramePhase.BeforePublication, LocalStateSyncReplay.FramePhase.Checkpoint }));
            Assert.That(delivery[0].FlightAuthority, Is.Null);
            Assert.That(delivery[0].FlightPrediction, Is.Not.Null);
            Assert.That(delivery[0].PredictionPose, Is.Not.Null);
            Assert.That(delivery[1].AuthorityProjectionPose, Is.Not.Null);
            Assert.That(delivery[1].PredictionPose, Is.Not.Null);
            Assert.That(delivery[1].Point.AppliedTick, Is.EqualTo(3));
            var restored = delivery[1];
            if (recipe == SkillReplayCase.BattleFlightRestore)
            {
                Assert.That(restored.FlightAuthority, Is.Not.Null);
                Assert.That(restored.FlightPrediction.ActorId, Is.EqualTo(restored.FlightAuthority.ActorId));
                Assert.That(restored.FlightPrediction.EntityVersion, Is.EqualTo(restored.FlightAuthority.EntityVersion));
                Assert.That(restored.FlightPrediction.Z, Is.GreaterThan(restored.FlightAuthority.Z));
                Assert.That(restored.FlightPrediction.Z, Is.LessThan(delivery[0].FlightPrediction.Z));
            }
            else
            {
                Assert.That(restored.FlightAuthority, Is.Null);
                if (recipe == SkillReplayCase.BattleFlightReplay)
                {
                    Assert.That(skill.Steps[0].State.Flight, Is.Null);
                    Assert.That(skill.Steps[1].State.Flight, Is.Not.Null);
                    Assert.That(restored.FlightPrediction.ProjectileId, Is.EqualTo(skill.Steps[1].State.Flight.ProjectileId));
                }
                else
                {
                    Assert.That(restored.FlightPrediction, Is.Null);
                    Assert.That(skill.Steps.All(step => step.State.Flight == null), Is.True);
                }
            }
            var afterPublication = frames.Where(frame => frame.Point.DeliveryTick > 7).ToArray();
            Assert.That(afterPublication.Zip(afterPublication.Skip(1), (before, after) =>
                before.FlightPrediction != null && after.FlightPrediction == null).Count(exited => exited),
                Is.EqualTo(recipe == SkillReplayCase.BattleFlightReject ? 0 : 1));
            Assert.That(frames.Last().Point.AppliedTick, Is.EqualTo(20));
            Assert.That(frames.Last().FlightAuthority, Is.Null);
            Assert.That(frames.Last().FlightPrediction, Is.Null);
        }

        [Test]
        public void FormalStateSyncReplay_RestoresMovementAndWindupInTheSameBattleCandidate()
        {
            var capture = BattleScenarioEditorIntegration.CaptureStateSyncSkillReplay;
            Assert.That(capture, Is.Not.Null);
            var json = capture(SkillReplayCase.BattleCastMoveWindup).Serialize();
            Assert.That(capture(SkillReplayCase.BattleCastMoveWindup).Serialize(), Is.EqualTo(json));
            var replay = LocalStateSyncReplay.Parse(json);
            var skill = replay.SkillDelivery;
            Assert.That(skill.FormalMovement && skill.FormalWindup, Is.True);
            Assert.That(skill.AnchorTick, Is.EqualTo(4));
            Assert.That(skill.DeliveryTick, Is.EqualTo(9));
            Assert.That(skill.TerminalThroughSequence, Is.EqualTo(2));
            Assert.That(skill.InputTick, Is.EqualTo(6));
            Assert.That(skill.PendingSequence, Is.EqualTo(3));
            Assert.That(skill.ReplayedSequence, Is.EqualTo(3));

            var anchor = replay.Checkpoints[4].Skill;
            Assert.That(anchor.Authority.PositionX.Value, Is.LessThan(anchor.OldPrediction.PositionX.Value));
            Assert.That(anchor.Authority.CastElapsedMs.Value, Is.LessThan(anchor.OldPrediction.CastElapsedMs.Value));
            Assert.That(skill.Steps.Select(step => step.Tick), Is.EqualTo(Enumerable.Range(5, 5)));
            Assert.That(skill.Steps[0].State.PositionX.Value, Is.GreaterThan(anchor.Authority.PositionX.Value));
            Assert.That(skill.Steps[1].State.PositionX, Is.EqualTo(skill.Steps[0].State.PositionX));
            Assert.That(skill.Steps[1].State.PositionZ.Value, Is.GreaterThan(skill.Steps[0].State.PositionZ.Value));
            foreach (var step in skill.Steps)
                Assert.That(step.State.Matches(replay.Checkpoints[step.Tick].Skill.Authority), Is.True);

            var delivered = replay.BuildTimeline().Frames.Where(frame => frame.Point.DeliveryTick == 9).ToArray();
            Assert.That(delivered.Select(frame => frame.Phase), Is.EqualTo(new[]
                { LocalStateSyncReplay.FramePhase.Compare, LocalStateSyncReplay.FramePhase.Anchor }
                .Concat(Enumerable.Repeat(LocalStateSyncReplay.FramePhase.ReplayTick, 5))
                .Append(LocalStateSyncReplay.FramePhase.Checkpoint)));
            Assert.That(delivered.All(frame => frame.Point.AppliedTick == 4), Is.True);
            var published = delivered.Last().Point.Skill.Published;
            Assert.That(published.Matches(skill.Steps.Last().State), Is.True);
            var next = replay.Checkpoints[10].Skill;
            Assert.That(next.Published.PositionZ.Value, Is.GreaterThan(published.PositionZ.Value));
            Assert.That(next.Published.Matches(next.Authority), Is.True);
        }

        [Test]
        public void FormalStateSyncReplay_RejectsPublishedLuxQBeforeReleaseAndKeepsTurn()
        {
            var capture = BattleScenarioEditorIntegration.CaptureStateSyncSkillReplay;
            Assert.That(capture, Is.Not.Null);
            var json = capture(SkillReplayCase.BattleCastMoveRejectBeforeRelease).Serialize();
            Assert.That(capture(SkillReplayCase.BattleCastMoveRejectBeforeRelease).Serialize(), Is.EqualTo(json));
            var replay = LocalStateSyncReplay.Parse(json);
            var skill = replay.SkillDelivery;
            Assert.That(replay.ActorModelId, Is.EqualTo(3007));
            Assert.That(skill.FormalBattleCandidate && skill.FormalMovement && !skill.FormalWindup, Is.True);
            Assert.That(skill.AnchorTick, Is.EqualTo(3));
            Assert.That(skill.InputTick, Is.EqualTo(5));
            Assert.That(skill.DeliveryTick, Is.EqualTo(7));
            Assert.That(skill.TerminalThroughSequence, Is.EqualTo(2));
            Assert.That(skill.PendingSequence, Is.EqualTo(3));
            Assert.That(skill.ReplayedSequence, Is.EqualTo(3));

            var delivered = replay.Checkpoints[7];
            Assert.That(delivered.AppliedTick, Is.EqualTo(3));
            Assert.That(delivered.Skill.OldPrediction.CastElapsedMs, Is.InRange(0, 249));
            Assert.That(delivered.Skill.OldPrediction.ManaRaw, Is.LessThan(delivered.Skill.Published.ManaRaw));
            Assert.That(delivered.Skill.OldPrediction.PrimaryCooldownEndMs, Is.GreaterThan(0));
            Assert.That(delivered.Skill.Published.PrimaryCooldownEndMs, Is.Zero);
            Assert.That(delivered.Skill.Published.CastElapsedMs, Is.Null);
            Assert.That(skill.Steps.Select(step => step.Tick), Is.EqualTo(Enumerable.Range(4, 4)));
            Assert.That(skill.Steps[0].State.PositionX, Is.GreaterThan(replay.Checkpoints[3].Skill.Authority.PositionX));
            Assert.That(skill.Steps[1].State.PositionX, Is.EqualTo(skill.Steps[0].State.PositionX));
            Assert.That(skill.Steps[1].State.PositionZ, Is.GreaterThan(skill.Steps[0].State.PositionZ));
            foreach (var step in skill.Steps)
                Assert.That(step.State.Matches(replay.Checkpoints[step.Tick].Skill.Authority), Is.True);
            Assert.That(replay.Checkpoints[8].AppliedTick, Is.EqualTo(3));
            Assert.That(replay.Checkpoints[8].Skill.Published.Matches(replay.Checkpoints[8].Skill.Authority), Is.True);
            Assert.That(replay.Checkpoints[8].Skill.Published.PositionZ, Is.GreaterThan(delivered.Skill.Published.PositionZ));

            var preview = replay.BuildTimeline(inspectSteps: false);
            var deliveryFrames = preview.Frames.Where(frame => frame.Point.DeliveryTick == 7).ToArray();
            Assert.That(deliveryFrames.Select(frame => frame.Phase), Is.EqualTo(new[]
                { LocalStateSyncReplay.FramePhase.BeforePublication, LocalStateSyncReplay.FramePhase.Checkpoint }));
            Assert.That(deliveryFrames[0].PredictionPose, Is.SameAs(delivered.Skill.OldPrediction.Pose));
            Assert.That(deliveryFrames[1].PredictionPose, Is.SameAs(delivered.Skill.Published.Pose));
            Assert.That(deliveryFrames[1].AuthorityProjectionPose, Is.SameAs(delivered.AuthorityPose));
        }

        [Test]
        public void FormalStateSyncPreview_DefaultsToPlaybackOfCommittedAndPublishedPoses()
        {
            var replay = LocalStateSyncReplay.Parse(BattleScenarioEditorIntegration.CaptureStateSyncSkillReplay(
                SkillReplayCase.BattleCastMoveWindup).Serialize());
            Assert.That(replay.ActorModelId, Is.EqualTo(3003));
            var preview = replay.BuildTimeline(inspectSteps: false);
            Assert.That(preview.Frames.Count, Is.EqualTo(replay.Checkpoints.Length + 1));
            Assert.That(preview.Frames.All(frame => frame.AuthorityProjectionPose != null && frame.PredictionPose != null), Is.True);
            var tick = replay.SkillDelivery.DeliveryTick;
            var delivery = preview.Frames.Where(frame => frame.Point.DeliveryTick == tick).ToArray();
            Assert.That(delivery.Select(frame => frame.Phase), Is.EqualTo(new[]
                { LocalStateSyncReplay.FramePhase.BeforePublication, LocalStateSyncReplay.FramePhase.Checkpoint }));
            Assert.That(delivery[0].PredictionPose, Is.SameAs(delivery[0].Point.Skill.OldPrediction.Pose));
            Assert.That(delivery[0].AuthorityProjectionPose, Is.SameAs(replay.Checkpoints[tick - 1].AuthorityPose));
            Assert.That(delivery[1].PredictionPose, Is.SameAs(delivery[1].Point.Skill.Published.Pose));
            Assert.That(delivery[1].PredictionPose.Matches(delivery[0].PredictionPose), Is.False);
            var anchor = replay.Checkpoints[replay.SkillDelivery.AnchorTick].Skill.Authority.Pose;
            Assert.That(delivery[1].AuthorityProjectionPose.X, Is.EqualTo(anchor.X));
            Assert.That(delivery[1].AuthorityProjectionPose.Z, Is.EqualTo(anchor.Z));
            Assert.That(delivery[1].AuthorityProjectionPose.Matches(delivery[1].Point.Skill.Authority.Pose), Is.False);
            Assert.That(preview.Frames.Last().AuthorityProjectionPose.Matches(delivery[1].AuthorityProjectionPose), Is.True);
            var diagnostic = replay.BuildTimeline();
            Assert.That(diagnostic.Frames.Count(frame => frame.Phase == LocalStateSyncReplay.FramePhase.ReplayTick),
                Is.EqualTo(replay.SkillDelivery.Steps.Length));

            var window = ScriptableObject.CreateInstance<BattleScenarioLocalReplayWindow>();
            try
            {
                window.SetAcceptance(replay, "本地测试", autoPreview: true);
                Assert.That(window.IsAutoPlayingPreview, Is.True);
                window.SetAcceptance(replay, "导入检查");
                Assert.That(window.IsAutoPlayingPreview, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

        private static SkillInputEvent LocalCastPress(int tick, long sequence) =>
            new SkillInputEvent(1, SkillInputPhase.Press, targetFrame: tick, inputSequence: sequence);

        private sealed class ProbeCastWorld : ILocalStateSyncCastWorld<int, int>
        {
            private readonly int _failTick;
            private int _state;
            internal ProbeCastWorld(int failTick) => _failTick = failTick;
            internal int PressedTick { get; private set; }
            internal bool Disposed { get; private set; }
            public void Restore(int anchor) => _state = anchor;
            public bool TryStep(int tick, SkillInputEvent? input, out string rejection)
            {
                rejection = null;
                if (input.HasValue)
                {
                    PressedTick = tick;
                    _state -= 10;
                }
                return tick != _failTick;
            }
            public int Read() => _state;
            public void Dispose() => Disposed = true;
        }

        [Test]
        public void StateSyncShowcases_ListOnlyFormalBattleCandidates()
        {
            Assert.That(BattleScenarioEditorIntegration.CaptureStateSyncSkillReplay, Is.Not.Null);
            var showcases = BattleScenarioWindow.StateSyncShowcases;
            Assert.That(showcases.Select(sample => sample.Recipe), Is.EquivalentTo(new[]
            {
                SkillReplayCase.BattleCastMoveWindup, SkillReplayCase.BattleCastMoveRejectBeforeRelease,
                SkillReplayCase.BattleCastWindup, SkillReplayCase.BattleCastConfirmed, SkillReplayCase.BattleCastRejected,
                SkillReplayCase.BattleFlightRestore, SkillReplayCase.BattleFlightReplay, SkillReplayCase.BattleFlightReject,
            }));
            Assert.That(showcases.Select(sample => sample.Label), Is.Unique);
        }

        [TestCase("confirmed", 0)]
        [TestCase("rejected", 30)]
        public void AnnieSkillReplayAssets_LoadFromTheSampleMenuPath(string variant, int replaySteps)
        {
            var path = "Packages/com.abilitykit.battlescenario/Editor/ReplaySamples/" +
                $"annie-w-e-local-skill.{variant}.statesync-replay.json";
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            Assert.That(text, Is.Not.Null);
            var replay = LocalStateSyncReplay.Parse(text.text);
            Assert.That(replay.Variant, Is.EqualTo(variant));
            Assert.That(replay.BuildTimeline().Frames.Count(frame => frame.SkillStep != null), Is.EqualTo(replaySteps));
        }

        [UnityTest]
        public IEnumerator LiveProjectileReplay_ConfirmationReusesHandleUntilNaturalEnd()
        {
            return VerifyLiveProjectileReplay(accepted: true);
        }

        [UnityTest]
        public IEnumerator LiveProjectileReplay_RejectionDestroysOnceAndDoesNotResurrect()
        {
            return VerifyLiveProjectileReplay(accepted: false);
        }

        private static IEnumerator VerifyLiveProjectileReplay(bool accepted)
        {
            var capture = BattleScenarioEditorIntegration.CaptureProjectilePresentationReplay;
            Assert.That(capture, Is.Not.Null, "The Battle Scenario sample menu needs the MOBA capture registration.");
            var captureTask = capture(accepted);
            var deadline = EditorApplication.timeSinceStartup + 195;
            while (!captureTask.IsCompleted && EditorApplication.timeSinceStartup < deadline)
                yield return null;

            Assert.That(captureTask.IsCompleted, Is.True, "The offline projectile capture did not finish in time.");
            var replay = captureTask.GetAwaiter().GetResult();
            Assert.That(replay.Variant, Is.EqualTo(accepted ? "confirmed" : "rejected"));
            var frames = replay.BuildTimeline().Frames;
            Assert.That(frames.Select(frame => frame.Point.DeliveryTick), Is.EqualTo(Enumerable.Range(37, 7)));
            var samples = frames.Select(frame => frame.Point.Presentation).ToArray();
            Assert.That(samples.All(sample => sample.Created == 1), Is.True);
            var handle = samples[0].Handle;
            Assert.That(handle, Is.Not.Null);
            Assert.That(samples[1].Handle, Is.EqualTo(handle));
            Assert.That(samples[1].Destroyed, Is.Zero);

            var decision = samples[2];
            Assert.That(decision.Phase, Is.EqualTo(accepted ? "confirmed" : "rejected"));
            Assert.That(decision.Promoted, Is.EqualTo(accepted));
            Assert.That(decision.DuplicateIgnored, Is.True);
            if (accepted)
            {
                foreach (var sample in samples.Skip(2).Take(4))
                {
                    Assert.That(sample.Handle, Is.EqualTo(handle));
                    Assert.That(sample.Destroyed, Is.Zero);
                    Assert.That(sample.Authority, Is.Not.Null);
                    Assert.That((sample.View.X, sample.View.Y, sample.View.Z),
                        Is.EqualTo((sample.Authority.X, sample.Authority.Y, sample.Authority.Z)));
                }
                Assert.That(samples[6].Phase, Is.EqualTo("ended"));
                AssertRetired(samples[6]);
            }
            else
            {
                AssertRetired(decision);
                foreach (var sample in samples.Skip(3))
                {
                    Assert.That(sample.RejectReason, Is.EqualTo("SupersededPrediction"));
                    AssertRetired(sample);
                }
            }

            static void AssertRetired(LocalStateSyncReplay.PresentationObservation sample)
            {
                Assert.That(sample.Active, Is.Zero);
                Assert.That(sample.Destroyed, Is.EqualTo(1));
                Assert.That(sample.Handle, Is.Null);
                Assert.That(sample.View, Is.Null);
            }
        }

        [Test]
        public void EditorRunner_UsesUnityBackendAndReportsAssertionFailure()
        {
            var scenario = BattleScenarioCompiler.Compile("editor-sync", MobaBattleScenarioDslParser.Parse(@"
sync-backend unity-route
network packet inbound opcode=5202 seq=1 frame=1 at=0
network packet inbound opcode=5202 seq=2 frame=2 at=34
assert-sync finalHash eq 999
"));
            var result = new MobaBattleScenarioRunner().Run(scenario);
            Assert.That(result.Passed, Is.False);
            StringAssert.Contains("syncBackend=unity-route", result.Summary);
            StringAssert.Contains("prediction.finalHash", result.Summary);
        }

        [Test]
        public void EditorRunner_RejectsUnsupportedGameplayInsteadOfPassing()
        {
            var scenario = BattleScenarioCompiler.Compile("unsupported", MobaBattleScenarioDslParser.Parse(@"
sync-backend unity-route
spawn caster hero=1001
"));
            Assert.Throws<NotSupportedException>(() => new MobaBattleScenarioRunner().Run(scenario));
        }

        [Test]
        public void EditorBatch_UnityCasesAndMalformedFilesAreReportedIndividually()
        {
            var directory = Path.Combine(Path.GetTempPath(), "abilitykit-sync-batch-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                BattleScenarioCodec.Save(Path.Combine(directory, "valid.battlescenario"), new BattleScenarioDocument
                {
                    CaseId = "unity-batch",
                    Blocks = new System.Collections.Generic.List<BattleBlock>(
                        MobaBattleScenarioDslParser.Parse("sync-backend unity-route\nassert-sync finalHash eq 0")),
                });
                File.WriteAllText(Path.Combine(directory, "bad.battlescenario"), "not json");
                var report = new MobaBattleScenarioRunner().RunDirectory(directory);
                StringAssert.Contains("total=2, passed=1, failed=1", report);
                StringAssert.Contains("bad: FAILED", report);
                StringAssert.Contains("syncBackend=unity-route", report);
            }
            finally
            {
                File.Delete(Path.Combine(directory, "valid.battlescenario"));
                File.Delete(Path.Combine(directory, "bad.battlescenario"));
                Directory.Delete(directory);
            }
        }
    }

    public sealed class MobaBattleScenarioWorkspaceTests
    {
        [Test]
        public void CaseLibrary_FindsRepositoryScenarioByHeroSlotAndTag()
        {
            var window = ScriptableObject.CreateInstance<BattleScenarioWindow>();
            try
            {
                var matches = window.FilterCases("金克丝 W skill-presentation").Select(entry => Path.GetFileName(entry.Path)).ToArray();
                Assert.That(matches, Is.EqualTo(new[] { "jinx-w.battlescenario" }));
                Assert.That(window.FilterCases("金克丝W jinx-w").Count(), Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

        [Test]
        public void Workspace_RestoresSelectedCaseFromSerializedWindowState()
        {
            var window = ScriptableObject.CreateInstance<BattleScenarioWindow>();
            var restored = ScriptableObject.CreateInstance<BattleScenarioWindow>();
            try
            {
                var path = window.FilterCases("金克丝W jinx-w").Single().Path;
                Assert.IsTrue(window.LoadCaseFromPath(path));
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(window), restored);
                restored.RestoreSelectedCase();
                Assert.IsTrue(restored.TryGetSavedCasePath(out var restoredPath));
                Assert.That(restoredPath, Is.EqualTo(path));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                UnityEngine.Object.DestroyImmediate(restored);
            }
        }

        [Test]
        public void Workspace_DispatchesBothActionsFromSavedCaseAndRejectsDrift()
        {
            var window = ScriptableObject.CreateInstance<BattleScenarioWindow>();
            var oldRunner = BattleScenarioRunnerRegistry.Runner;
            var oldPreview = BattleScenarioEditorIntegration.PlaySavedCase;
            var directory = Path.Combine(Path.GetTempPath(), "abilitykit-workspace-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "case.battlescenario");
            var scenePath = Path.Combine(directory, "arena.battlescene");
            try
            {
                var original = Path.Combine(BattleScenarioEditorIntegration.CaseDirectories.Single(), "SkillPresentation", "jinx-w.battlescenario");
                var document = BattleScenarioCodec.Load(original);
                BattleScenarioCodec.SaveScene(scenePath, new BattleSceneDocument
                {
                    SceneId = "arena",
                    Sections = new BattleScenarioSections { Setup = document.Sections.Setup },
                });
                document.Sections.Setup = new System.Collections.Generic.List<BattleBlock>();
                document.ScenarioRef = "arena.battlescene";
                BattleScenarioCodec.Save(path, document);

                var runner = new RecordingRunner();
                BattleScenarioRunnerRegistry.Runner = runner;
                var previews = new System.Collections.Generic.List<(string Path, bool PreviewOnly)>();
                BattleScenarioEditorIntegration.PlaySavedCase = (file, previewOnly) => { previews.Add((file, previewOnly)); return true; };

                Assert.IsTrue(window.LoadCaseFromPath(path));
                window.RunSavedAssertions();
                window.PlaySavedCase(previewOnly: true);
                window.PlaySavedCase(previewOnly: false);
                Assert.That(runner.Calls, Is.EqualTo(1));
                Assert.That(runner.Scenario?.CaseId, Is.EqualTo("jinx-w"));
                Assert.That(runner.Scenario?.Actors.Count, Is.EqualTo(2));
                Assert.That(previews, Is.EqualTo(new[] { (path, true), (path, false) }));

                typeof(BattleScenarioWindow).GetField("_displayName", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(window, "未保存的编辑");
                Assert.That(window.TryGetSavedCasePath(out _), Is.False);
                window.RunSavedAssertions();
                window.PlaySavedCase(previewOnly: true);
                Assert.That(runner.Calls, Is.EqualTo(1));
                Assert.That(previews.Count, Is.EqualTo(2));

                Assert.IsTrue(window.LoadCaseFromPath(path));
                File.AppendAllText(scenePath, "\n");
                Assert.That(window.TryGetSavedCasePath(out _), Is.False);
                Assert.IsTrue(window.LoadCaseFromPath(path));
                Assert.IsTrue(window.TryGetSavedCasePath(out _));
                File.AppendAllText(path, "\n");
                Assert.That(window.TryGetSavedCasePath(out _), Is.False);
            }
            finally
            {
                BattleScenarioRunnerRegistry.Runner = oldRunner;
                BattleScenarioEditorIntegration.PlaySavedCase = oldPreview;
                UnityEngine.Object.DestroyImmediate(window);
                File.Delete(path);
                File.Delete(scenePath);
                Directory.Delete(directory);
            }
        }

        private sealed class RecordingRunner : IBattleScenarioRunner
        {
            public int Calls;
            public TestScenario Scenario;

            public BattleScenarioRunResult Run(TestScenario scenario)
            {
                Calls++;
                Scenario = scenario;
                return new BattleScenarioRunResult { Passed = true };
            }
        }
    }
}
