using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class ViewEventSourceModePolicyTests
    {
        [Test]
        public void Resolve_DefaultsToSnapshotOnly_WhenContextIsNull()
        {
            var policy = new ViewEventSourceModePolicy();

            var mode = policy.Resolve(null);

            Assert.AreEqual(BattleViewEventSourceMode.SnapshotOnly, mode);
        }

        [Test]
        public void ShouldUseTriggerAdapter_MatchesTriggerAndHybridModes()
        {
            var policy = new ViewEventSourceModePolicy();

            Assert.IsFalse(policy.ShouldUseTriggerAdapter(BattleViewEventSourceMode.SnapshotOnly));
            Assert.IsTrue(policy.ShouldUseTriggerAdapter(BattleViewEventSourceMode.TriggerOnly));
            Assert.IsTrue(policy.ShouldUseTriggerAdapter(BattleViewEventSourceMode.Hybrid));
        }

        [Test]
        public void ShouldUseSnapshotAdapter_MatchesSnapshotAndHybridModes()
        {
            var policy = new ViewEventSourceModePolicy();

            Assert.IsTrue(policy.ShouldUseSnapshotAdapter(BattleViewEventSourceMode.SnapshotOnly));
            Assert.IsFalse(policy.ShouldUseSnapshotAdapter(BattleViewEventSourceMode.TriggerOnly));
            Assert.IsTrue(policy.ShouldUseSnapshotAdapter(BattleViewEventSourceMode.Hybrid));
        }

        [Test]
        public void HybridDamagePresentation_HasExactlyOneTransientOwner()
        {
            Assert.IsFalse(BattleDamagePresentationSourcePolicy.ShouldPresentTrigger(BattleViewEventSourceMode.Hybrid));
            Assert.IsTrue(BattleDamagePresentationSourcePolicy.ShouldPresentSnapshot(BattleViewEventSourceMode.Hybrid));
            Assert.IsTrue(BattleDamagePresentationSourcePolicy.ShouldPresentSnapshot(BattleViewEventSourceMode.SnapshotOnly));
            Assert.IsFalse(BattleDamagePresentationSourcePolicy.ShouldPresentTrigger(BattleViewEventSourceMode.SnapshotOnly));
        }
    }
}
