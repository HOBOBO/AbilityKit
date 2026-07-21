using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.View.Lib.Skill;
using AbilityKit.Game.Flow;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattleHudSkillButtonTemplateResolverTests
    {
        [Test]
        public void ConfiguredLoadout_ResolvesTemplateDrivenSlotsAndAppendedBasicAttack()
        {
            var resolver = new BattleHudSkillButtonTemplateResolver();
            var loadout = new MobaPlayerLoadout(
                playerId: new PlayerId("mozi_player"),
                teamId: 1,
                heroId: 1004,
                attributeTemplateId: 1004,
                level: 1,
                basicAttackSkillId: 10040011,
                skillIds: new[] { 10040101, 10040201, 10040301 },
                spawnIndex: 0);

            Assert.AreEqual(4, resolver.ResolveSkillButtonCount(loadout));

            AssertResolvedSlot(
                resolver,
                loadout,
                slot: 1,
                expectedSkillId: 10040101,
                expectedPreviewShape: BattleHudSkillPreviewShape.DirectionLine,
                expectedIndicatorShape: SkillAimIndicatorShape.DirectionLine);
            AssertResolvedSlot(
                resolver,
                loadout,
                slot: 2,
                expectedSkillId: 10040201,
                expectedPreviewShape: BattleHudSkillPreviewShape.DirectionLine,
                expectedIndicatorShape: SkillAimIndicatorShape.DirectionLine);
            AssertResolvedSlot(
                resolver,
                loadout,
                slot: 3,
                expectedSkillId: 10040301,
                expectedPreviewShape: BattleHudSkillPreviewShape.None,
                expectedIndicatorShape: SkillAimIndicatorShape.Hidden);

            AssertResolvedSlot(
                resolver,
                loadout,
                slot: 4,
                expectedSkillId: 10040011,
                expectedPreviewShape: BattleHudSkillPreviewShape.None,
                expectedIndicatorShape: SkillAimIndicatorShape.Hidden);

            Assert.IsFalse(resolver.TryResolveSkill(loadout, 5, out _, out _, out _));
        }

        [Test]
        public void DajiSkill1_ResolvesDirectionalAreaWithoutChangingDirectionLineSkills()
        {
            var resolver = new BattleHudSkillButtonTemplateResolver();
            var daji = CreateLoadout("daji", 1005, 10050001, 10050101, 10050201, 10050301);
            var mozi = CreateLoadout("mozi", 1004, 10040011, 10040101, 10040201, 10040301);

            Assert.IsTrue(resolver.TryResolveSkill(daji, 1, out _, out var template, out var dajiSpec));
            Assert.AreEqual(4, template.Id);
            Assert.AreEqual(BattleHudSkillPreviewShape.DirectionArea, dajiSpec.PreviewShape);
            Assert.AreEqual(SkillAimIndicatorShape.DirectionArea, dajiSpec.IndicatorShape);
            Assert.AreEqual(12f, dajiSpec.Range, 0.0001f);
            Assert.AreEqual(2f, dajiSpec.Width, 0.0001f);
            Assert.AreEqual(264f, dajiSpec.UiLengthPixels, 0.0001f);
            Assert.AreEqual(48f, dajiSpec.UiWidthPixels, 0.0001f);

            Assert.IsTrue(resolver.TryResolveSkill(mozi, 2, out _, out _, out var moziSpec));
            Assert.AreEqual(BattleHudSkillPreviewShape.DirectionLine, moziSpec.PreviewShape);
            Assert.AreEqual(SkillAimIndicatorShape.DirectionLine, moziSpec.IndicatorShape);
        }

        [Test]
        public void TargetPointHeroes_ResolveSharedAimSemanticsFromTemplate()
        {
            var resolver = new BattleHudSkillButtonTemplateResolver();
            var lianPo = CreateLoadout("lian_po", 1001, 10010011, 10010101, 10010201, 10010301);
            var xiaoQiao = CreateLoadout("xiao_qiao", 1002, 10020011, 10020101, 10020201, 10020301);

            AssertTargetPointSlot(resolver, lianPo, slot: 3, expectedSkillId: 10010301);
            AssertTargetPointSlot(resolver, xiaoQiao, slot: 2, expectedSkillId: 10020201);
        }

        [Test]
        public void PlayerLoadoutResolver_SwitchesBetweenHeroesWithoutUsingResponsePlayer()
        {
            var lianPo = CreateLoadout("lian_po", 1001, 10010011, 10010101, 10010201, 10010301);
            var xiaoQiao = CreateLoadout("xiao_qiao", 1002, 10020011, 10020101, 10020201, 10020301);
            var response = new EnterMobaGameRes(
                new WorldId("test"),
                new PlayerId("lian_po"),
                localActorId: 1001,
                randomSeed: 1,
                tickRate: 30,
                inputDelayFrames: 0,
                playersLoadout: new[] { lianPo, xiaoQiao });
            var resolver = new BattleHudSkillButtonTemplateResolver();

            Assert.IsTrue(resolver.TryFindLoadout(response, "xiao_qiao", out var selectedXiaoQiao));
            Assert.AreEqual(1002, selectedXiaoQiao.HeroId);
            AssertTargetPointSlot(resolver, selectedXiaoQiao, slot: 2, expectedSkillId: 10020201);

            Assert.IsTrue(resolver.TryFindLoadout(response, "lian_po", out var selectedLianPo));
            Assert.AreEqual(1001, selectedLianPo.HeroId);
            AssertTargetPointSlot(resolver, selectedLianPo, slot: 3, expectedSkillId: 10010301);

            Assert.IsFalse(resolver.TryFindLoadout(response, "missing_player", out _));
        }

        [Test]
        public void DynamicHud_TargetPointHeroesShowIndicatorAndPublishPreviewAim()
        {
            var root = new GameObject("TargetPointHudTests.Root", typeof(RectTransform), typeof(Canvas));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(1920f, 1080f);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var context = BattleContext.Rent();
            var controller = new BattleHudInputController(context, rootRect, canvas, null);

            var lianPo = CreateLoadout("lian_po", 1001, 10010011, 10010101, 10010201, 10010301);
            var xiaoQiao = CreateLoadout("xiao_qiao", 1002, 10020011, 10020101, 10020201, 10020301);
            var response = new EnterMobaGameRes(
                new WorldId("test"),
                new PlayerId("lian_po"),
                localActorId: 1001,
                randomSeed: 1,
                tickRate: 30,
                inputDelayFrames: 0,
                playersLoadout: new[] { lianPo, xiaoQiao });

            try
            {
                controller.Ensure();
                controller.ApplySkillButtonTemplates(response, "lian_po");
                AssertTargetPointButtonPublishesAim(controller, context, actorId: 1001, slot: 3, skillId: 10010301);

                controller.ApplySkillButtonTemplates(response, "missing_player");
                Assert.AreEqual(10010301, controller.SkillSpecs[3].SkillId);
                Assert.AreEqual(SkillAimIndicatorShape.TargetCircle, controller.InputUi.SkillViews[2].Config.IndicatorShape);

                controller.ApplySkillStates(new[]
                {
                    new MobaSkillStateSnapshotEntry
                    {
                        ActorId = 1001,
                        Slot = 2,
                        SkillId = 10010201,
                        Level = 1,
                        Availability = MobaSkillAvailabilityState.Disabled,
                        DisableReason = 1,
                    }
                }, 1001);

                var lianPoSkill3 = controller.InputUi.SkillViews[2];
                var lianPoSkill3Rect = (RectTransform)lianPoSkill3.transform;
                var lianPoSkill3Pointer = RectTransformUtility.WorldToScreenPoint(null, lianPoSkill3Rect.position) + Vector2.right * 100f;
                lianPoSkill3.OnPointerDown(new PointerEventData(EventSystem.current)
                {
                    pointerId = 3,
                    position = lianPoSkill3Pointer,
                });
                var skill3Indicator = controller.InputUi.Root.transform.Find("Skill3AimIndicator");
                Assert.IsTrue(skill3Indicator.gameObject.activeSelf);

                controller.ApplySkillButtonTemplates(response, "xiao_qiao");

                Assert.IsFalse(controller.InputUi.SkillViews[2].Config.EnableAim);
                Assert.AreEqual(SkillAimIndicatorShape.Hidden, controller.InputUi.SkillViews[2].Config.IndicatorShape);
                Assert.IsFalse(skill3Indicator.gameObject.activeSelf);
                AssertTargetPointButtonPublishesAim(controller, context, actorId: 1002, slot: 2, skillId: 10020201);
            }
            finally
            {
                controller.Dispose();
                BattleContext.Return(context);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ControlPlayerChange_RebindsXiaoQiaoSlotsOnNextHudCheck()
        {
            var root = new GameObject("ControlPlayerChangeHudTests.Root", typeof(RectTransform), typeof(Canvas));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(1920f, 1080f);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var context = BattleContext.Rent();
            var controller = new BattleHudInputController(context, rootRect, canvas, null);
            var binding = new BattleHudSkillTemplateBindingState();
            var lianPo = CreateLoadout("p1", 1001, 10010011, 10010101, 10010201, 10010301);
            var xiaoQiao = CreateLoadout("p2", 1002, 10020011, 10020101, 10020201, 10020301);
            var response = new EnterMobaGameRes(
                new WorldId("test"),
                new PlayerId("p1"),
                localActorId: 1001,
                randomSeed: 1,
                tickRate: 30,
                inputDelayFrames: 0,
                playersLoadout: new[] { lianPo, xiaoQiao });

            try
            {
                controller.Ensure();
                Assert.IsTrue(binding.RequiresBinding("p1", loadoutRevision: 0));
                Assert.IsTrue(controller.ApplySkillButtonTemplates(response, "p1"));
                binding.MarkBound("p1", loadoutRevision: 0);
                Assert.IsFalse(binding.RequiresBinding("p1", loadoutRevision: 0));
                Assert.IsTrue(
                    binding.RequiresBinding("p1", loadoutRevision: 1),
                    "same player must rebind after an authoritative hero loadout change");
                Assert.AreEqual(SkillAimIndicatorShape.TargetCircle, controller.InputUi.SkillViews[2].Config.IndicatorShape);

                context.LocalControlPlayerId = "p2";
                context.LocalActorId = 1002;

                Assert.IsTrue(binding.RequiresBinding(context.ResolveLocalControlPlayerId(), loadoutRevision: 1));
                Assert.IsTrue(controller.ApplySkillButtonTemplates(response, context.ResolveLocalControlPlayerId()));
                binding.MarkBound(context.ResolveLocalControlPlayerId(), loadoutRevision: 1);

                var skill2 = controller.InputUi.SkillViews[1].Config;
                Assert.IsTrue(skill2.EnableAim);
                Assert.AreEqual(SkillAimMode.Point, skill2.AimMode);
                Assert.AreEqual(SkillUsePointMode.TargetPoint, skill2.UsePointMode);
                Assert.AreEqual(SkillAimIndicatorShape.TargetCircle, skill2.IndicatorShape);
                Assert.AreEqual(10020201, controller.SkillSpecs[2].SkillId);

                var skill3 = controller.InputUi.SkillViews[2].Config;
                Assert.IsFalse(skill3.EnableAim);
                Assert.AreEqual(SkillAimIndicatorShape.Hidden, skill3.IndicatorShape);
                Assert.AreEqual(10020301, controller.SkillSpecs[3].SkillId);
                Assert.IsFalse(binding.RequiresBinding("P2", loadoutRevision: 1));
            }
            finally
            {
                controller.Dispose();
                BattleContext.Return(context);
                Object.DestroyImmediate(root);
            }
        }

        private static void AssertTargetPointButtonPublishesAim(
            BattleHudInputController controller,
            BattleContext context,
            int actorId,
            int slot,
            int skillId)
        {
            var view = controller.InputUi.SkillViews[slot - 1];
            controller.ApplySkillStates(new[]
            {
                new MobaSkillStateSnapshotEntry
                {
                    ActorId = actorId,
                    Slot = slot,
                    SkillId = skillId,
                    Level = 1,
                    Availability = MobaSkillAvailabilityState.Available,
                }
            }, actorId);

            Assert.IsTrue(view.Config.EnableAim);
            Assert.AreEqual(SkillAimMode.Point, view.Config.AimMode);
            Assert.AreEqual(SkillUsePointMode.TargetPoint, view.Config.UsePointMode);
            Assert.AreEqual(SkillAimIndicatorShape.TargetCircle, view.Config.IndicatorShape);

            var buttonRect = (RectTransform)view.transform;
            var pointerPosition = RectTransformUtility.WorldToScreenPoint(null, buttonRect.position) + Vector2.right * 100f;
            view.OnPointerDown(new PointerEventData(EventSystem.current)
            {
                pointerId = slot,
                position = pointerPosition,
            });

            var indicator = controller.InputUi.Root.transform.Find("Skill" + slot + "AimIndicator");
            Assert.IsNotNull(indicator);
            Assert.IsTrue(indicator.gameObject.activeSelf);
            Assert.IsTrue(context.TryReadHudSkillAimPreview(out var previewSlot, out var dx, out var dz, out _));
            Assert.AreEqual(slot, previewSlot);
            Assert.Greater(new Vector2(dx, dz).magnitude, 0f);
            Assert.LessOrEqual(new Vector2(dx, dz).magnitude, controller.SkillSpecs[slot].Range + 0.0001f);

            view.OnPointerUp(new PointerEventData(EventSystem.current)
            {
                pointerId = slot,
                position = pointerPosition,
            });
        }

        private static MobaPlayerLoadout CreateLoadout(
            string playerId,
            int heroId,
            int basicAttackSkillId,
            params int[] skillIds)
        {
            return new MobaPlayerLoadout(
                new PlayerId(playerId),
                teamId: 1,
                heroId: heroId,
                attributeTemplateId: heroId,
                level: 1,
                basicAttackSkillId: basicAttackSkillId,
                skillIds: skillIds,
                spawnIndex: 0);
        }

        private static void AssertTargetPointSlot(
            BattleHudSkillButtonTemplateResolver resolver,
            in MobaPlayerLoadout loadout,
            int slot,
            int expectedSkillId)
        {
            Assert.IsTrue(resolver.TryResolveSkill(loadout, slot, out _, out _, out var spec));
            Assert.AreEqual(expectedSkillId, spec.SkillId);
            Assert.AreEqual(BattleHudSkillPreviewShape.TargetCircle, spec.PreviewShape);
            Assert.AreEqual(SkillAimIndicatorShape.TargetCircle, spec.IndicatorShape);
            Assert.IsTrue(spec.EnableAim);
            Assert.AreEqual(SkillAimMode.Point, spec.AimMode);
            Assert.AreEqual(SkillUsePointMode.TargetPoint, spec.UsePointMode);
            Assert.IsTrue(spec.UsesTargetPoint);
            Assert.Greater(spec.Range, 0f);
            Assert.Greater(spec.Radius, 0f);
        }

        private static void AssertResolvedSlot(
            BattleHudSkillButtonTemplateResolver resolver,
            in MobaPlayerLoadout loadout,
            int slot,
            int expectedSkillId,
            BattleHudSkillPreviewShape expectedPreviewShape,
            SkillAimIndicatorShape expectedIndicatorShape)
        {
            Assert.IsTrue(resolver.TryResolveSkill(loadout, slot, out var skill, out _, out var spec), $"slot {slot} should resolve.");
            Assert.IsNotNull(skill, $"slot {slot} should resolve a concrete skill config.");
            Assert.AreEqual(expectedSkillId, skill.Id);
            Assert.AreEqual(expectedSkillId, spec.SkillId);
            Assert.AreEqual(expectedPreviewShape, spec.PreviewShape);
            Assert.AreEqual(expectedIndicatorShape, spec.IndicatorShape);
        }
    }
}
