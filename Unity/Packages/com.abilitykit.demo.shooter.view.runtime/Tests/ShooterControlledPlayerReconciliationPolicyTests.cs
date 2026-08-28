#nullable enable

using NUnit.Framework;

namespace AbilityKit.Demo.Shooter.View.Tests
{
    public sealed class ShooterControlledPlayerReconciliationPolicyTests
    {
        private const float FixedDeltaTime = 1f / 30f;

        [Test]
        public void PreservesPredictionInsideAuthorityFrameAgeEnvelope()
        {
            var applied = Resolve(
                currentX: 8f,
                authorityX: 0f,
                currentFrame: 148,
                authorityFrame: 100,
                replayedFrames: 0,
                correctionBudget: 0.25f,
                forceSnap: false,
                out var resolvedX);

            Assert.That(applied, Is.Zero);
            Assert.That(resolvedX, Is.EqualTo(8f));
        }

        [Test]
        public void PreservesPredictionWhenClientFrameTemporarilyTrailsAuthority()
        {
            var applied = Resolve(
                currentX: 5f,
                authorityX: 0f,
                currentFrame: 100,
                authorityFrame: 130,
                replayedFrames: 0,
                correctionBudget: 0.25f,
                forceSnap: false,
                out var resolvedX);

            Assert.That(applied, Is.Zero);
            Assert.That(resolvedX, Is.EqualTo(5f));
        }

        [Test]
        public void CorrectsOnlyErrorOutsideAuthorityFrameAgeEnvelope()
        {
            var applied = Resolve(
                currentX: 8.5f,
                authorityX: 0f,
                currentFrame: 148,
                authorityFrame: 100,
                replayedFrames: 0,
                correctionBudget: 0.25f,
                forceSnap: false,
                out var resolvedX);

            Assert.That(applied, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(resolvedX, Is.EqualTo(8.25f).Within(0.0001f));
        }

        [Test]
        public void ReplayedInputsReduceRemainingPredictionEnvelope()
        {
            var applied = Resolve(
                currentX: 7.5f,
                authorityX: 0f,
                currentFrame: 148,
                authorityFrame: 100,
                replayedFrames: 6,
                correctionBudget: 0.25f,
                forceSnap: false,
                out var resolvedX);

            Assert.That(applied, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(resolvedX, Is.EqualTo(7.25f).Within(0.0001f));
        }

        [Test]
        public void HonorsRemainingPerClientFrameCorrectionBudget()
        {
            var applied = Resolve(
                currentX: 2f,
                authorityX: 0f,
                currentFrame: 100,
                authorityFrame: 100,
                replayedFrames: 0,
                correctionBudget: 0.05f,
                forceSnap: false,
                out var resolvedX);

            Assert.That(applied, Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(resolvedX, Is.EqualTo(1.95f).Within(0.0001f));
        }

        [Test]
        public void IgnoresSubToleranceErrorWithoutFrameLead()
        {
            var applied = Resolve(
                currentX: 0.04f,
                authorityX: 0f,
                currentFrame: 100,
                authorityFrame: 100,
                replayedFrames: 0,
                correctionBudget: 0.25f,
                forceSnap: false,
                out var resolvedX);

            Assert.That(applied, Is.Zero);
            Assert.That(resolvedX, Is.EqualTo(0.04f));
        }

        [Test]
        public void ForceSnapIgnoresPredictionEnvelopeAndBudget()
        {
            var applied = Resolve(
                currentX: 8f,
                authorityX: 1f,
                currentFrame: 148,
                authorityFrame: 100,
                replayedFrames: 0,
                correctionBudget: 0f,
                forceSnap: true,
                out var resolvedX);

            Assert.That(applied, Is.EqualTo(7f).Within(0.0001f));
            Assert.That(resolvedX, Is.EqualTo(1f));
        }

        private static float Resolve(
            float currentX,
            float authorityX,
            int currentFrame,
            int authorityFrame,
            int replayedFrames,
            float correctionBudget,
            bool forceSnap,
            out float resolvedX)
        {
            return ShooterClientAuthoritativeInterpolationSyncController.ResolveControlledPlayerPosition(
                currentX,
                0f,
                authorityX,
                0f,
                currentFrame,
                authorityFrame,
                replayedFrames,
                FixedDeltaTime,
                correctionBudget,
                forceSnap,
                localPredictionTolerance: 0.05f,
                snapDistance: float.MaxValue,
                out resolvedX,
                out _);
        }
    }
}
