using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Flow.Battle.Hud;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattleHudBuffCueFilterTests
    {
        private static PresentationCueData Cue(
            string ownerKind,
            string instanceKey,
            int targetActorId,
            PresentationCueStage stage)
        {
            return new PresentationCueData(
                stage: stage,
                cueKind: ownerKind,
                cueVfxId: null,
                cueSfxId: null,
                templateId: 7,
                vfxId: 0,
                sfxId: 0,
                requestKey: instanceKey,
                sourceActorId: 1,
                targetActorId: targetActorId,
                triggerEventId: 0,
                triggerEventName: null,
                triggerId: 0,
                phase: 0,
                priority: 0,
                order: 0,
                actionIndex: 0,
                interruptReason: 0,
                interruptSourceName: null,
                interruptTriggerId: 0,
                interruptConditionPassed: false,
                targets: null,
                positions: null,
                offsetX: 0f,
                offsetY: 0f,
                offsetZ: 0f,
                durationMsOverride: 6000,
                scale: 1f,
                colorR: 1f,
                colorG: 1f,
                colorB: 1f,
                colorA: 1f,
                ownerKind: ownerKind,
                instanceKey: instanceKey,
                stackCount: 1,
                maxStackCount: 1,
                elapsedSeconds: 1f,
                remainingSeconds: 5f);
        }

        [Test]
        public void IsBuffCue_BuffOwnerKind_ReturnsTrue()
        {
            var cue = Cue("Buff", "buff:1:42:99", 1, PresentationCueStage.Started);
            Assert.IsTrue(BattleHudBuffCueFilter.IsBuffCue(in cue));
        }

        [Test]
        public void IsBuffCue_NonBuffOwnerKind_ReturnsFalse()
        {
            var cue = Cue("Skill", "skill:1:1:99", 1, PresentationCueStage.Started);
            Assert.IsFalse(BattleHudBuffCueFilter.IsBuffCue(in cue));
        }

        [Test]
        public void IsBuffCue_MissingInstanceKey_ReturnsFalse()
        {
            var cue = Cue("Buff", null, 1, PresentationCueStage.Started);
            Assert.IsFalse(BattleHudBuffCueFilter.IsBuffCue(in cue));
        }

        [Test]
        public void IsBuffCue_MissingTargetActor_ReturnsFalse()
        {
            var cue = Cue("Buff", "buff:0:42:99", 0, PresentationCueStage.Started);
            Assert.IsFalse(BattleHudBuffCueFilter.IsBuffCue(in cue));
        }

        [Test]
        public void IsBuffActiveStage_TrueForLifecycleStages()
        {
            Assert.IsTrue(BattleHudBuffCueFilter.IsBuffActiveStage(PresentationCueStage.Started));
            Assert.IsTrue(BattleHudBuffCueFilter.IsBuffActiveStage(PresentationCueStage.Refreshed));
            Assert.IsTrue(BattleHudBuffCueFilter.IsBuffActiveStage(PresentationCueStage.Ticked));
            Assert.IsTrue(BattleHudBuffCueFilter.IsBuffActiveStage(PresentationCueStage.StackChanged));
        }

        [Test]
        public void IsBuffActiveStage_FalseForTerminalStages()
        {
            Assert.IsFalse(BattleHudBuffCueFilter.IsBuffActiveStage(PresentationCueStage.Expired));
            Assert.IsFalse(BattleHudBuffCueFilter.IsBuffActiveStage(PresentationCueStage.Removed));
        }

        [Test]
        public void IsBuffRemoveStage_TrueForTerminalStages()
        {
            Assert.IsTrue(BattleHudBuffCueFilter.IsBuffRemoveStage(PresentationCueStage.Expired));
            Assert.IsTrue(BattleHudBuffCueFilter.IsBuffRemoveStage(PresentationCueStage.Removed));
            Assert.IsTrue(BattleHudBuffCueFilter.IsBuffRemoveStage(PresentationCueStage.Completed));
            Assert.IsTrue(BattleHudBuffCueFilter.IsBuffRemoveStage(PresentationCueStage.Interrupted));
        }

        [Test]
        public void IsBuffRemoveStage_FalseForActiveStages()
        {
            Assert.IsFalse(BattleHudBuffCueFilter.IsBuffRemoveStage(PresentationCueStage.Started));
            Assert.IsFalse(BattleHudBuffCueFilter.IsBuffRemoveStage(PresentationCueStage.Ticked));
        }

        [Test]
        public void CollectBuffCues_KeepsOnlyBuffCues()
        {
            var entries = new List<PresentationCueData>
            {
                Cue("Buff", "buff:1:42:99", 1, PresentationCueStage.Started),
                Cue("Skill", "skill:1:1:1", 1, PresentationCueStage.Executed),
                Cue("Buff", "buff:2:43:99", 2, PresentationCueStage.Refreshed),
                Cue("Buff", null, 1, PresentationCueStage.Started) // dropped (no key)
            };
            var buffer = new List<PresentationCueData>();

            BattleHudBuffCueFilter.CollectBuffCues(entries, buffer);

            Assert.AreEqual(2, buffer.Count);
            Assert.AreEqual("Buff", buffer[0].OwnerKind);
            Assert.AreEqual("Buff", buffer[1].OwnerKind);
        }

        [Test]
        public void CollectBuffCues_NullSource_ProducesEmptyBuffer()
        {
            var buffer = new List<PresentationCueData>();
            BattleHudBuffCueFilter.CollectBuffCues(null, buffer);
            Assert.AreEqual(0, buffer.Count);
        }
    }
}
