using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.LogicWorld;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaSnapshotRequestContractTests
    {
        private static readonly FrameIndex InvalidFrame = new FrameIndex(-1);

        [Test]
        public void Router_TryGetSnapshot_WithNegativeFrame_ThrowsWithoutChangingHealth()
        {
            var router = new MobaSnapshotRouter();
            var before = router.GetHealth();

            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => router.TryGetSnapshot(InvalidFrame, out _));

            AssertInvalidFrame(exception);
            AssertHealthUnchanged(before, router.GetHealth());
        }

        [Test]
        public void Router_CollectSnapshots_WithInvalidArguments_ThrowsWithoutChangingHealthOrBuffer()
        {
            var router = new MobaSnapshotRouter();
            var sentinel = new WorldStateSnapshot(91, new byte[] { 1 });
            var snapshots = new List<WorldStateSnapshot> { sentinel };
            var before = router.GetHealth();

            var frameException = Assert.Throws<ArgumentOutOfRangeException>(
                () => router.CollectSnapshots(InvalidFrame, snapshots));
            var maxException = Assert.Throws<ArgumentOutOfRangeException>(
                () => router.CollectSnapshots(new FrameIndex(0), snapshots, 0));

            AssertInvalidFrame(frameException);
            Assert.That(maxException.ParamName, Is.EqualTo("maxSnapshots"));
            Assert.That(maxException.ActualValue, Is.EqualTo(0));
            Assert.That(snapshots, Has.Count.EqualTo(1));
            Assert.That(snapshots[0].OpCode, Is.EqualTo(sentinel.OpCode));
            Assert.That(snapshots[0].Payload, Is.SameAs(sentinel.Payload));
            AssertHealthUnchanged(before, router.GetHealth());
        }

        [Test]
        public void Router_WithValidFrameAndNoEmitters_ReturnsEmptyAndRecordsValidRequests()
        {
            var router = new MobaSnapshotRouter();
            var snapshots = new List<WorldStateSnapshot>();

            var found = router.TryGetSnapshot(new FrameIndex(7), out var snapshot);
            var count = router.CollectSnapshots(new FrameIndex(8), snapshots);
            var health = router.GetHealth();

            Assert.That(found, Is.False);
            Assert.That(snapshot.OpCode, Is.Zero);
            Assert.That(snapshot.Payload, Is.Null);
            Assert.That(count, Is.Zero);
            Assert.That(snapshots, Is.Empty);
            Assert.That(health.SingleRequests, Is.EqualTo(1));
            Assert.That(health.BatchRequests, Is.EqualTo(1));
            Assert.That(health.HitCount, Is.Zero);
            Assert.That(health.EmptyCount, Is.EqualTo(2));
            Assert.That(health.LastFrame, Is.EqualTo(8));
            Assert.That(health.LastBatchSnapshotCount, Is.Zero);
        }

        [Test]
        public void IOPort_WithInvalidSnapshotArguments_RejectsBeforeProviderInvocation()
        {
            var provider = new PermissiveSnapshotProvider();
            var port = new MobaBattleIOPort(new StubInputCoordinator(), provider);
            var snapshots = new List<WorldStateSnapshot>();

            var singleException = Assert.Throws<ArgumentOutOfRangeException>(
                () => port.TryGetSnapshot(InvalidFrame, out _));
            var batchException = Assert.Throws<ArgumentOutOfRangeException>(
                () => port.CollectSnapshots(InvalidFrame, snapshots));
            var maxException = Assert.Throws<ArgumentOutOfRangeException>(
                () => port.CollectSnapshots(new FrameIndex(0), snapshots, 0));

            AssertInvalidFrame(singleException);
            AssertInvalidFrame(batchException);
            Assert.That(maxException.ParamName, Is.EqualTo("maxSnapshots"));
            Assert.That(provider.RequestCount, Is.Zero);
            Assert.That(snapshots, Is.Empty);
        }

        [Test]
        public void RuntimePort_WithInvalidSnapshotArguments_RejectsBeforeOutputInvocation()
        {
            var output = new PermissiveBattleOutputPort();
            var port = new MobaBattleRuntimePort(null, null, output, null);
            var snapshots = new List<WorldStateSnapshot>();

            var singleException = Assert.Throws<ArgumentOutOfRangeException>(
                () => port.TryGetSnapshot(InvalidFrame, out _));
            var batchException = Assert.Throws<ArgumentOutOfRangeException>(
                () => port.CollectSnapshots(InvalidFrame, snapshots));
            var maxException = Assert.Throws<ArgumentOutOfRangeException>(
                () => port.CollectSnapshots(new FrameIndex(0), snapshots, 0));

            AssertInvalidFrame(singleException);
            AssertInvalidFrame(batchException);
            Assert.That(maxException.ParamName, Is.EqualTo("maxSnapshots"));
            Assert.That(output.RequestCount, Is.Zero);
            Assert.That(snapshots, Is.Empty);
        }

        [Test]
        public void RuntimePort_WhenOutputIsMissing_PrioritizesRequiredDependencyFailure()
        {
            var port = new MobaBattleRuntimePort(null, null, null, null);

            var singleException = Assert.Throws<InvalidOperationException>(
                () => port.TryGetSnapshot(InvalidFrame, out _));
            var batchException = Assert.Throws<InvalidOperationException>(
                () => port.CollectSnapshots(InvalidFrame, null, 0));

            StringAssert.Contains(nameof(IMobaBattleOutputPort), singleException.Message);
            StringAssert.Contains(nameof(IMobaBattleOutputPort), batchException.Message);
        }

        private static void AssertInvalidFrame(ArgumentOutOfRangeException exception)
        {
            Assert.That(exception.ParamName, Is.EqualTo("frame"));
            Assert.That(exception.ActualValue, Is.EqualTo(-1));
        }

        private static void AssertHealthUnchanged(
            MobaSnapshotRouterHealth expected,
            MobaSnapshotRouterHealth actual)
        {
            Assert.That(actual.SingleRequests, Is.EqualTo(expected.SingleRequests));
            Assert.That(actual.BatchRequests, Is.EqualTo(expected.BatchRequests));
            Assert.That(actual.HitCount, Is.EqualTo(expected.HitCount));
            Assert.That(actual.EmptyCount, Is.EqualTo(expected.EmptyCount));
            Assert.That(actual.LastFrame, Is.EqualTo(expected.LastFrame));
            Assert.That(actual.LastSnapshotOpCode, Is.EqualTo(expected.LastSnapshotOpCode));
            Assert.That(actual.LastBatchSnapshotCount, Is.EqualTo(expected.LastBatchSnapshotCount));
        }

        private sealed class StubInputCoordinator : IMobaInputCoordinator
        {
            public void Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
            {
            }

            public LogicWorldInputSubmitResult TrySubmit(
                FrameIndex frame,
                IReadOnlyList<PlayerInputCommand> inputs)
            {
                return LogicWorldInputSubmitResult.Accepted(inputs?.Count ?? 0, inputs?.Count ?? 0);
            }
        }

        private sealed class PermissiveSnapshotProvider : IWorldStateSnapshotProvider
        {
            public int RequestCount { get; private set; }

            public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
            {
                RequestCount++;
                snapshot = default;
                return false;
            }

            public void Dispose()
            {
            }
        }

        private sealed class PermissiveBattleOutputPort : IMobaBattleOutputPort
        {
            public int RequestCount { get; private set; }

            public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
            {
                RequestCount++;
                snapshot = default;
                return false;
            }

            public int CollectSnapshots(
                FrameIndex frame,
                IList<WorldStateSnapshot> snapshots,
                int maxSnapshots = 32)
            {
                RequestCount++;
                return 0;
            }
        }
    }
}
