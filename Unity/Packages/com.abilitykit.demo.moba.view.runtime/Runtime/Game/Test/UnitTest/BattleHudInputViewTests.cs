using System.Collections;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Battle.View;
using AbilityKit.Game.Battle.View.Lib.Skill;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattleHudInputViewTests
    {
        [Test]
        public void SkillClick_ForwardsSlotFromInitializedSkillButton()
        {
            using (var fixture = BattleHudInputViewFixture.Create())
            {
                var clickCount = 0;
                var clickedSlot = 0;
                fixture.InputView.SkillClick += slot =>
                {
                    clickCount++;
                    clickedSlot = slot;
                };

                fixture.Click(fixture.Skill2);

                Assert.AreEqual(1, clickCount);
                Assert.AreEqual(2, clickedSlot);
            }
        }

        [Test]
        public void Initialize_WithSameControls_DoesNotDuplicateSkillSubscriptions()
        {
            using (var fixture = BattleHudInputViewFixture.Create())
            {
                var clickCount = 0;
                fixture.InputView.SkillClick += _ => clickCount++;

                fixture.InputView.Initialize(null, fixture.Skill1, fixture.Skill2, fixture.Skill3);
                fixture.InputView.Initialize(null, fixture.Skill1, fixture.Skill2, fixture.Skill3);

                fixture.Click(fixture.Skill1);

                Assert.AreEqual(1, clickCount);
            }
        }

        [Test]
        public void Disable_UnhooksSkillSubscriptionsUntilReenabled()
        {
            using (var fixture = BattleHudInputViewFixture.Create())
            {
                var clickCount = 0;
                fixture.InputView.SkillClick += _ => clickCount++;

                fixture.InputView.enabled = false;
                fixture.Click(fixture.Skill1);

                fixture.InputView.enabled = true;
                fixture.Click(fixture.Skill1);

                Assert.AreEqual(1, clickCount);
            }
        }

        [Test]
        public void SkillPreviewRelease_IsRaisedOnceForClick()
        {
            using (var fixture = BattleHudInputViewFixture.Create())
            {
                var config = fixture.Skill1.Config;
                config.PreviewVfxId = 9001;
                fixture.Skill1.Config = config;
                var releaseCount = 0;
                fixture.Skill1.OnPreviewReleased += () => releaseCount++;

                fixture.Click(fixture.Skill1);

                Assert.AreEqual(1, releaseCount);
            }
        }

        [Test]
        public void SkillPreviewRelease_CleansUpWhenSkillBecomesUnavailableWhilePressed()
        {
            using (var fixture = BattleHudInputViewFixture.Create())
            {
                var config = fixture.Skill1.Config;
                config.PreviewVfxId = 9001;
                fixture.Skill1.Config = config;
                var releaseCount = 0;
                fixture.Skill1.OnPreviewReleased += () => releaseCount++;

                fixture.Skill1.OnPointerDown(BattleHudInputViewFixture.Pointer(1, new Vector2(10f, 10f)));
                fixture.Skill1.ApplySkillState(new SkillStateData(
                    actorId: 1,
                    slot: 1,
                    skillId: 10010101,
                    availability: SkillAvailabilityState.Disabled));
                fixture.Skill1.OnPointerUp(BattleHudInputViewFixture.Pointer(1, new Vector2(10f, 10f)));

                Assert.AreEqual(1, releaseCount);
            }
        }

        [UnityTest]
        public IEnumerator SkillLongPress_ForwardsSlotAndSuppressesClick()
        {
            using (var fixture = BattleHudInputViewFixture.Create())
            {
                var longPressCount = 0;
                var longPressedSlot = 0;
                var clickCount = 0;
                fixture.InputView.SkillLongPress += slot =>
                {
                    longPressCount++;
                    longPressedSlot = slot;
                };
                fixture.InputView.SkillClick += _ => clickCount++;

                fixture.Skill3.OnPointerDown(BattleHudInputViewFixture.Pointer(3, new Vector2(30f, 30f)));
                yield return null;
                fixture.Skill3.OnPointerUp(BattleHudInputViewFixture.Pointer(3, new Vector2(30f, 30f)));

                Assert.AreEqual(1, longPressCount);
                Assert.AreEqual(3, longPressedSlot);
                Assert.AreEqual(0, clickCount);
            }
        }

        private sealed class BattleHudInputViewFixture : System.IDisposable
        {
            private readonly GameObject _root;

            private BattleHudInputViewFixture(GameObject root, BattleHudInputView inputView, SkillButtonView skill1, SkillButtonView skill2, SkillButtonView skill3)
            {
                _root = root;
                InputView = inputView;
                Skill1 = skill1;
                Skill2 = skill2;
                Skill3 = skill3;
            }

            public BattleHudInputView InputView { get; }
            public SkillButtonView Skill1 { get; }
            public SkillButtonView Skill2 { get; }
            public SkillButtonView Skill3 { get; }

            public static BattleHudInputViewFixture Create()
            {
                var root = new GameObject("BattleHudInputViewTests.Root", typeof(RectTransform));
                var inputView = root.AddComponent<BattleHudInputView>();
                var skill1 = CreateSkill(root.transform, "Skill1");
                var skill2 = CreateSkill(root.transform, "Skill2");
                var skill3 = CreateSkill(root.transform, "Skill3");

                inputView.Initialize(null, skill1, skill2, skill3);
                return new BattleHudInputViewFixture(root, inputView, skill1, skill2, skill3);
            }

            public static PointerEventData Pointer(int pointerId, Vector2 position)
            {
                return new PointerEventData(EventSystem.current)
                {
                    pointerId = pointerId,
                    position = position
                };
            }

            public void Click(SkillButtonView skill)
            {
                skill.OnPointerDown(Pointer(1, new Vector2(10f, 10f)));
                skill.OnPointerUp(Pointer(1, new Vector2(10f, 10f)));
            }

            public void Dispose()
            {
                Object.DestroyImmediate(_root);
            }

            private static SkillButtonView CreateSkill(Transform parent, string name)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var rect = (RectTransform)go.transform;
                var skill = go.AddComponent<SkillButtonView>();
                var config = SkillButtonConfig.Default;
                config.LongPressSeconds = 0.0001f;
                config.EnableAim = false;
                skill.Initialize(rect, parent as RectTransform, null, config);
                return skill;
            }
        }
    }
}
