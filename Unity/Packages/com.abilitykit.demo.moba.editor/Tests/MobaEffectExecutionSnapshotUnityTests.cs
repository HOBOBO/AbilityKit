using AbilityKit.Context;
using AbilityKit.Demo.Moba.Services;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaEffectExecutionSnapshotUnityTests
    {
        [Test]
        public void EntrySnapshot_IsDetachedAndPreservesZeroStagePresence()
        {
            using (var trace = new MobaTraceRegistry())
            using (var snapshots = new MobaEffectExecutionSnapshotStore(trace, 2, () => true))
            {
                var id = trace.CreateRootContext(MobaTraceKind.EffectExecution, 101);
                var payload = new StagePayload();
                var context = new MobaCombatExecutionContext(payload, default, default, default, default, 10);
                snapshots.OnExecutionStarted(id, 101, 201, in context);
                var reference = Reference(trace, id);
                payload.StackCount = 99;
                snapshots.OnExecutionStarted(id, 102, 202, in context);
                var facts = snapshots.Read(reference);
                Assert.That(facts.IsCaptured, Is.True);
                Assert.That(facts.HasStageSnapshot, Is.True);
                Assert.That(facts.StackCount, Is.Zero);
                Assert.That(facts.Frame, Is.EqualTo(10));
                Assert.That(facts.EffectConfigId, Is.EqualTo(101));
                Assert.That(payload.ReadCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void EntryCapture_RespectsModeChannelsAndFreeze()
        {
            using (var trace = new MobaTraceRegistry())
            {
                var collector = new MobaBattleDiagnosticEventCollector(new BattleDiagnosticSessionScope("facts", "world", 1));
                using (var snapshots = new MobaEffectExecutionSnapshotStore(trace, collector))
                {
                    collector.CaptureMode = BattleDiagnosticCaptureMode.Metrics;
                    Assert.That(snapshots.IsEnabled, Is.False);
                    collector.CaptureMode = BattleDiagnosticCaptureMode.Events;
                    collector.EnabledChannels = BattleDiagnosticEventChannel.Skill;
                    Assert.That(snapshots.IsEnabled, Is.True);
                    collector.SetFrozen(true);
                    Assert.That(snapshots.IsEnabled, Is.False);
                    collector.SetFrozen(false);
                    collector.EnabledChannels = BattleDiagnosticEventChannel.None;
                    Assert.That(snapshots.IsEnabled, Is.False);
                }
            }
        }

        [Test]
        public void EntryHistory_EvictionAndTraceCleanupNeverReadCurrentData()
        {
            using (var trace = new MobaTraceRegistry())
            using (var snapshots = new MobaEffectExecutionSnapshotStore(trace, 1, () => true))
            {
                var first = trace.CreateRootContext(MobaTraceKind.EffectExecution, 101);
                var context = new MobaCombatExecutionContext(new StagePayload(), default, default, default, default, 10);
                snapshots.OnExecutionStarted(first, 101, 201, in context);
                var old = Reference(trace, first);
                var second = trace.CreateRootContext(MobaTraceKind.EffectExecution, 101);
                snapshots.OnExecutionStarted(second, 101, 201, in context);
                var current = Reference(trace, second);
                Assert.That(snapshots.Read(old).Availability, Is.EqualTo(BattleDiagnosticDataAvailability.Evicted));
                Assert.That(snapshots.Read(current).IsCaptured, Is.True);
                trace.PurgeRoot(second);
                Assert.That(snapshots.Read(current).Availability, Is.EqualTo(BattleDiagnosticDataAvailability.Evicted));
            }
        }

        private static ContextSnapshotReference Reference(MobaTraceRegistry trace, long id)
        {
            Assert.That(trace.TryGetNodeSnapshot(id, out var node), Is.True);
            return ((MobaTraceMetadata)node.Metadata).ExecutionSnapshot;
        }

        private sealed class StagePayload : IMobaTriggerStageSnapshotProvider
        {
            public int StackCount;
            public int ReadCount;
            public bool TryGetStageSnapshot(out MobaTriggerStageSnapshot snapshot)
            {
                ReadCount++;
                snapshot = new MobaTriggerStageSnapshot(StackCount);
                return true;
            }
        }
    }
}
