using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Services.StateSync;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaDeterministicCheckpointCoordinatorTests
    {
        private static readonly FrameIndex Frame = new FrameIndex(12);

        [Test]
        public void Restore_PrepareFailure_DoesNotMutateAnyProvider()
        {
            var log = new List<string>();
            var first = new StagedProvider(1, 10, log);
            var second = new StagedProvider(2, 20, log);
            var coordinator = CreateCoordinator(first, second);
            var checkpoint = coordinator.Capture(Frame);
            first.Value = 30;
            second.Value = 40;
            log.Clear();
            second.ThrowOnPrepare = true;

            Assert.Throws<InvalidOperationException>(() => coordinator.Restore(in checkpoint));

            Assert.That(first.Value, Is.EqualTo(30));
            Assert.That(second.Value, Is.EqualTo(40));
            Assert.That(first.ImportCount, Is.Zero);
            Assert.That(second.ImportCount, Is.Zero);
            Assert.That(log, Is.EqualTo(new[] { "prepare:1", "prepare:2" }));
        }

        [Test]
        public void Restore_PostApplyValidationFailure_RollsBackAppliedProvidersInReverseOrder()
        {
            var log = new List<string>();
            var first = new StagedProvider(1, 10, log);
            var second = new StagedProvider(2, 20, log);
            var coordinator = CreateCoordinator(first, second);
            var checkpoint = coordinator.Capture(Frame);
            first.Value = 30;
            second.Value = 40;
            log.Clear();
            second.ThrowOnValidate = true;

            Assert.Throws<InvalidOperationException>(() => coordinator.Restore(in checkpoint));

            Assert.That(first.Value, Is.EqualTo(30));
            Assert.That(second.Value, Is.EqualTo(40));
            Assert.That(log, Is.EqualTo(new[]
            {
                "prepare:1", "prepare:2",
                "import:1:10", "validate:1",
                "import:2:20", "validate:2",
                "import:2:40", "import:1:30"
            }));
        }

        [Test]
        public void Restore_HashMismatch_RollsBackAllProviders()
        {
            var first = new StagedProvider(1, 10);
            var second = new StagedProvider(2, 20);
            var coordinator = CreateCoordinator(first, second);
            var captured = coordinator.Capture(Frame);
            var checkpoint = new MobaDeterministicCheckpoint(
                captured.SchemaVersion,
                captured.WorldId,
                captured.WorldType,
                captured.TickRate,
                captured.Frame,
                captured.StateHash + 1u,
                captured.Entries);
            first.Value = 30;
            second.Value = 40;

            Assert.Throws<InvalidOperationException>(() => coordinator.Restore(in checkpoint));

            Assert.That(first.Value, Is.EqualTo(30));
            Assert.That(second.Value, Is.EqualTo(40));
        }

        [Test]
        public void Restore_Success_PreparesAllProvidersBeforeApplyingInKeyOrder()
        {
            var log = new List<string>();
            var first = new StagedProvider(1, 10, log);
            var second = new StagedProvider(2, 20, log);
            var coordinator = CreateCoordinator(second, first);
            var checkpoint = coordinator.Capture(Frame);
            first.Value = 30;
            second.Value = 40;
            log.Clear();

            coordinator.Restore(in checkpoint);

            Assert.That(first.Value, Is.EqualTo(10));
            Assert.That(second.Value, Is.EqualTo(20));
            Assert.That(log, Is.EqualTo(new[]
            {
                "prepare:1", "prepare:2",
                "import:1:10", "validate:1",
                "import:2:20", "validate:2"
            }));
        }

        private static MobaDeterministicCheckpointCoordinator CreateCoordinator(
            params IMobaStateRecoveryProvider[] providers)
        {
            return new MobaDeterministicCheckpointCoordinator(
                "world",
                "moba",
                30,
                providers);
        }

        private sealed class StagedProvider : IMobaStagedStateRecoveryProvider
        {
            private readonly List<string> _log;

            public StagedProvider(int key, byte value, List<string> log = null)
            {
                Key = key;
                Value = value;
                _log = log;
            }

            public int Key { get; }
            public string Name => "test-" + Key;
            public byte Value { get; set; }
            public bool ThrowOnPrepare { get; set; }
            public bool ThrowOnValidate { get; set; }
            public int ImportCount { get; private set; }

            public byte[] ExportState(FrameIndex frame)
            {
                return new[] { Value };
            }

            public void PrepareRestore(FrameIndex frame, byte[] payload)
            {
                _log?.Add("prepare:" + Key);
                if (ThrowOnPrepare)
                {
                    throw new InvalidOperationException("prepare failed");
                }

                if (payload == null || payload.Length != 1)
                {
                    throw new InvalidOperationException("invalid payload");
                }
            }

            public void ImportState(FrameIndex frame, byte[] payload)
            {
                ImportCount++;
                Value = payload[0];
                _log?.Add("import:" + Key + ":" + Value);
            }

            public void ValidateRestoredState(FrameIndex frame, byte[] payload)
            {
                _log?.Add("validate:" + Key);
                if (ThrowOnValidate)
                {
                    throw new InvalidOperationException("validation failed");
                }

                if (Value != payload[0])
                {
                    throw new InvalidOperationException("state does not match payload");
                }
            }

            public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
            {
                hash.AddByte(Value);
            }
        }
    }
}
