using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share.Config;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaSkillWindowRuntimeServiceTests
    {
        [Test]
        public void WindowFacts_RoundTripThroughExistingSkillRuntimeRollbackProvider()
        {
            var runtimes = new MobaSkillCastRuntimeService();
            var aim = Vec3.Zero;
            var direction = Vec3.Forward;
            var request = new MobaSkillCastRuntimeCreateRequest(801001, 1, 2, 3, 10, 20, in aim, in direction, 9001L);
            var runtime = runtimes.Create(in request);
            var windows = new MobaSkillWindowRuntimeService(runtimes);
            var handle = runtime.Handle;

            Assert.That(windows.Open(in handle, "charge.primary", SkillWindowKind.Charge), Is.True);
            Assert.That(windows.Advance(in handle, 650, 2, 0), Is.True);
            Assert.That(windows.SignalEvent(in handle, "projectile.hit"), Is.True);
            Assert.That(windows.Commit(in handle, "resource.commit"), Is.True);

            var provider = new MobaSkillRuntimeRollbackProvider(runtimes);
            var payload = provider.ExportState(new FrameIndex(12));
            windows.Advance(in handle, 900, 3, 0);
            windows.Close(in handle, MobaSkillWindowEndReason.Cancelled);
            provider.ImportState(new FrameIndex(12), payload);

            Assert.That(windows.TryGetSnapshot(in handle, out var restored), Is.True);
            Assert.That(restored.Kind, Is.EqualTo(SkillWindowKind.Charge));
            Assert.That(restored.ElapsedMs, Is.EqualTo(650));
            Assert.That(restored.ChargeTier, Is.EqualTo(2));
            Assert.That(restored.EventSequence, Is.EqualTo(1));
            Assert.That(restored.CommitSequence, Is.EqualTo(1));
            Assert.That(restored.EndReason, Is.EqualTo(MobaSkillWindowEndReason.None));
        }

        [Test]
        public void RecastSignal_IsAcceptedOnlyByOpenRecastWindow()
        {
            var runtimes = new MobaSkillCastRuntimeService();
            var aim = Vec3.Zero;
            var direction = Vec3.Forward;
            var request = new MobaSkillCastRuntimeCreateRequest(801002, 2, 1, 1, 11, 0, in aim, in direction, 9002L);
            var runtime = runtimes.Create(in request);
            var windows = new MobaSkillWindowRuntimeService(runtimes);
            var handle = runtime.Handle;

            windows.Open(in handle, "charge", SkillWindowKind.Charge);
            Assert.That(windows.TrySignalRecast(in handle), Is.False);
            windows.Open(in handle, "recast", SkillWindowKind.Recast);
            Assert.That(windows.TrySignalRecast(in handle), Is.True);
            Assert.That(windows.TryGetSnapshot(in handle, out var active), Is.True);
            Assert.That(active.RecastSequence, Is.EqualTo(1));
            windows.Close(in handle, MobaSkillWindowEndReason.Recast);
            Assert.That(windows.TrySignalRecast(in handle), Is.False);
        }
    }
}
