using System;
using System.IO;
using AbilityKit.Game.Battle.Diagnostics.Capture;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattleCaptureSessionTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "abilitykit-battle-capture-tests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
            catch { }
        }

        [Test]
        public void StartRecordMarkAndDispose_WritesVersionedJsonlPackage()
        {
            Assert.IsTrue(BattleCaptureSession.TryStart(_root, "jhin-w-real-input", "run-1", out var capture, out var error), error);
            var directory = capture.Directory;

            capture.RecordHudEvent(1840, "skill.aim_end", 2, 9002, 0.5f, -0.25f, 6f, -3f);
            capture.Mark("w-release-no-projectile", 1842);
            capture.Dispose();

            var manifest = File.ReadAllText(Path.Combine(directory, "capture-manifest.json"));
            var events = File.ReadAllLines(Path.Combine(directory, "input.jsonl"));
            StringAssert.Contains("\"status\":\"closed\"", manifest);
            StringAssert.Contains("\"captureProtocolVersion\":1", manifest);
            StringAssert.Contains("\"firstFrame\":1840", manifest);
            StringAssert.Contains("\"lastFrame\":1842", manifest);
            Assert.That(events, Has.Length.EqualTo(2));
            StringAssert.Contains("\"frame\":1840", events[0]);
            StringAssert.Contains("\"kind\":\"skill.aim_end\"", events[0]);
            StringAssert.Contains("\"resolvedX\":6", events[0]);
            StringAssert.Contains("\"type\":\"mark\"", events[1]);
            StringAssert.Contains("w-release-no-projectile", events[1]);
        }

        [Test]
        public void Dispose_IsIdempotentAndDisabledCaptureDoesNotAppend()
        {
            Assert.IsTrue(BattleCaptureSession.TryStart(_root, "case", "run", out var capture, out var error), error);
            capture.Dispose();
            capture.Dispose();
            capture.RecordHudEvent("skill.click", 1, 9001, 0f, 0f, 0f, 0f);

            var events = File.ReadAllLines(Path.Combine(capture.Directory, "input.jsonl"));
            Assert.That(events, Has.Length.EqualTo(0));
            Assert.IsFalse(capture.IsEnabled);
        }
    }
}
