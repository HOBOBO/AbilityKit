using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class BattleDiagnosticActorAttributeStoreTests
    {
        private BattleDiagnosticSessionScope _scope;
        private BattleDiagnosticActorAttributeStore _store;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("attributes", "world", 1);
            _store = new BattleDiagnosticActorAttributeStore(_scope);
        }

        [Test]
        public void QueryBeforeSnapshot_ReturnsNotProduced()
        {
            var result = _store.QueryActorAttributes(1, 0, 10);

            Assert.That(result.Status.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.NotProduced));
        }

        [Test]
        public void ReplaceSnapshot_QueriesActorAttributesAndModifiers()
        {
            var actors = new List<long> { 10, 20 };
            var attributes = new List<BattleDiagnosticActorAttribute>
            {
                new BattleDiagnosticActorAttribute(_scope, 7, 10, 1, 100f, 125f, 1, "Attack")
            };
            var modifiers = new List<BattleDiagnosticActorAttributeModifier>
            {
                new BattleDiagnosticActorAttributeModifier(_scope, 7, 10, 1, 1, 25f, 10, 99, 0)
            };

            Assert.That(_store.TryReplaceSnapshot(7, actors, attributes, modifiers), Is.True);
            attributes.Clear();
            modifiers.Clear();

            var attributeResult = _store.QueryActorAttributes(1, 0, 10);
            var modifierResult = _store.QueryActorAttributeModifiers(2, 7, 10);
            var emptyActorResult = _store.QueryActorAttributes(3, 7, 20);

            Assert.That(attributeResult.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Ready));
            Assert.That(attributeResult.Items.Count, Is.EqualTo(1));
            Assert.That(attributeResult.Items[0].FinalValue, Is.EqualTo(125f));
            Assert.That(modifierResult.Items.Count, Is.EqualTo(1));
            Assert.That(modifierResult.Items[0].SourceId, Is.EqualTo(99));
            Assert.That(emptyActorResult.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Empty));
        }

        [Test]
        public void QueryNonLatestFrameOrMissingActor_ReturnsNotCaptured()
        {
            Assert.That(_store.TryReplaceSnapshot(
                7,
                new long[] { 10 },
                new BattleDiagnosticActorAttribute[0],
                new BattleDiagnosticActorAttributeModifier[0]), Is.True);

            var oldFrame = _store.QueryActorAttributes(1, 6, 10);
            var missingActor = _store.QueryActorAttributes(2, 7, 20);

            Assert.That(oldFrame.Status.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.NotCaptured));
            Assert.That(missingActor.Status.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.NotCaptured));
        }

        [Test]
        public void InvalidAttributeOrOrphanModifier_RejectsWholeReplacement()
        {
            Assert.That(_store.TryReplaceSnapshot(
                1,
                new long[] { 10 },
                new[] { new BattleDiagnosticActorAttribute(_scope, 1, 10, 1, 1f, 2f, 0) },
                new BattleDiagnosticActorAttributeModifier[0]), Is.True);

            var invalidAttribute = _store.TryReplaceSnapshot(
                2,
                new long[] { 10 },
                new[]
                {
                    new BattleDiagnosticActorAttribute(
                        new BattleDiagnosticSessionScope("other", "world", 1),
                        2,
                        10,
                        1,
                        1f,
                        2f,
                        0)
                },
                new BattleDiagnosticActorAttributeModifier[0]);
            var orphanModifier = _store.TryReplaceSnapshot(
                2,
                new long[] { 10 },
                new[] { new BattleDiagnosticActorAttribute(_scope, 2, 10, 1, 1f, 2f, 0) },
                new[] { new BattleDiagnosticActorAttributeModifier(_scope, 2, 10, 2, 1, 3f, 0, 0, 0) });

            Assert.That(invalidAttribute, Is.False);
            Assert.That(orphanModifier, Is.False);
            Assert.That(_store.Revision, Is.EqualTo(1));
            Assert.That(_store.SnapshotFrame, Is.EqualTo(1));
            Assert.That(_store.QueryActorAttributes(1, 0, 10).Items[0].AttributeId, Is.EqualTo(1));
        }

        [Test]
        public void FreezeRejectsWritesAndClearAdvancesRevision()
        {
            _store.SetFrozen(true);
            Assert.That(_store.TryReplaceSnapshot(
                1,
                new long[] { 10 },
                new BattleDiagnosticActorAttribute[0],
                new BattleDiagnosticActorAttributeModifier[0]), Is.False);
            Assert.That(_store.Revision, Is.Zero);

            _store.SetFrozen(false);
            Assert.That(_store.TryReplaceSnapshot(
                1,
                new long[] { 10 },
                new BattleDiagnosticActorAttribute[0],
                new BattleDiagnosticActorAttributeModifier[0]), Is.True);
            _store.Clear();

            Assert.That(_store.Revision, Is.EqualTo(2));
            Assert.That(_store.SnapshotFrame, Is.EqualTo(BattleDiagnosticFrames.Invalid));
        }
    }

    public sealed class MobaBattleDiagnosticActorAttributeSessionTests
    {
        private BattleDiagnosticSessionScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("attributes", "world", 1);
        }

        [Test]
        public void SessionWithAttributeStore_DeclaresCapabilityAndRoutesQueries()
        {
            var collector = new MobaBattleDiagnosticEventCollector(
                _scope,
                16,
                () => 7,
                () => 1L);
            var attributeStore = new BattleDiagnosticActorAttributeStore(_scope);
            attributeStore.TryReplaceSnapshot(
                7,
                new long[] { 10 },
                new[] { new BattleDiagnosticActorAttribute(_scope, 7, 10, 1, 1f, 2f, 0, "Attack") },
                new BattleDiagnosticActorAttributeModifier[0]);
            var session = new MobaBattleDiagnosticLocalSession(
                collector.Store,
                collector.StateStore,
                null,
                attributeStore);

            var result = session.QueryActorAttributes(1, 0, 10);

            Assert.That(session.SessionInfo.Supports(BattleDiagnosticCapabilities.ActorAttributes), Is.True);
            Assert.That(session.ActorAttributeStoreRevision, Is.EqualTo(1));
            Assert.That(result.Items.Count, Is.EqualTo(1));
        }

        [Test]
        public void SessionWithoutAttributeStore_ReturnsUnsupported()
        {
            var collector = new MobaBattleDiagnosticEventCollector(
                _scope,
                16,
                () => 0,
                () => 1L);
            var session = new MobaBattleDiagnosticLocalSession(collector);

            var result = session.QueryActorAttributes(1, 0, 10);

            Assert.That(session.SessionInfo.Supports(BattleDiagnosticCapabilities.ActorAttributes), Is.False);
            Assert.That(result.Status.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.Unsupported));
        }
    }

    public sealed class MobaBattleDiagnosticAttributeHistoryTests
    {
        private BattleDiagnosticSessionScope _scope;
        private MobaBattleDiagnosticActorAttributeStore _store;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("attribute-history", "world", 1);
            _store = new MobaBattleDiagnosticActorAttributeStore(
                new MobaBattleDiagnosticEventCollector(_scope));
        }

        [Test]
        public void Track_RecordsAtConfiguredFrameIntervalAndReplacesSameFrame()
        {
            _store.Configure(10, 1, 3, true);
            for (var frame = 1; frame <= 7; frame++)
                _store.Record(Sample(frame, frame));
            _store.Record(Sample(7, 70));

            var samples = _store.GetSamples();
            Assert.That(samples.Count, Is.EqualTo(3));
            Assert.That(samples[0].Frame, Is.EqualTo(1));
            Assert.That(samples[1].Frame, Is.EqualTo(4));
            Assert.That(samples[2].Frame, Is.EqualTo(7));
            Assert.That(samples[2].FinalValue, Is.EqualTo(70));
        }

        [Test]
        public void Track_PauseSelectionChangeAndRewindResetHistory()
        {
            _store.Configure(10, 1, 2, true);
            _store.Record(Sample(10, 10));
            _store.Configure(10, 1, 2, false);
            _store.Record(Sample(12, 12));
            Assert.That(_store.GetSamples().Count, Is.EqualTo(1));

            _store.Configure(10, 1, 2, true);
            _store.Record(Sample(12, 12));
            _store.Record(Sample(2, 2));
            Assert.That(_store.GetSamples().Count, Is.EqualTo(1));
            Assert.That(_store.GetSamples()[0].Frame, Is.EqualTo(2));

            _store.Configure(10, 2, 2, true);
            Assert.That(_store.GetSamples(), Is.Empty);
            _store.Stop();
            Assert.That(_store.TryGetTrack(out _, out _, out _), Is.False);
        }

        [Test]
        public void Track_IgnoresOtherScopeAndKeepsBoundedHistory()
        {
            _store.Configure(10, 1, 1, true);
            _store.Record(new BattleDiagnosticActorAttribute(
                new BattleDiagnosticSessionScope("other", "world", 1),
                1, 10, 1, 0, 1, 0));
            for (var frame = 1; frame <= 300; frame++)
                _store.Record(Sample(frame, frame));

            var samples = _store.GetSamples();
            Assert.That(samples.Count, Is.EqualTo(240));
            Assert.That(samples[0].Frame, Is.EqualTo(61));
            Assert.That(samples[239].Frame, Is.EqualTo(300));
            _store.Clear();
            Assert.That(_store.GetSamples(), Is.Empty);
        }

        private BattleDiagnosticActorAttribute Sample(int frame, float value) =>
            new BattleDiagnosticActorAttribute(_scope, frame, 10, 1, 100, value, 1);
    }
}
