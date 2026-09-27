using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.StateSync.Prediction;
using AbilityKit.Combat.Collision;
using AbilityKit.Combat.MotionSystem.Collision;
using AbilityKit.Combat.MotionSystem.Constraints;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.Share.Prediction;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ConflictLevel = AbilityKit.Ability.StateSync.ConflictLevel;
using StateSyncVector3 = AbilityKit.Ability.StateSync.Vector3;

namespace AbilityKit.Game.Flow
{
    public sealed class StateSyncPredictionPlayModeSmokeTests
    {
        private const int LocalActorId = 7;
        private const float FrameTime = 1f / 30f;
        private const float VisualScale = 7f;

        [UnityTest]
        public IEnumerator WallCorrection_ReplaysInputs_AndSmoothsARealUnityTransform()
        {
            Assert.IsTrue(Application.isPlaying, "This evidence test must execute in Unity PlayMode.");

            var created = new List<UnityEngine.Object>();
            RenderTexture renderTexture = null;
            Texture2D screenshot = null;
            try
            {
                var conflict = ConflictLevel.None;
                using (var coordinator = new PredictionCoordinator(LocalActorId))
                {
                    coordinator.Register(new MobaMovementPredictionHandler(FrameTime));
                    coordinator.OnRollbackExecuted += (_, level) => conflict = level;

                    var slots = coordinator.GetCurrentSlots();
                    slots.Set(MobaPredictionSlotNames.Position, StateSyncVector3.Zero);
                    slots.Set(MobaPredictionSlotNames.Velocity, StateSyncVector3.Zero);

                    var move = new MobaMovePredictionInput(5f, 0f, Mathf.PI * 0.5f);
                    for (var i = 0; i < 4; i++) coordinator.ProcessInput(move);
                    var predicted = slots.GetPosition(MobaPredictionSlotNames.Position);

                    var collisionWorld = new GridCollisionWorld(cellSize: 1f, initialCapacity: 8);
                    AddWall(collisionWorld, new Vec3(0.24f, 0f, 0f), new Vec3(0.04f, 1f, 2f));
                    var motionWorld = new MobaMotionCollisionWorldAdapter(collisionWorld, null);
                    var solver = new ConfigurableMotionSolver(
                        motionWorld,
                        ResolveDisabledConstraints);
                    var constraints = new MotionCollisionConstraints(
                        enable: true,
                        allowPassThrough: false,
                        endOverlapPolicy: MotionEndOverlapPolicy.ClampToLastValid,
                        radius: 0.05f,
                        skin: 0.01f,
                        obstacleMask: MobaCollisionLayers.WorldMask,
                        ignoreMask: 0);

                    var authority = Vec3.Zero;
                    var collisionCount = 0;
                    for (var frame = 1; frame <= 2; frame++)
                    {
                        var result = solver.Resolve(
                            LocalActorId,
                            authority,
                            new Vec3(5f * FrameTime, 0f, 0f),
                            constraints);
                        authority += result.AppliedDelta;
                        if (result.Hit.Hit) collisionCount++;
                    }

                    var authoritativeSlots = new StateSlots();
                    authoritativeSlots.Set(
                        MobaPredictionSlotNames.Position,
                        new StateSyncVector3(authority.X, authority.Y, authority.Z));
                    authoritativeSlots.Set(MobaPredictionSlotNames.Velocity, StateSyncVector3.Zero);
                    coordinator.ApplyServerSnapshot(2, LocalActorId, authoritativeSlots);
                    var reconciled = slots.GetPosition(MobaPredictionSlotNames.Position);

                    var presentationObject = CreateMarker(
                        "PresentationMarker",
                        Color.cyan,
                        Vector3.zero,
                        created);
                    var viewHandle = new BattleViewHandle { GameObject = presentationObject };
                    var emptyVfxEntity = default(AbilityKit.World.ECS.IEntity);
                    var attachedVfx = new BattleViewAttachedVfxController(null, in emptyVfxEntity);
                    var applier = new BattleViewPositionApplier(null, attachedVfx) { SmoothingHz = 12f };
                    applier.ApplyPendingPositionsForTest(
                        viewHandle,
                        new Vector3(predicted.X, predicted.Y, predicted.Z),
                        0f);
                    applier.ApplyPendingPositionsForTest(
                        viewHandle,
                        new Vector3(reconciled.X, reconciled.Y, reconciled.Z),
                        FrameTime);
                    var presentationX = presentationObject.transform.position.x;

                    Assert.Greater(collisionCount, 0);
                    Assert.AreNotEqual(ConflictLevel.None, conflict);
                    Assert.AreEqual(2, coordinator.ConfirmedFrame.Value);
                    Assert.AreEqual(4, coordinator.CurrentFrame.Value);
                    Assert.Greater(predicted.X, reconciled.X);
                    Assert.Greater(reconciled.X, authority.X);
                    Assert.Greater(presentationX, reconciled.X);
                    Assert.Less(presentationX, predicted.X);

                    BuildEvidenceScene(
                        predicted.X,
                        authority.X,
                        reconciled.X,
                        presentationX,
                        presentationObject,
                        created);
                    yield return null;

                    var outputDirectory = GetOutputDirectory();
                    Directory.CreateDirectory(outputDirectory);
                    var pngPath = Path.Combine(outputDirectory, "wall-correction.png");
                    var jsonPath = Path.Combine(outputDirectory, "wall-correction.json");
                    screenshot = CaptureEvidence(created, out renderTexture);
                    AssertNonBlank(screenshot);
                    File.WriteAllBytes(pngPath, screenshot.EncodeToPNG());

                    var artifact = new WallCorrectionArtifact
                    {
                        evidenceLevel = "E4 Unity PlayMode smoke (not networked)",
                        predictedFrame = 4,
                        confirmedFrame = 2,
                        replayedFrames = 2,
                        predictedX = predicted.X,
                        authorityX = authority.X,
                        reconciledLogicX = reconciled.X,
                        smoothedPresentationX = presentationX,
                        collisionCount = collisionCount,
                        conflictLevel = conflict.ToString(),
                        screenshot = pngPath.Replace('\\', '/'),
                    };
                    File.WriteAllText(jsonPath, JsonUtility.ToJson(artifact, true));

                    Assert.IsTrue(File.Exists(pngPath));
                    Assert.IsTrue(File.Exists(jsonPath));
                    Debug.Log($"[StateSyncPredictionPlayMode] artifact={jsonPath}; screenshot={pngPath}");
                }
            }
            finally
            {
                if (RenderTexture.active == renderTexture) RenderTexture.active = null;
                if (screenshot != null) UnityEngine.Object.DestroyImmediate(screenshot);
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }
                for (var i = created.Count - 1; i >= 0; i--)
                {
                    if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
                }
            }
        }

        [UnityTest]
        public IEnumerator PredictedCast_ConfirmationPromotes_AndRejectionDestroysTransientObjects()
        {
            Assert.IsTrue(Application.isPlaying, "This evidence test must execute in Unity PlayMode.");

            var created = new List<UnityEngine.Object>();
            RenderTexture renderTexture = null;
            Texture2D screenshot = null;
            try
            {
                var resolver = new BattlePresentationCueResolver();
                var tracker = new BattlePresentationCueReconciliationTracker(capacity: 8);
                var presenter = new CueGameObjectPresenter(created);

                var acceptedPredicted = CreateCastCue(
                    "client-cast-q",
                    predictionKey: 87,
                    PresentationCuePredictionState.Predicted,
                    confirmedFrame: 0);
                var acceptedKey = BattlePresentationCueRequestKey.From(in acceptedPredicted);
                Assert.IsTrue(tracker.Accept(acceptedKey, in acceptedPredicted));
                var acceptedPredictionDecision = resolver.Resolve(in acceptedPredicted);
                presenter.Apply(in acceptedPredictionDecision, acceptedPredicted.PredictionState, new Vector3(-2.25f, 0.45f, 0f));
                Assert.IsTrue(presenter.TryGet(acceptedKey, out var acceptedObject));
                var acceptedInstanceId = acceptedObject.GetInstanceID();

                var acceptedAuthority = CreateCastCue(
                    "server-cast-q",
                    predictionKey: 87,
                    PresentationCuePredictionState.ServerConfirmed,
                    confirmedFrame: 121);
                Assert.IsTrue(tracker.Accept(acceptedKey, in acceptedAuthority));
                var acceptedAuthorityDecision = resolver.Resolve(in acceptedAuthority);
                presenter.Apply(in acceptedAuthorityDecision, acceptedAuthority.PredictionState, new Vector3(-2.25f, 0.45f, 0f));
                Assert.IsTrue(presenter.TryGet(acceptedKey, out var promotedObject));
                Assert.AreEqual(acceptedInstanceId, promotedObject.GetInstanceID());

                var rejectedPredicted = CreateCastCue(
                    "client-cast-w",
                    predictionKey: 88,
                    PresentationCuePredictionState.Predicted,
                    confirmedFrame: 0);
                var rejectedKey = BattlePresentationCueRequestKey.From(in rejectedPredicted);
                Assert.IsTrue(tracker.Accept(rejectedKey, in rejectedPredicted));
                var rejectedPredictionDecision = resolver.Resolve(in rejectedPredicted);
                presenter.Apply(in rejectedPredictionDecision, rejectedPredicted.PredictionState, new Vector3(2.25f, 0.45f, 0f));
                Assert.IsTrue(presenter.TryGet(rejectedKey, out var rejectedObject));
                var rejectedInstanceId = rejectedObject.GetInstanceID();

                var rejectedAuthority = CreateCastCue(
                    "server-cast-w",
                    predictionKey: 88,
                    PresentationCuePredictionState.Rejected,
                    confirmedFrame: 121);
                Assert.IsTrue(tracker.Accept(rejectedKey, in rejectedAuthority));
                var rejectedAuthorityDecision = resolver.Resolve(in rejectedAuthority);
                Assert.AreEqual(BattlePresentationCueDecisionKind.Stop, rejectedAuthorityDecision.Kind);
                presenter.Apply(in rejectedAuthorityDecision, rejectedAuthority.PredictionState, new Vector3(2.25f, 0.45f, 0f));
                Assert.IsFalse(presenter.TryGet(rejectedKey, out _));
                Assert.IsTrue(rejectedObject == null, "Rejected transient GameObject must be destroyed.");
                Assert.AreEqual(1L, tracker.ConfirmedCount);
                Assert.AreEqual(1L, tracker.RejectedCount);

                BuildCastEvidenceScene(created);
                yield return null;

                var outputDirectory = GetOutputDirectory();
                Directory.CreateDirectory(outputDirectory);
                var pngPath = Path.Combine(outputDirectory, "cast-confirm-reject.png");
                var jsonPath = Path.Combine(outputDirectory, "cast-confirm-reject.json");
                screenshot = CaptureEvidence(created, out renderTexture);
                AssertNonBlank(screenshot);
                File.WriteAllBytes(pngPath, screenshot.EncodeToPNG());

                var artifact = new CastReconciliationArtifact
                {
                    evidenceLevel = "E4 Unity PlayMode cue lifecycle smoke (not full skill phase/network)",
                    predictionKeyAccepted = 87,
                    acceptedSameInstance = acceptedInstanceId == promotedObject.GetInstanceID(),
                    acceptedInstanceId = acceptedInstanceId,
                    predictionKeyRejected = 88,
                    rejectedDestroyed = rejectedObject == null,
                    rejectedInstanceId = rejectedInstanceId,
                    confirmedCount = tracker.ConfirmedCount,
                    rejectedCount = tracker.RejectedCount,
                    screenshot = pngPath.Replace('\\', '/'),
                };
                File.WriteAllText(jsonPath, JsonUtility.ToJson(artifact, true));

                Assert.IsTrue(File.Exists(pngPath));
                Assert.IsTrue(File.Exists(jsonPath));
                Debug.Log($"[StateSyncPredictionPlayMode] artifact={jsonPath}; screenshot={pngPath}");
            }
            finally
            {
                if (RenderTexture.active == renderTexture) RenderTexture.active = null;
                if (screenshot != null) UnityEngine.Object.DestroyImmediate(screenshot);
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }
                for (var i = created.Count - 1; i >= 0; i--)
                {
                    if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
                }
            }
        }

        [UnityTest]
        public IEnumerator SkillTransaction_ConfirmationCalibratesPhase_AndRejectionRestoresState()
        {
            Assert.IsTrue(Application.isPlaying, "This evidence test must execute in Unity PlayMode.");

            var created = new List<UnityEngine.Object>();
            RenderTexture renderTexture = null;
            Texture2D screenshot = null;
            try
            {
                var resolver = new BattlePresentationCueResolver();
                var tracker = new BattlePresentationCueReconciliationTracker(capacity: 8);
                var presenter = new CueGameObjectPresenter(created);

                float predictedPlayback;
                float confirmedPlayback;
                int acceptedInstanceId;
                GameObject acceptedObject;
                using (var accepted = CreateSkillCoordinator(100f))
                {
                    accepted.ProcessInput(new MobaSkillPredictionInput(
                        predictionKey: 87,
                        skillId: 6001,
                        predictedPhase: 1,
                        cooldownFrames: 150,
                        resourceCost: 20f,
                        inputLockMask: 3,
                        movementLocked: true));
                    var slots = accepted.GetCurrentSlots();
                    Assert.AreEqual(1, slots.GetInt(MobaPredictionSlotNames.SkillPhase));
                    Assert.AreEqual(20f, slots.GetFloat(MobaPredictionSlotNames.SkillReservedResource));
                    Assert.AreEqual(80f, slots.GetFloat(MobaPredictionSlotNames.Mana));
                    Assert.AreEqual(3, slots.GetInt(MobaPredictionSlotNames.SkillInputLockMask));
                    Assert.IsTrue(slots.GetBool(MobaPredictionSlotNames.SkillMovementLocked));

                    var predictedCue = CreateCastCue("client-cast-q", 87, PresentationCuePredictionState.Predicted, 0);
                    var acceptedKey = BattlePresentationCueRequestKey.From(in predictedCue);
                    Assert.IsTrue(tracker.Accept(acceptedKey, in predictedCue));
                    var predictedDecision = resolver.Resolve(in predictedCue);
                    presenter.Apply(in predictedDecision, predictedCue.PredictionState, new Vector3(-2.25f, 0.45f, 0f));
                    Assert.IsTrue(presenter.TryGet(acceptedKey, out acceptedObject));
                    acceptedInstanceId = acceptedObject.GetInstanceID();
                    var animation = CreatePhaseAnimation(acceptedObject, created);
                    predictedPlayback = SamplePhase(animation, 4f * FrameTime);

                    accepted.ProcessInputs(Array.Empty<AbilityKit.Ability.StateSync.IInputCommand>());
                    accepted.ApplyServerSnapshot(2, LocalActorId, CreateSkillAuthorityState(
                        87, 6001, 2, 2, 152, 0f, 80f, 1, false));

                    Assert.AreEqual(2, slots.GetInt(MobaPredictionSlotNames.SkillPhase));
                    Assert.AreEqual(2, slots.GetInt(MobaPredictionSlotNames.SkillPhaseStartFrame));
                    Assert.AreEqual(152, slots.GetInt(MobaPredictionSlotNames.SkillCooldownEndFrame));
                    Assert.AreEqual(0f, slots.GetFloat(MobaPredictionSlotNames.SkillReservedResource));
                    Assert.AreEqual(1, slots.GetInt(MobaPredictionSlotNames.SkillInputLockMask));
                    Assert.IsFalse(slots.GetBool(MobaPredictionSlotNames.SkillMovementLocked));

                    var confirmedCue = CreateCastCue("server-cast-q", 87, PresentationCuePredictionState.ServerConfirmed, 2);
                    Assert.IsTrue(tracker.Accept(acceptedKey, in confirmedCue));
                    var confirmedDecision = resolver.Resolve(in confirmedCue);
                    presenter.Apply(in confirmedDecision, confirmedCue.PredictionState, new Vector3(-2.25f, 0.45f, 0f));
                    Assert.IsTrue(presenter.TryGet(acceptedKey, out var promotedObject));
                    Assert.AreEqual(acceptedInstanceId, promotedObject.GetInstanceID());
                    confirmedPlayback = SamplePhase(
                        animation,
                        (14 - slots.GetInt(MobaPredictionSlotNames.SkillPhaseStartFrame)) * FrameTime);
                    Assert.Greater(confirmedPlayback, predictedPlayback);
                }

                var rejectedBlendFrames = 0;
                var rejectedPredictedMana = 0f;
                GameObject rejectedObject;
                using (var rejected = CreateSkillCoordinator(100f))
                {
                    rejected.ProcessInput(new MobaSkillPredictionInput(
                        predictionKey: 88,
                        skillId: 6002,
                        predictedPhase: 1,
                        cooldownFrames: 240,
                        resourceCost: 35f,
                        inputLockMask: 7,
                        movementLocked: true));
                    var slots = rejected.GetCurrentSlots();
                    rejectedPredictedMana = slots.GetFloat(MobaPredictionSlotNames.Mana);

                    var predictedCue = CreateCastCue("client-cast-w", 88, PresentationCuePredictionState.Predicted, 0);
                    var rejectedKey = BattlePresentationCueRequestKey.From(in predictedCue);
                    Assert.IsTrue(tracker.Accept(rejectedKey, in predictedCue));
                    var predictedDecision = resolver.Resolve(in predictedCue);
                    presenter.Apply(in predictedDecision, predictedCue.PredictionState, new Vector3(2.25f, 0.45f, 0f));
                    Assert.IsTrue(presenter.TryGet(rejectedKey, out rejectedObject));

                    rejected.ProcessInputs(Array.Empty<AbilityKit.Ability.StateSync.IInputCommand>());
                    rejected.ApplyServerSnapshot(2, LocalActorId, CreateSkillAuthorityState(
                        88, 6002, 0, 0, 0, 0f, 100f, 0, false));

                    Assert.AreEqual(0, slots.GetInt(MobaPredictionSlotNames.SkillPhase));
                    Assert.AreEqual(0, slots.GetInt(MobaPredictionSlotNames.SkillCooldownEndFrame));
                    Assert.AreEqual(0f, slots.GetFloat(MobaPredictionSlotNames.SkillReservedResource));
                    Assert.AreEqual(100f, slots.GetFloat(MobaPredictionSlotNames.Mana));
                    Assert.AreEqual(0, slots.GetInt(MobaPredictionSlotNames.SkillInputLockMask));
                    Assert.IsFalse(slots.GetBool(MobaPredictionSlotNames.SkillMovementLocked));

                    var rejectedCue = CreateCastCue("server-cast-w", 88, PresentationCuePredictionState.Rejected, 2);
                    Assert.IsTrue(tracker.Accept(rejectedKey, in rejectedCue));
                    var rejectedDecision = resolver.Resolve(in rejectedCue);
                    Assert.AreEqual(BattlePresentationCueDecisionKind.Stop, rejectedDecision.Kind);

                    var initialScale = rejectedObject.transform.localScale;
                    for (var frame = 1; frame <= 3; frame++)
                    {
                        rejectedObject.transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, frame / 3f);
                        rejectedBlendFrames++;
                        yield return null;
                    }
                    presenter.Apply(in rejectedDecision, rejectedCue.PredictionState, new Vector3(2.25f, 0.45f, 0f));
                    Assert.IsFalse(presenter.TryGet(rejectedKey, out _));
                    Assert.IsTrue(rejectedObject == null, "Rejected VFX must be released after blend out.");
                }

                BuildSkillTransactionEvidenceScene(
                    predictedPlayback,
                    confirmedPlayback,
                    rejectedPredictedMana,
                    rejectedBlendFrames,
                    acceptedObject,
                    created);
                yield return null;

                var outputDirectory = GetOutputDirectory();
                Directory.CreateDirectory(outputDirectory);
                var pngPath = Path.Combine(outputDirectory, "skill-transaction.png");
                var jsonPath = Path.Combine(outputDirectory, "skill-transaction.json");
                screenshot = CaptureEvidence(created, out renderTexture);
                AssertNonBlank(screenshot);
                File.WriteAllBytes(pngPath, screenshot.EncodeToPNG());

                var artifact = new SkillTransactionArtifact
                {
                    evidenceLevel = "E4 Unity PlayMode local skill transaction (not networked/production VFX)",
                    predictionKeyAccepted = 87,
                    acceptedSameInstance = acceptedObject != null && acceptedObject.GetInstanceID() == acceptedInstanceId,
                    predictedPhase = 1,
                    authoritativePhase = 2,
                    predictedPlayback = predictedPlayback,
                    calibratedPlayback = confirmedPlayback,
                    acceptedMana = 80f,
                    acceptedCooldownEndFrame = 152,
                    acceptedInputLockMask = 1,
                    predictionKeyRejected = 88,
                    rejectedPredictedMana = rejectedPredictedMana,
                    rejectedRestoredMana = 100f,
                    rejectedCooldownEndFrame = 0,
                    rejectedInputLockMask = 0,
                    rejectedBlendFrames = rejectedBlendFrames,
                    rejectedVfxDestroyed = rejectedObject == null,
                    screenshot = pngPath.Replace('\\', '/'),
                };
                File.WriteAllText(jsonPath, JsonUtility.ToJson(artifact, true));

                Assert.IsTrue(File.Exists(pngPath));
                Assert.IsTrue(File.Exists(jsonPath));
                Debug.Log($"[StateSyncPredictionPlayMode] artifact={jsonPath}; screenshot={pngPath}");
            }
            finally
            {
                if (RenderTexture.active == renderTexture) RenderTexture.active = null;
                if (screenshot != null) UnityEngine.Object.DestroyImmediate(screenshot);
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }
                for (var i = created.Count - 1; i >= 0; i--)
                {
                    if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
                }
            }
        }

        private static PresentationCueData CreateCastCue(
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

        private static void BuildCastEvidenceScene(List<UnityEngine.Object> created)
        {
            CreateCube(
                "AcceptedPanel",
                new Color(0.08f, 0.16f, 0.13f),
                new Vector3(-2.6f, -0.12f, 0f),
                new Vector3(4.6f, 0.15f, 5.6f),
                created);
            CreateCube(
                "RejectedPanel",
                new Color(0.18f, 0.09f, 0.10f),
                new Vector3(2.6f, -0.12f, 0f),
                new Vector3(4.6f, 0.15f, 5.6f),
                created);
            CreateLabel("P87 CONFIRMED", new Vector3(-4.55f, 0.08f, 2.25f), created, 0.06f, Color.white);
            CreateLabel("SAME INSTANCE", new Vector3(-4.55f, 0.08f, -2.35f), created, 0.05f, new Color(0.45f, 1f, 0.58f));
            CreateLabel("P88 REJECTED", new Vector3(0.65f, 0.08f, 2.25f), created, 0.06f, Color.white);
            CreateLabel("TRANSIENT RELEASED", new Vector3(0.65f, 0.08f, -2.35f), created, 0.05f, new Color(1f, 0.42f, 0.4f));
            CreateLabel("PREDICTED CAST CUE RECONCILIATION", new Vector3(-4.55f, 0.08f, 4.25f), created, 0.085f, Color.white);

            var slashA = CreateCube(
                "RejectedSlashA",
                new Color(1f, 0.25f, 0.23f),
                new Vector3(2.25f, 0.25f, 0f),
                new Vector3(2.1f, 0.22f, 0.28f),
                created);
            slashA.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            var slashB = CreateCube(
                "RejectedSlashB",
                new Color(1f, 0.25f, 0.23f),
                new Vector3(2.25f, 0.25f, 0f),
                new Vector3(2.1f, 0.22f, 0.28f),
                created);
            slashB.transform.rotation = Quaternion.Euler(0f, -45f, 0f);
        }

        private static PredictionCoordinator CreateSkillCoordinator(float initialMana)
        {
            var coordinator = new PredictionCoordinator(LocalActorId);
            coordinator.Register(new MobaSkillPredictionHandler());
            coordinator.GetCurrentSlots().Set(MobaPredictionSlotNames.Mana, initialMana);
            return coordinator;
        }

        private static StateSlots CreateSkillAuthorityState(
            int predictionKey,
            int skillId,
            int phase,
            int phaseStartFrame,
            int cooldownEndFrame,
            float reservedResource,
            float mana,
            int inputLockMask,
            bool movementLocked)
        {
            var slots = new StateSlots();
            slots.Set(MobaPredictionSlotNames.SkillPredictionKey, predictionKey);
            slots.Set(MobaPredictionSlotNames.SkillId, skillId);
            slots.Set(MobaPredictionSlotNames.SkillPhase, phase);
            slots.Set(MobaPredictionSlotNames.SkillPhaseStartFrame, phaseStartFrame);
            slots.Set(MobaPredictionSlotNames.SkillCooldownEndFrame, cooldownEndFrame);
            slots.Set(MobaPredictionSlotNames.SkillReservedResource, reservedResource);
            slots.Set(MobaPredictionSlotNames.Mana, mana);
            slots.Set(MobaPredictionSlotNames.SkillInputLockMask, inputLockMask);
            slots.Set(MobaPredictionSlotNames.SkillMovementLocked, movementLocked);
            return slots;
        }

        private static UnityEngine.Animation CreatePhaseAnimation(
            GameObject target,
            List<UnityEngine.Object> created)
        {
            var clip = new AnimationClip { name = "SkillPhase", legacy = true };
            clip.SetCurve("", typeof(Transform), "localScale.x", AnimationCurve.Linear(0f, 0.7f, 1f, 1.15f));
            clip.SetCurve("", typeof(Transform), "localScale.y", AnimationCurve.Linear(0f, 0.7f, 1f, 1.15f));
            clip.SetCurve("", typeof(Transform), "localScale.z", AnimationCurve.Linear(0f, 0.7f, 1f, 1.15f));
            var animation = target.AddComponent<UnityEngine.Animation>();
            animation.AddClip(clip, clip.name);
            animation.clip = clip;
            animation.Play(clip.name);
            created.Add(clip);
            return animation;
        }

        private static float SamplePhase(UnityEngine.Animation animation, float seconds)
        {
            var state = animation["SkillPhase"];
            Assert.IsNotNull(state);
            state.time = seconds;
            animation.Sample();
            return state.normalizedTime;
        }

        private static void BuildSkillTransactionEvidenceScene(
            float predictedPlayback,
            float confirmedPlayback,
            float rejectedPredictedMana,
            int rejectedBlendFrames,
            GameObject acceptedObject,
            List<UnityEngine.Object> created)
        {
            CreateCube("AcceptedPanel", new Color(0.07f, 0.16f, 0.13f), new Vector3(-2.6f, -0.12f, 0f), new Vector3(4.7f, 0.15f, 5.8f), created);
            CreateCube("RejectedPanel", new Color(0.18f, 0.08f, 0.10f), new Vector3(2.6f, -0.12f, 0f), new Vector3(4.7f, 0.15f, 5.8f), created);
            CreateLabel("SKILL PREDICTION TRANSACTION / AUTHORITY RECONCILIATION", new Vector3(-4.75f, 0.08f, 4.25f), created, 0.058f, Color.white);

            CreateLabel("P87  CONFIRMED", new Vector3(-4.55f, 0.08f, 2.35f), created, 0.06f, Color.white);
            CreateLabel("PHASE  1 -> 2", new Vector3(-4.55f, 0.08f, 1.55f), created, 0.052f, new Color(0.45f, 1f, 0.58f));
            CreateLabel($"PLAYHEAD  {predictedPlayback:F2} -> {confirmedPlayback:F2}", new Vector3(-4.55f, 0.08f, 0.75f), created, 0.046f);
            CreateLabel("MANA 100 -> 80   CD END F152", new Vector3(-4.55f, 0.08f, -0.05f), created, 0.043f);
            CreateLabel("LOCK 3 -> 1   MOVE RELEASED", new Vector3(-4.55f, 0.08f, -0.85f), created, 0.043f);
            CreateLabel("SAME VFX INSTANCE", new Vector3(-4.55f, 0.08f, -2.6f), created, 0.05f, new Color(0.45f, 1f, 0.58f));

            CreateLabel("P88  REJECTED", new Vector3(0.65f, 0.08f, 2.35f), created, 0.06f, Color.white);
            CreateLabel("PHASE  1 -> IDLE", new Vector3(0.65f, 0.08f, 1.55f), created, 0.052f, new Color(1f, 0.45f, 0.42f));
            CreateLabel($"MANA {rejectedPredictedMana:F0} -> 100", new Vector3(0.65f, 0.08f, 0.75f), created, 0.046f);
            CreateLabel("CD CLEARED   LOCK RELEASED", new Vector3(0.65f, 0.08f, -0.05f), created, 0.043f);
            CreateLabel($"VFX BLEND OUT  {rejectedBlendFrames} FRAMES", new Vector3(0.65f, 0.08f, -0.85f), created, 0.043f);
            CreateLabel("TRANSIENT DESTROYED", new Vector3(0.65f, 0.08f, -2.6f), created, 0.05f, new Color(1f, 0.45f, 0.42f));

            acceptedObject.transform.position = new Vector3(-2.25f, 0.45f, -1.55f);
            var slashA = CreateCube("RejectedSlashA", new Color(1f, 0.25f, 0.23f), new Vector3(2.25f, 0.25f, -1.55f), new Vector3(1.6f, 0.2f, 0.25f), created);
            slashA.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            var slashB = CreateCube("RejectedSlashB", new Color(1f, 0.25f, 0.23f), new Vector3(2.25f, 0.25f, -1.55f), new Vector3(1.6f, 0.2f, 0.25f), created);
            slashB.transform.rotation = Quaternion.Euler(0f, -45f, 0f);
        }

        private static void BuildEvidenceScene(
            float predictedX,
            float authorityX,
            float reconciledX,
            float presentationX,
            GameObject presentationObject,
            List<UnityEngine.Object> created)
        {
            var laneZ = new[] { 3f, 1f, -1f, -3f };
            var labels = new[]
            {
                $"F4 LOCAL PREDICTION   x={predictedX:F3}",
                $"F2 AUTHORITY HIT     x={authorityX:F3}",
                $"F3-F4 INPUT REPLAY   x={reconciledX:F3}",
                $"RENDER SMOOTHING     x={presentationX:F3}",
            };

            for (var i = 0; i < laneZ.Length; i++)
            {
                CreateCube(
                    $"Lane{i}",
                    new Color(0.10f, 0.13f, 0.16f),
                    new Vector3(0f, -0.12f, laneZ[i]),
                    new Vector3(10f, 0.15f, 1.45f),
                    created);
                CreateCube(
                    $"Wall{i}",
                    new Color(0.95f, 0.55f, 0.12f),
                    new Vector3(ToVisualX(0.24f), 0.45f, laneZ[i]),
                    new Vector3(0.42f, 0.9f, 1.2f),
                    created);
                CreateLabel(labels[i], new Vector3(-4.65f, 0.08f, laneZ[i] + 0.45f), created);
            }

            CreateMarker("Predicted", new Color(1f, 0.25f, 0.23f), new Vector3(ToVisualX(predictedX), 0.45f, laneZ[0]), created);
            CreateMarker("Authority", new Color(1f, 0.9f, 0.22f), new Vector3(ToVisualX(authorityX), 0.45f, laneZ[1]), created);
            CreateMarker("Replayed", new Color(0.3f, 1f, 0.45f), new Vector3(ToVisualX(reconciledX), 0.45f, laneZ[2]), created);

            presentationObject.transform.position = new Vector3(ToVisualX(presentationX), 0.45f, laneZ[3]);
            presentationObject.transform.localScale = Vector3.one * 0.55f;
            CreateMarker(
                "CorrectedLogicGhost",
                new Color(0.3f, 1f, 0.45f, 0.45f),
                new Vector3(ToVisualX(reconciledX), 0.25f, laneZ[3]),
                created,
                0.34f);

            CreateLabel(
                "STATE SYNC: WALL CORRECTION / REPLAY / SMOOTHING",
                new Vector3(-4.65f, 0.08f, 4.25f),
                created,
                0.075f,
                Color.white);
        }

        private static Texture2D CaptureEvidence(
            List<UnityEngine.Object> created,
            out RenderTexture renderTexture)
        {
            var cameraObject = new GameObject("EvidenceCamera");
            created.Add(cameraObject);
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 12f, 0f);
            cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = 5.2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.055f);
            camera.allowHDR = false;
            camera.allowMSAA = false;

            renderTexture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            renderTexture.Create();
            camera.targetTexture = renderTexture;
            camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
            texture.Apply(false, false);
            RenderTexture.active = previous;
            camera.targetTexture = null;
            return texture;
        }

        private static void AssertNonBlank(Texture2D texture)
        {
            var pixels = texture.GetPixels32();
            var first = pixels[0];
            var differentPixels = 0;
            for (var i = 1; i < pixels.Length; i += 97)
            {
                var pixel = pixels[i];
                if (Math.Abs(pixel.r - first.r) + Math.Abs(pixel.g - first.g) + Math.Abs(pixel.b - first.b) > 12)
                {
                    differentPixels++;
                }
            }
            Assert.Greater(differentPixels, 100, "The rendered evidence image is blank or nearly uniform.");
        }

        private static GameObject CreateMarker(
            string name,
            Color color,
            Vector3 position,
            List<UnityEngine.Object> created,
            float scale = 0.55f)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            marker.name = name;
            marker.transform.position = position;
            marker.transform.localScale = new Vector3(scale, scale, scale);
            ApplyColor(marker, color, created);
            created.Add(marker);
            return marker;
        }

        private static GameObject CreateCube(
            string name,
            Color color,
            Vector3 position,
            Vector3 scale,
            List<UnityEngine.Object> created)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = position;
            cube.transform.localScale = scale;
            ApplyColor(cube, color, created);
            created.Add(cube);
            return cube;
        }

        private static void ApplyColor(GameObject gameObject, Color color, List<UnityEngine.Object> created)
        {
            var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            Assert.IsNotNull(shader, "A color-capable shader is required for the evidence render.");
            var material = new Material(shader) { color = color };
            gameObject.GetComponent<Renderer>().sharedMaterial = material;
            created.Add(material);
        }

        private static void CreateLabel(
            string text,
            Vector3 position,
            List<UnityEngine.Object> created,
            float size = 0.07f,
            Color? color = null)
        {
            var labelObject = new GameObject("Label");
            labelObject.transform.position = position;
            labelObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.fontSize = 64;
            label.characterSize = size;
            label.anchor = TextAnchor.LowerLeft;
            label.alignment = TextAlignment.Left;
            label.color = color ?? new Color(0.82f, 0.87f, 0.91f);
            created.Add(labelObject);
        }

        private static float ToVisualX(float logicX)
        {
            return -1.25f + logicX * VisualScale;
        }

        private static ColliderId AddWall(ICollisionWorld world, in Vec3 center, in Vec3 halfExtents)
        {
            var transform = new Transform3(center, Quat.Identity, Vec3.One);
            var shape = ColliderShape.CreateAabb(-halfExtents, halfExtents);
            return world.Add(in transform, in shape, MobaCollisionLayers.WorldId);
        }

        private static MotionConstraints ResolveDisabledConstraints(
            int moverId,
            in MotionState state,
            in MotionOutput input,
            float deltaTime)
        {
            return MotionConstraints.Disabled;
        }

        private static string GetOutputDirectory()
        {
            var repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            return Path.Combine(repositoryRoot, "local", "Logs", "state-sync-prediction-unity-smoke");
        }

        private sealed class CueGameObjectPresenter
        {
            private readonly List<UnityEngine.Object> _created;
            private readonly Dictionary<BattlePresentationCueRequestKey, GameObject> _active =
                new Dictionary<BattlePresentationCueRequestKey, GameObject>();

            public CueGameObjectPresenter(List<UnityEngine.Object> created)
            {
                _created = created;
            }

            public void Apply(
                in BattlePresentationCueDecision decision,
                PresentationCuePredictionState predictionState,
                Vector3 position)
            {
                if (decision.Kind == BattlePresentationCueDecisionKind.Stop)
                {
                    if (_active.TryGetValue(decision.RequestKey, out var stopped))
                    {
                        _active.Remove(decision.RequestKey);
                        UnityEngine.Object.DestroyImmediate(stopped);
                    }
                    return;
                }

                if (decision.Kind != BattlePresentationCueDecisionKind.Play) return;
                var color = predictionState == PresentationCuePredictionState.ServerConfirmed
                    ? new Color(0.3f, 1f, 0.45f)
                    : new Color(1f, 0.82f, 0.2f);
                if (_active.TryGetValue(decision.RequestKey, out var existing))
                {
                    existing.name = "ConfirmedCastCue-P87";
                    existing.GetComponent<Renderer>().sharedMaterial.color = color;
                    existing.transform.localScale = Vector3.one * 0.85f;
                    return;
                }

                var created = CreateMarker("PredictedCastCue", color, position, _created, 0.85f);
                _active[decision.RequestKey] = created;
            }

            public bool TryGet(BattlePresentationCueRequestKey key, out GameObject gameObject)
            {
                return _active.TryGetValue(key, out gameObject) && gameObject != null;
            }
        }

        [Serializable]
        private sealed class WallCorrectionArtifact
        {
            public string evidenceLevel;
            public int predictedFrame;
            public int confirmedFrame;
            public int replayedFrames;
            public float predictedX;
            public float authorityX;
            public float reconciledLogicX;
            public float smoothedPresentationX;
            public int collisionCount;
            public string conflictLevel;
            public string screenshot;
        }

        [Serializable]
        private sealed class CastReconciliationArtifact
        {
            public string evidenceLevel;
            public int predictionKeyAccepted;
            public bool acceptedSameInstance;
            public int acceptedInstanceId;
            public int predictionKeyRejected;
            public bool rejectedDestroyed;
            public int rejectedInstanceId;
            public long confirmedCount;
            public long rejectedCount;
            public string screenshot;
        }

        [Serializable]
        private sealed class SkillTransactionArtifact
        {
            public string evidenceLevel;
            public int predictionKeyAccepted;
            public bool acceptedSameInstance;
            public int predictedPhase;
            public int authoritativePhase;
            public float predictedPlayback;
            public float calibratedPlayback;
            public float acceptedMana;
            public int acceptedCooldownEndFrame;
            public int acceptedInputLockMask;
            public int predictionKeyRejected;
            public float rejectedPredictedMana;
            public float rejectedRestoredMana;
            public int rejectedCooldownEndFrame;
            public int rejectedInputLockMask;
            public int rejectedBlendFrames;
            public bool rejectedVfxDestroyed;
            public string screenshot;
        }
    }
}
