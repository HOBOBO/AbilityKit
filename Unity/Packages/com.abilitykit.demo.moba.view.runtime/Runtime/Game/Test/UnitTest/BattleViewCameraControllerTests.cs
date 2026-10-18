using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Flow;
using EC = AbilityKit.World.ECS;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Game.Test.UnitTest
{
    /// <summary>
    /// Unit tests for the battle view camera. Verifies:
    /// - first-frame snap on a freshly tracked actor (SnapOnFirstTarget)
    /// - the camera ignores vertical motion when LockVerticalAxis is enabled,
    ///   so jumps do not jitter the view.
    /// - the camera falls back to the last known valid position when the entity
    ///   has not yet spawned, so follow does not stall waiting for the first snapshot.
    /// </summary>
    public sealed class BattleViewCameraControllerTests
    {
        [Test]
        public void AuxiliaryCamera_DoesNotFallBackToMainCamera()
        {
            var go = new GameObject("main-camera");
            go.tag = "MainCamera";
            var camera = go.AddComponent<Camera>();
            var original = go.transform.position;
            try
            {
                var controller = new BattleViewCameraController(
                    allowMainCameraFallback: false);
                controller.SetCamera(null);
                controller.TrackActor(TrackedActorId);
                var query = new FakeEntityQuery();
                query.SetActorPosition(new Vector3(5f, 0f, 7f));

                controller.Tick(query, 0.05f);

                Assert.That(controller.Camera, Is.Null);
                Assert.That(camera.transform.position, Is.EqualTo(original));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private sealed class FakeEntityQuery : IBattleEntityQuery
        {
            public EC.IECWorld World => null;
            public BattleEntityLookup Lookup => null;
            public bool TryResolve(BattleNetId netId, out EC.IEntity entity)
            {
                entity = default;
                return false;
            }

            public bool TryGetTransform(BattleNetId netId, out BattleTransformComponent transform)
            {
                transform = _transform;
                return _transform != null;
            }

            public bool TryGetCharacter(BattleNetId netId, out BattleCharacterComponent character)
            {
                character = null;
                return false;
            }

            public bool TryGetProjectile(BattleNetId netId, out BattleProjectileComponent projectile)
            {
                projectile = null;
                return false;
            }

            public bool TryGetSkills(BattleNetId netId, out SkillListComponent skills)
            {
                skills = null;
                return false;
            }

            public bool TryGetBuffs(BattleNetId netId, out BuffListComponent buffs)
            {
                buffs = null;
                return false;
            }

            public void SetActorPosition(Vector3 pos)
            {
                _transform = new BattleTransformComponent { Position = pos, Forward = Vector3.forward };
            }

            public void ClearActorPosition()
            {
                _transform = null;
            }

            private BattleTransformComponent _transform;
        }

        private const int TrackedActorId = 42;

        private static (Camera camera, BattleViewCameraController controller, FakeEntityQuery query) CreateCamera(BattleCameraConfig config = null)
        {
            var go = new GameObject("Camera");
            go.AddComponent<Camera>();
            var controller = new BattleViewCameraController(config);
            controller.SetCamera(go.GetComponent<Camera>());
            controller.TrackActor(TrackedActorId);
            return (go.GetComponent<Camera>(), controller, new FakeEntityQuery());
        }

        private static void DestroyCamera(GameObject go)
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void FirstTick_WithSnapEnabled_SnapsCameraToTargetPlusOffset()
        {
            var go = new GameObject("Camera");
            try
            {
                go.AddComponent<Camera>();
                var controller = new BattleViewCameraController(new BattleCameraConfig
                {
                    SnapOnFirstTarget = true,
                    LockVerticalAxis = false,
                });
                controller.SetCamera(go.GetComponent<Camera>());
                controller.TrackActor(TrackedActorId);

                var query = new FakeEntityQuery();
                query.SetActorPosition(new Vector3(5f, 0f, 7f));

                controller.Tick(query, deltaTime: 0.05f);

                var cameraTransform = go.transform;
                var expected = new Vector3(5f, 0f, 7f) + new Vector3(0f, 15f, -12f);
                Assert.AreEqual(expected.x, cameraTransform.position.x, 0.0001f);
                Assert.AreEqual(expected.y, cameraTransform.position.y, 0.0001f);
                Assert.AreEqual(expected.z, cameraTransform.position.z, 0.0001f);
            }
            finally
            {
                DestroyCamera(go);
            }
        }

        [Test]
        public void FirstTick_WithSnapDisabled_UsesLerpFromCurrentPosition()
        {
            var go = new GameObject("Camera");
            try
            {
                go.AddComponent<Camera>();
                go.transform.position = new Vector3(0f, 0f, 0f);
                var controller = new BattleViewCameraController(new BattleCameraConfig
                {
                    SnapOnFirstTarget = false,
                    PositionLerpSpeed = 8f,
                    LockVerticalAxis = false,
                });
                controller.SetCamera(go.GetComponent<Camera>());
                controller.TrackActor(TrackedActorId);

                var query = new FakeEntityQuery();
                query.SetActorPosition(new Vector3(100f, 0f, 0f));

                controller.Tick(query, deltaTime: 0.016f);

                var pos = go.transform.position;
                // Lerp should not fully reach the target on a single short deltaTime.
                Assert.Less(pos.x, 100f);
                Assert.Greater(pos.x, 0f);
            }
            finally
            {
                DestroyCamera(go);
            }
        }

        [Test]
        public void VerticalAxisLocked_DoesNotFollowJump()
        {
            var go = new GameObject("Camera");
            try
            {
                go.AddComponent<Camera>();
                var controller = new BattleViewCameraController(new BattleCameraConfig
                {
                    SnapOnFirstTarget = true,
                    LockVerticalAxis = true,
                });
                controller.SetCamera(go.GetComponent<Camera>());
                controller.TrackActor(TrackedActorId);

                var query = new FakeEntityQuery();
                // First frame at y=0 - locks the Y reference.
                query.SetActorPosition(new Vector3(5f, 0f, 5f));
                controller.Tick(query, deltaTime: 0.05f);

                var initialPos = go.transform.position;

                // Actor jumps up by 10. With LockVerticalAxis, the camera position must
                // *not* change because target.y is replaced by the locked reference.
                query.SetActorPosition(new Vector3(5f, 10f, 5f));
                controller.Tick(query, deltaTime: 0.05f);

                Assert.AreEqual(initialPos.x, go.transform.position.x, 0.0001f);
                Assert.AreEqual(initialPos.y, go.transform.position.y, 0.0001f);
                Assert.AreEqual(initialPos.z, go.transform.position.z, 0.0001f);
            }
            finally
            {
                DestroyCamera(go);
            }
        }

        [Test]
        public void VerticalAxisUnlocked_SmoothlyFollowsJumpHeight()
        {
            var go = new GameObject("Camera");
            try
            {
                go.AddComponent<Camera>();
                var controller = new BattleViewCameraController(new BattleCameraConfig
                {
                    SnapOnFirstTarget = true,
                    PositionLerpSpeed = 8f,
                    LockVerticalAxis = false,
                });
                controller.SetCamera(go.GetComponent<Camera>());
                controller.TrackActor(TrackedActorId);

                var query = new FakeEntityQuery();
                query.SetActorPosition(new Vector3(5f, 0f, 5f));
                controller.Tick(query, deltaTime: 0.05f);

                var initialPos = go.transform.position;
                query.SetActorPosition(new Vector3(5f, 10f, 5f));
                controller.Tick(query, deltaTime: 0.05f);

                var expectedMove = 10f * (1f - Mathf.Exp(-8f * 0.05f));
                Assert.AreEqual(expectedMove, go.transform.position.y - initialPos.y, 0.0001f);
            }
            finally
            {
                DestroyCamera(go);
            }
        }

        [Test]
        public void Retrack_RearmsSnapPending()
        {
            var go = new GameObject("Camera");
            try
            {
                go.AddComponent<Camera>();
                var controller = new BattleViewCameraController(new BattleCameraConfig
                {
                    SnapOnFirstTarget = true,
                    LockVerticalAxis = false,
                });
                controller.SetCamera(go.GetComponent<Camera>());
                controller.TrackActor(TrackedActorId);

                var query = new FakeEntityQuery();
                query.SetActorPosition(new Vector3(0f, 0f, 0f));
                controller.Tick(query, deltaTime: 0.05f);

                // Switch to a different actor -> should snap again.
                controller.TrackActor(43);
                query.SetActorPosition(new Vector3(50f, 0f, 0f));
                controller.Tick(query, deltaTime: 0.05f);

                var expected = new Vector3(50f, 0f, 0f) + new Vector3(0f, 15f, -12f);
                Assert.AreEqual(expected.x, go.transform.position.x, 0.0001f);
            }
            finally
            {
                DestroyCamera(go);
            }
        }

        [Test]
        public void FollowsLastValidPosition_WhenEntityNotYetSpawned()
        {
            // Regression test: when the entity has not spawned yet, the camera should
            // continue following the last known position instead of stalling.
            var go = new GameObject("Camera");
            try
            {
                go.AddComponent<Camera>();
                var controller = new BattleViewCameraController(new BattleCameraConfig
                {
                    SnapOnFirstTarget = true,
                    LockVerticalAxis = false,
                });
                controller.SetCamera(go.GetComponent<Camera>());
                controller.TrackActor(TrackedActorId);

                var query = new FakeEntityQuery();

                // Frame 1: entity not spawned — no position available yet.
                controller.Tick(query, deltaTime: 0.016f);
                // Camera should do nothing (no valid position and no fallback yet).
                var posAfterMissedFrame = go.transform.position;
                Assert.AreEqual(new Vector3(0f, 0f, 0f), posAfterMissedFrame);

                // Frame 2: entity spawns at position (10, 0, 5).
                query.SetActorPosition(new Vector3(10f, 0f, 5f));
                controller.Tick(query, deltaTime: 0.016f);
                // Now camera should have snapped.
                var expected = new Vector3(10f, 0f, 5f) + new Vector3(0f, 15f, -12f);
                Assert.AreEqual(expected.x, go.transform.position.x, 0.0001f);

                // Frame 3: entity despawns — camera should keep following last known pos.
                query.ClearActorPosition();
                controller.Tick(query, deltaTime: 0.016f);
                // Camera should remain at the last valid world target.
                Assert.AreEqual(expected.x, go.transform.position.x, 0.0001f);
            }
            finally
            {
                DestroyCamera(go);
            }
        }

        [Test]
        public void NoFollow_WhenNoPositionAndNoFallback()
        {
            // When the entity has not spawned and there is no last known position,
            // the camera should not move.
            var go = new GameObject("Camera");
            try
            {
                go.AddComponent<Camera>();
                go.transform.position = new Vector3(99f, 99f, 99f);
                var controller = new BattleViewCameraController(new BattleCameraConfig
                {
                    SnapOnFirstTarget = false,
                    LockVerticalAxis = false,
                });
                controller.SetCamera(go.GetComponent<Camera>());
                controller.TrackActor(TrackedActorId);

                var query = new FakeEntityQuery();
                // Entity not spawned.
                controller.Tick(query, deltaTime: 0.016f);

                // Camera must not move from its initial position.
                Assert.AreEqual(new Vector3(99f, 99f, 99f), go.transform.position);
            }
            finally
            {
                DestroyCamera(go);
            }
        }

        [Test]
        public void PositionDamping_IsStableAcrossFrameRates()
        {
            var slowFrames = CreateCamera(new BattleCameraConfig
            {
                SnapOnFirstTarget = true,
                PositionLerpSpeed = 8f,
                LockVerticalAxis = false,
                AlwaysLookAtTarget = false,
            });
            var fastFrames = CreateCamera(new BattleCameraConfig
            {
                SnapOnFirstTarget = true,
                PositionLerpSpeed = 8f,
                LockVerticalAxis = false,
                AlwaysLookAtTarget = false,
            });

            try
            {
                slowFrames.query.SetActorPosition(Vector3.zero);
                fastFrames.query.SetActorPosition(Vector3.zero);
                slowFrames.controller.Tick(slowFrames.query, 0.01f);
                fastFrames.controller.Tick(fastFrames.query, 0.01f);

                slowFrames.query.SetActorPosition(new Vector3(10f, 0f, 0f));
                fastFrames.query.SetActorPosition(new Vector3(10f, 0f, 0f));
                for (var i = 0; i < 2; i++) slowFrames.controller.Tick(slowFrames.query, 0.05f);
                for (var i = 0; i < 10; i++) fastFrames.controller.Tick(fastFrames.query, 0.01f);

                Assert.AreEqual(
                    slowFrames.camera.transform.position.x,
                    fastFrames.camera.transform.position.x,
                    0.0001f,
                    "Equal elapsed time should produce equal damping regardless of frame rate.");
            }
            finally
            {
                DestroyCamera(slowFrames.camera != null ? slowFrames.camera.gameObject : null);
                DestroyCamera(fastFrames.camera != null ? fastFrames.camera.gameObject : null);
            }
        }

        [Test]
        public void LookAtDamping_KeepsOrientationStableOnSnapshotStep()
        {
            var state = CreateCamera(new BattleCameraConfig
            {
                SnapOnFirstTarget = true,
                PositionLerpSpeed = 8f,
                LookAtLerpSpeed = 8f,
                LockVerticalAxis = false,
                AlwaysLookAtTarget = true,
            });

            try
            {
                state.query.SetActorPosition(Vector3.zero);
                state.controller.Tick(state.query, 0.016f);
                var initialForward = state.camera.transform.forward;

                state.query.SetActorPosition(new Vector3(10f, 0f, 0f));
                state.controller.Tick(state.query, 0.016f);

                var steppedForward = state.camera.transform.forward;
                var immediateDirection = (new Vector3(10f, 0f, 0f) - state.camera.transform.position).normalized;
                Assert.AreEqual(0f, Vector3.Angle(initialForward, steppedForward), 0.001f);
                Assert.Greater(Vector3.Angle(steppedForward, immediateDirection), 0.01f);
            }
            finally
            {
                DestroyCamera(state.camera != null ? state.camera.gameObject : null);
            }
        }
    }
}
