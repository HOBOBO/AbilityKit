using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaDerivedSkillServiceTests
    {
        [Test]
        public void ChildFinalization_ReleasesParentRetainAndDepthGuardRejectsExcessiveNesting()
        {
            var runtimes = new MobaSkillCastRuntimeService();
            using var derived = new MobaDerivedSkillService(runtimes);
            var parent = CreateRuntime(runtimes, 99200020, 1, 1001L);
            var child = CreateRuntime(runtimes, 99200021, 2, 1002L);
            var grandchild = CreateRuntime(runtimes, 99200022, 3, 1003L);

            Assert.That(derived.Link(in parent, in child, maxDepth: 2, out var failure), Is.True, failure);
            Assert.That(derived.Link(in child, in grandchild, maxDepth: 2, out failure), Is.True, failure);
            Assert.That(derived.CanStart(in grandchild, maxDepth: 2, out failure), Is.False);
            Assert.That(runtimes.CountPendingChildren(in parent, MobaSkillRuntimeChildKind.SkillRuntime), Is.EqualTo(1));

            Assert.That(runtimes.MarkPipelineEnded(in child, MobaSkillRuntimeEndReason.PipelineCompleted), Is.True);
            Assert.That(runtimes.CountPendingChildren(in parent, MobaSkillRuntimeChildKind.SkillRuntime), Is.EqualTo(1),
                "The child remains alive while its own derived child is retained.");
            Assert.That(runtimes.MarkPipelineEnded(in grandchild, MobaSkillRuntimeEndReason.PipelineCompleted), Is.True);
            Assert.That(runtimes.CountPendingChildren(in parent, MobaSkillRuntimeChildKind.SkillRuntime), Is.EqualTo(0));
        }

        [Test]
        public void RollbackProvider_RestoresLinksAndReleasesParentWhenChildFinalizes()
        {
            var runtimes = new MobaSkillCastRuntimeService();
            var parent = CreateRuntime(runtimes, 99200020, 1, 2001L);
            var child = CreateRuntime(runtimes, 99200021, 2, 2002L);
            byte[] payload;

            using (var original = new MobaDerivedSkillService(runtimes))
            {
                Assert.That(original.Link(in parent, in child, maxDepth: 4, out var failure), Is.True, failure);
                payload = new MobaDerivedSkillRollbackProvider(original).ExportState(new FrameIndex(30));
            }

            using var restored = new MobaDerivedSkillService(runtimes);
            new MobaDerivedSkillRollbackProvider(restored).ImportState(new FrameIndex(30), payload);
            Assert.That(runtimes.CountPendingChildren(in parent, MobaSkillRuntimeChildKind.SkillRuntime), Is.EqualTo(1));

            Assert.That(runtimes.MarkPipelineEnded(in child, MobaSkillRuntimeEndReason.PipelineCompleted), Is.True);
            Assert.That(runtimes.CountPendingChildren(in parent, MobaSkillRuntimeChildKind.SkillRuntime), Is.EqualTo(0));
        }

        private static MobaSkillCastRuntimeHandle CreateRuntime(
            MobaSkillCastRuntimeService runtimes, int skillId, int sequence, long traceId)
        {
            var aim = Vec3.Zero;
            var direction = Vec3.Forward;
            var request = new MobaSkillCastRuntimeCreateRequest(
                skillId, 0, 1, sequence, 7, 8, in aim, in direction, traceId);
            return runtimes.Create(in request).Handle;
        }
    }
}
