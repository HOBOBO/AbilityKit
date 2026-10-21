using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Flow.Battle.Hud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattleHudBuffCueFilterTests
    {
        private static PresentationCueData Cue(
            string ownerKind,
            string instanceKey,
            int targetActorId,
            PresentationCueStage stage,
            int stackCount = 1,
            int maxStackCount = 1)
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
                stackCount: stackCount,
                maxStackCount: maxStackCount,
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
        public void GameplayCounterCue_ShowsOneOfFiveAboveActor()
        {
            var root = new GameObject("counter-root", typeof(RectTransform));
            var textObject = new GameObject("counter-text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(root.transform, false);
            var view = root.AddComponent<BattleHudBuffIconView>();
            var stackText = textObject.GetComponent<Text>();
            view.Initialize(root.GetComponent<RectTransform>(), null, null, null, stackText, null);

            try
            {
                var cue = Cue("Buff", "gameplay-counter:1:10080001", 1, PresentationCueStage.Started, 1, 5);
                view.Apply(in cue, 0f);
                Assert.IsTrue(stackText.gameObject.activeSelf);
                Assert.AreEqual("1/5", stackText.text);

                var updated = Cue("Buff", "gameplay-counter:1:10080001", 1, PresentationCueStage.StackChanged, 2, 5);
                view.Apply(in updated, 0f);
                Assert.AreEqual("2/5", stackText.text);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void IsTagCue_StableTagKey_ReturnsTrueWithoutVfx()
        {
            var cue = Cue("Tag", "tag:1:1002", 1, PresentationCueStage.Started);
            Assert.IsTrue(BattleHudBuffCueFilter.IsTagCue(in cue));
            Assert.IsTrue(BattleHudBuffCueFilter.IsHudStatusCue(in cue));
        }

        [Test]
        public void TagPresentation_ExposesConfiguredHeadTextChannel()
        {
            var cue = Cue("Tag", "tag:1:1002", 1, PresentationCueStage.Started);
            Assert.IsTrue(BattleHudStatusCueFilter.TryResolveHeadText(in cue, out var text, out var color, out _));
            Assert.AreEqual("霸体", text);
            Assert.Greater(color.r, 0f);
        }

        [Test]
        public void TagPresentation_ExposesAirborneHeadTextChannel()
        {
            var cue = Cue("Tag", "tag:1:1504", 1, PresentationCueStage.Started);
            Assert.IsTrue(BattleHudStatusCueFilter.TryResolveHeadText(in cue, out var text, out _, out _));
            Assert.AreEqual("击飞", text);
        }

        [Test]
        public void IsTagCue_MissingStableKey_ReturnsFalse()
        {
            var cue = Cue("Tag", null, 1, PresentationCueStage.Started);
            Assert.IsFalse(BattleHudBuffCueFilter.IsTagCue(in cue));
        }

        [Test]
        public void IsTagCue_UnconfiguredDefinition_ReturnsFalse()
        {
            var cue = Cue("Tag", "tag:1:9999", 1, PresentationCueStage.Started);
            Assert.IsFalse(BattleHudStatusCueFilter.IsTagCue(in cue));
            Assert.IsFalse(BattleHudStatusCueFilter.IsHudStatusCue(in cue));
        }

        [Test]
        public void ResolveTagLabel_UsesSimpleParameterAndLocalizesCommonStates()
        {
            var cue = Cue("Tag", "tag:1:1002", 1, PresentationCueStage.Started);
            var withLabel = new PresentationCueData(
                stage: cue.Stage,
                cueKind: cue.CueKind,
                cueVfxId: null,
                cueSfxId: null,
                templateId: 0,
                vfxId: 0,
                sfxId: 0,
                requestKey: cue.RequestKey,
                sourceActorId: cue.SourceActorId,
                targetActorId: cue.TargetActorId,
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
                durationMsOverride: 0,
                scale: 1f,
                colorR: 1f,
                colorG: 1f,
                colorB: 1f,
                colorA: 1f,
                ownerKind: "Tag",
                instanceKey: cue.InstanceKey,
                stringParamKeys: new[] { "simple" },
                stringParamValues: new[] { "SuperArmor" });

            Assert.AreEqual("\u9738\u4f53", BattleHudBuffCueFilter.ResolveTagLabel(in withLabel));
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
        public void StatusLifecycle_UsesSharedCueSemantics()
        {
            Assert.IsTrue(BattleHudStatusCueFilter.IsStatusActiveStage(PresentationCueStage.Started));
            Assert.IsTrue(BattleHudStatusCueFilter.IsStatusActiveStage(PresentationCueStage.Refreshed));
            Assert.IsTrue(BattleHudStatusCueFilter.IsStatusActiveStage(PresentationCueStage.Ticked));
            Assert.IsTrue(BattleHudStatusCueFilter.IsStatusRemoveStage(PresentationCueStage.Removed));
            Assert.IsFalse(BattleHudStatusCueFilter.IsStatusActiveStage(PresentationCueStage.None));
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

        [Test]
        public void BaselineSession_RemovesOnlyStaleKeysForParticipatingActors()
        {
            var session = new BattleHudStatusBaselineSession();
            var reconciled = new Dictionary<int, HashSet<string>>();
            session.Begin();
            var actorOneKept = Cue("Tag", "tag:1:1002", 1, PresentationCueStage.Started);
            var actorTwoKept = Cue("Tag", "tag:2:1504", 2, PresentationCueStage.Started);
            var unrelatedActor = Cue("Skill", "skill:3:1", 3, PresentationCueStage.Executed);
            session.Observe(in actorOneKept);
            session.Observe(in actorTwoKept);
            session.Observe(in unrelatedActor);
            session.End((actorId, keys) => reconciled[actorId] = new HashSet<string>(keys, StringComparer.Ordinal));

            Assert.AreEqual(3, reconciled.Count);
            Assert.IsTrue(reconciled[1].Contains(actorOneKept.InstanceKey));
            Assert.IsTrue(reconciled[2].Contains(actorTwoKept.InstanceKey));
            Assert.AreEqual(0, reconciled[3].Count);
            Assert.IsFalse(session.IsActive);
        }

        [Test]
        public void BaselineSession_DuplicateSourcesCollapseToOneStableKey()
        {
            var session = new BattleHudStatusBaselineSession();
            session.Begin();
            var first = Cue("Tag", "tag:1:1002", 1, PresentationCueStage.Started);
            var duplicate = Cue("Tag", "tag:1:1002", 1, PresentationCueStage.Started);
            session.Observe(in first);
            session.Observe(in duplicate);
            Assert.IsTrue(session.Contains(1, "tag:1:1002"));
            var calls = 0;
            session.End((_, keys) => { calls++; Assert.AreEqual(1, keys.Count); });
            Assert.AreEqual(1, calls);
        }
    }
}
