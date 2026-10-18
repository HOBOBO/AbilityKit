using AbilityKit.Game.Flow;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Game.Test.UnitTest
{
    /// <summary>
    /// Headless coverage for the role-follow interpolation pipeline:
    /// position sample buffer (extrapolation) and the position applier
    /// (frame-rate independent smoothing).
    /// </summary>
    public sealed class BattleViewInterpolationSmoothingTests
    {
        [Test]
        public void SampleBuffer_LinearInterpolation_BetweenSamples()
        {
            var buffer = new BattleViewPositionSampleBuffer();
            buffer.Add(time: 0.0d, pos: new Vector3(0f, 0f, 0f));
            buffer.Add(time: 1.0d, pos: new Vector3(10f, 0f, 0f));

            var resolved = buffer.TryEvaluate(0.5d, out var pos);

            Assert.IsTrue(resolved);
            Assert.AreEqual(5f, pos.x, 0.0001f);
            Assert.AreEqual(0f, pos.y, 0.0001f);
            Assert.AreEqual(0f, pos.z, 0.0001f);
        }

        [Test]
        public void SampleBuffer_HoldsLastPosition_WhenFewerThanTwoSamples()
        {
            var buffer = new BattleViewPositionSampleBuffer();
            buffer.Add(time: 0.0d, pos: new Vector3(2f, 3f, 4f));

            // Even far past the only sample, extrapolation cannot derive velocity.
            var resolved = buffer.TryEvaluate(5.0d, out var pos);

            Assert.IsTrue(resolved);
            Assert.AreEqual(new Vector3(2f, 3f, 4f), pos);
        }

        [Test]
        public void SampleBuffer_ExtrapolatesBeyondLastSample_UsingLinearVelocity()
        {
            var buffer = new BattleViewPositionSampleBuffer();
            buffer.Add(time: 0.0d, pos: new Vector3(0f, 0f, 0f));
            buffer.Add(time: 1.0d, pos: new Vector3(10f, 0f, 0f));

            // 0.25s past the latest sample at velocity 10/s => +2.5 on X.
            var resolved = buffer.TryEvaluate(1.25d, out var pos);

            Assert.IsTrue(resolved);
            Assert.AreEqual(12.5f, pos.x, 0.0001f);
            Assert.AreEqual(0f, pos.y, 0.0001f);
            Assert.AreEqual(0f, pos.z, 0.0001f);
        }

        [Test]
        public void SampleBuffer_Extrapolation_ClampsAtMaxLeadTime()
        {
            var buffer = new BattleViewPositionSampleBuffer();
            buffer.Add(time: 0.0d, pos: new Vector3(0f, 0f, 0f));
            buffer.Add(time: 1.0d, pos: new Vector3(10f, 0f, 0f));

            // 100s past the latest sample should be clamped to MaxExtrapolationLeadSeconds.
            // With clamp = 1/15s (~0.0667s), position advances by velocity * clamp.
            var resolved = buffer.TryEvaluate(101.0d, out var pos);

            Assert.IsTrue(resolved);
            var clampedX = 10f + 10f * (1f / 15f);
            Assert.AreEqual(clampedX, pos.x, 0.05f);
        }

        [Test]
        public void SampleBuffer_CapacityAtLeastEight_RetainsRecentSamples()
        {
            var buffer = new BattleViewPositionSampleBuffer();
            // Push 10 samples; the buffer should retain the most recent 8.
            for (var i = 0; i < 10; i++)
            {
                buffer.Add(time: i, pos: new Vector3(i, 0f, 0f));
            }

            // The earliest retained time must be 2 (= 10 - 8).
            var resolved = buffer.TryEvaluate(2d, out var firstPos);
            Assert.IsTrue(resolved);
            Assert.AreEqual(2f, firstPos.x, 0.0001f);

            // The latest retained time must be 9.
            var resolvedLast = buffer.TryEvaluate(9d, out var lastPos);
            Assert.IsTrue(resolvedLast);
            Assert.AreEqual(9f, lastPos.x, 0.0001f);
        }

        [Test]
        public void PositionApplier_Smoothing_ApproachesTargetOverMultipleFrames()
        {
            var go = new GameObject("TestActor");
            try
            {
                var handle = new BattleViewHandle { GameObject = go };
                var applier = new BattleViewPositionApplier(handles: null, attachedVfx: null)
                {
                    SmoothingHz = 12f,
                };

                // First frame: target lands directly without smoothing.
                applier.ApplyPendingPositionsForTest(handle, new Vector3(0f, 0f, 0f), deltaTime: 0.05f);
                Assert.AreEqual(Vector3.zero, go.transform.position);

                // Second frame: target is at +10 X. With Hz=12 and dt=0.05, alpha = 1 - exp(-0.6) ~ 0.451.
                applier.ApplyPendingPositionsForTest(handle, new Vector3(10f, 0f, 0f), deltaTime: 0.05f);
                Assert.Less(go.transform.position.x, 10f);
                Assert.Greater(go.transform.position.x, 0f);

                // After many frames the position should converge to the target.
                for (var i = 0; i < 60; i++)
                {
                    applier.ApplyPendingPositionsForTest(handle, new Vector3(10f, 0f, 0f), deltaTime: 0.05f);
                }
                Assert.AreEqual(10f, go.transform.position.x, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PositionApplier_SmoothingDisabled_WritesTargetDirectly()
        {
            var go = new GameObject("TestActor");
            try
            {
                var handle = new BattleViewHandle { GameObject = go };
                var applier = new BattleViewPositionApplier(handles: null, attachedVfx: null)
                {
                    SmoothingHz = 0f,
                };

                applier.ApplyPendingPositionsForTest(handle, new Vector3(0f, 0f, 0f), deltaTime: 0.05f);
                applier.ApplyPendingPositionsForTest(handle, new Vector3(7f, 0f, 0f), deltaTime: 0.05f);

                Assert.AreEqual(7f, go.transform.position.x, 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PositionApplier_TurnsTowardForwardAtConfiguredRateAndRetainsTargetForZeroForward()
        {
            var go = new GameObject("TestActor");
            try
            {
                var handle = new BattleViewHandle { GameObject = go };
                var applier = new BattleViewPositionApplier(handles: null, attachedVfx: null)
                {
                    TurnSpeedDegreesPerSecond = 180f,
                };

                applier.ApplyPendingPositionsForTest(handle, Vector3.zero, Vector3.forward, deltaTime: 0f);
                Assert.That(Vector3.Dot(go.transform.forward, Vector3.forward), Is.GreaterThan(0.999f));

                applier.ApplyPendingPositionsForTest(handle, Vector3.right, Vector3.right, deltaTime: 0.1f);
                Assert.That(Quaternion.Angle(Quaternion.identity, go.transform.rotation), Is.EqualTo(18f).Within(0.1f));

                var lastRotation = go.transform.rotation;
                applier.ApplyPendingPositionsForTest(handle, Vector3.right * 2f, Vector3.zero, deltaTime: 0.1f);
                Assert.That(Quaternion.Angle(go.transform.rotation, lastRotation), Is.EqualTo(18f).Within(0.1f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
