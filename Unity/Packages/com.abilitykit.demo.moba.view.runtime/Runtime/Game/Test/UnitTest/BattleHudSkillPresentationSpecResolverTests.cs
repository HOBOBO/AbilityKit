using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Game.Battle.View.Lib.Skill;
using AbilityKit.Game.Flow;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    /// <summary>
    /// BattleHudSkillPresentationSpecResolver 的形状覆盖测试：
    /// 验证 9 种 IndicatorShape（含 Hidden）都能正确映射到 PreviewShape/IndicatorShape，
    /// 并把模板里的几何参数写入 BattleHudSkillPresentationSpec 的对应字段。
    /// </summary>
    public sealed class BattleHudSkillPresentationSpecResolverTests
    {
        private const int TestSkillId = 900101;
        private const int TestRange = 8;

        [Test]
        public void Hidden_IndicatorShape_ReturnsHiddenSpec()
        {
            var (resolver, skill, template) = BuildResolver(indicatorShape: 0);

            var spec = resolver.Resolve(skill, template);

            Assert.AreEqual(BattleHudSkillPreviewShape.None, spec.PreviewShape);
            Assert.AreEqual(SkillAimIndicatorShape.Hidden, spec.IndicatorShape);
            Assert.IsFalse(spec.EnableAim);
        }

        [Test]
        public void DisabledTemplate_ReturnsHiddenEvenIfIndicatorShapeSet()
        {
            var (resolver, skill, template) = BuildResolver(indicatorShape: 1, enableAim: false);

            var spec = resolver.Resolve(skill, template);

            Assert.AreEqual(BattleHudSkillPreviewShape.None, spec.PreviewShape);
            Assert.AreEqual(SkillAimIndicatorShape.Hidden, spec.IndicatorShape);
        }

        [Test]
        public void DirectionLine_MapsToDirectionLinePreview()
        {
            var (resolver, skill, template) = BuildResolver(indicatorShape: 1, indicatorWorldWidth: 1.5f);

            var spec = resolver.Resolve(skill, template);

            Assert.AreEqual(BattleHudSkillPreviewShape.DirectionLine, spec.PreviewShape);
            Assert.AreEqual(SkillAimIndicatorShape.DirectionLine, spec.IndicatorShape);
            Assert.AreEqual(TestRange, spec.Range, 0.0001f);
            Assert.AreEqual(1.5f, spec.Width, 0.0001f);
        }

        [Test]
        public void TargetCircle_MapsToTargetCirclePreview()
        {
            var (resolver, skill, template) = BuildResolver(indicatorShape: 2, indicatorWorldWidth: 3.2f);

            var spec = resolver.Resolve(skill, template);

            Assert.AreEqual(BattleHudSkillPreviewShape.TargetCircle, spec.PreviewShape);
            Assert.AreEqual(SkillAimIndicatorShape.TargetCircle, spec.IndicatorShape);
            Assert.AreEqual(SkillAimMode.Point, spec.AimMode);
            Assert.AreEqual(3.2f, spec.Radius, 0.0001f);
        }

        [Test]
        public void SelfCircle_UsesTemplateSelfRadius()
        {
            var (resolver, skill, template) = BuildResolver(indicatorShape: 3);
            SetField(template, "SelfRadius", 4.5f);

            var spec = resolver.Resolve(skill, template);

            Assert.AreEqual(BattleHudSkillPreviewShape.SelfCircle, spec.PreviewShape);
            Assert.AreEqual(SkillAimIndicatorShape.SelfCircle, spec.IndicatorShape);
            Assert.AreEqual(4.5f, spec.SelfRadius, 0.0001f);
            Assert.AreEqual(4.5f, spec.Radius, 0.0001f);
        }

        [Test]
        public void DisabledSelfCircle_RetainsClickCastPreviewWithoutEnablingAim()
        {
            var (resolver, skill, template) = BuildResolver(indicatorShape: 3, enableAim: false);
            SetField(template, "SelfRadius", 4f);
            SetField(template, "UsePointMode", 0);

            var spec = resolver.Resolve(skill, template);

            Assert.AreEqual(BattleHudSkillPreviewShape.SelfCircle, spec.PreviewShape);
            Assert.AreEqual(SkillAimIndicatorShape.SelfCircle, spec.IndicatorShape);
            Assert.IsFalse(spec.EnableAim);
            Assert.AreEqual(SkillUsePointMode.None, spec.UsePointMode);
            Assert.AreEqual(4f, spec.SelfRadius, 0.0001f);
        }

        [Test]
        public void Sector_UsesTemplateSectorAngleDegrees()
        {
            var (resolver, skill, template) = BuildResolver(indicatorShape: 4);
            SetField(template, "SectorAngleDegrees", 60f);
            SetField(template, "IndicatorWorldWidth", 3f);

            var spec = resolver.Resolve(skill, template);

            Assert.AreEqual(BattleHudSkillPreviewShape.Sector, spec.PreviewShape);
            Assert.AreEqual(SkillAimIndicatorShape.Sector, spec.IndicatorShape);
            Assert.AreEqual(60f, spec.AngleDegrees, 0.0001f);
            Assert.AreEqual(60f, spec.FanAngleDegrees, 0.0001f);
            Assert.AreEqual(3f, spec.FanRadius, 0.0001f);
        }

        [Test]
        public void DirectionArea_MapsToDirectionAreaPreview()
        {
            var (resolver, skill, template) = BuildResolver(indicatorShape: 5, indicatorWorldWidth: 2f);

            var spec = resolver.Resolve(skill, template);

            Assert.AreEqual(BattleHudSkillPreviewShape.DirectionArea, spec.PreviewShape);
            Assert.AreEqual(SkillAimIndicatorShape.DirectionArea, spec.IndicatorShape);
            Assert.AreEqual(2f, spec.Width, 0.0001f);
        }

        [Test]
        public void DashLine_UsesTemplateDashDistance()
        {
            var (resolver, skill, template) = BuildResolver(indicatorShape: 6, indicatorWorldWidth: 1.2f);
            SetField(template, "DashDistance", 5f);

            var spec = resolver.Resolve(skill, template);

            Assert.AreEqual(BattleHudSkillPreviewShape.DashLine, spec.PreviewShape);
            Assert.AreEqual(SkillAimIndicatorShape.DashLine, spec.IndicatorShape);
            Assert.AreEqual(5f, spec.DashDistance, 0.0001f);
            Assert.AreEqual(MathfMax(TestRange, 5f), spec.Range, 0.0001f);
            Assert.AreEqual(1.2f, spec.Width, 0.0001f);
        }

        [Test]
        public void LockProjectile_StoresLockOnDurationSecondsAndRadius()
        {
            var (resolver, skill, template) = BuildResolver(indicatorShape: 7);
            SetField(template, "LockOnDurationMs", 2500);
            SetField(template, "LockProjectileRadius", 2.4f);
            SetField(template, "IndicatorWorldWidth", 0.6f);

            var spec = resolver.Resolve(skill, template);

            Assert.AreEqual(BattleHudSkillPreviewShape.LockProjectile, spec.PreviewShape);
            Assert.AreEqual(SkillAimIndicatorShape.LockProjectile, spec.IndicatorShape);
            Assert.AreEqual(SkillAimMode.Point, spec.AimMode);
            Assert.AreEqual(2.5f, spec.LockOnDurationSeconds, 0.0001f);
            Assert.AreEqual(2.4f, spec.LockProjectileRadius, 0.0001f);
            Assert.AreEqual(2.4f, spec.Radius, 0.0001f);
            Assert.AreEqual(0.6f, spec.Width, 0.0001f);
        }

        [Test]
        public void FanArea_UsesFanAngleDegreesAndFanRadius()
        {
            var (resolver, skill, template) = BuildResolver(indicatorShape: 8);
            SetField(template, "FanAngleDegrees", 120f);
            SetField(template, "FanRadius", 5.5f);
            SetField(template, "IndicatorWorldWidth", 0.6f);

            var spec = resolver.Resolve(skill, template);

            Assert.AreEqual(BattleHudSkillPreviewShape.FanArea, spec.PreviewShape);
            Assert.AreEqual(SkillAimIndicatorShape.FanArea, spec.IndicatorShape);
            Assert.AreEqual(120f, spec.AngleDegrees, 0.0001f);
            Assert.AreEqual(120f, spec.FanAngleDegrees, 0.0001f);
            Assert.AreEqual(5.5f, spec.FanRadius, 0.0001f);
        }

        [Test]
        public void ResolverRawHelpers_MapEveryKnownShapeCorrectly()
        {
            Assert.AreEqual(SkillAimIndicatorShape.Hidden, BattleHudSkillPresentationSpecResolver.ResolveIndicatorShape(0));
            Assert.AreEqual(SkillAimIndicatorShape.DirectionLine, BattleHudSkillPresentationSpecResolver.ResolveIndicatorShape(1));
            Assert.AreEqual(SkillAimIndicatorShape.TargetCircle, BattleHudSkillPresentationSpecResolver.ResolveIndicatorShape(2));
            Assert.AreEqual(SkillAimIndicatorShape.SelfCircle, BattleHudSkillPresentationSpecResolver.ResolveIndicatorShape(3));
            Assert.AreEqual(SkillAimIndicatorShape.Sector, BattleHudSkillPresentationSpecResolver.ResolveIndicatorShape(4));
            Assert.AreEqual(SkillAimIndicatorShape.DirectionArea, BattleHudSkillPresentationSpecResolver.ResolveIndicatorShape(5));
            Assert.AreEqual(SkillAimIndicatorShape.DashLine, BattleHudSkillPresentationSpecResolver.ResolveIndicatorShape(6));
            Assert.AreEqual(SkillAimIndicatorShape.LockProjectile, BattleHudSkillPresentationSpecResolver.ResolveIndicatorShape(7));
            Assert.AreEqual(SkillAimIndicatorShape.FanArea, BattleHudSkillPresentationSpecResolver.ResolveIndicatorShape(8));
            Assert.AreEqual(SkillAimIndicatorShape.Hidden, BattleHudSkillPresentationSpecResolver.ResolveIndicatorShape(-1));

            Assert.AreEqual(BattleHudSkillPreviewShape.None, BattleHudSkillPresentationSpecResolver.ResolvePreviewShape(0));
            Assert.AreEqual(BattleHudSkillPreviewShape.DirectionLine, BattleHudSkillPresentationSpecResolver.ResolvePreviewShape(1));
            Assert.AreEqual(BattleHudSkillPreviewShape.TargetCircle, BattleHudSkillPresentationSpecResolver.ResolvePreviewShape(2));
            Assert.AreEqual(BattleHudSkillPreviewShape.SelfCircle, BattleHudSkillPresentationSpecResolver.ResolvePreviewShape(3));
            Assert.AreEqual(BattleHudSkillPreviewShape.Sector, BattleHudSkillPresentationSpecResolver.ResolvePreviewShape(4));
            Assert.AreEqual(BattleHudSkillPreviewShape.DashLine, BattleHudSkillPresentationSpecResolver.ResolvePreviewShape(5));
            Assert.AreEqual(BattleHudSkillPreviewShape.LockProjectile, BattleHudSkillPresentationSpecResolver.ResolvePreviewShape(6));
            Assert.AreEqual(BattleHudSkillPreviewShape.FanArea, BattleHudSkillPresentationSpecResolver.ResolvePreviewShape(7));
            Assert.AreEqual(BattleHudSkillPreviewShape.DirectionArea, BattleHudSkillPresentationSpecResolver.ResolvePreviewShape(8));
            Assert.AreEqual(BattleHudSkillPreviewShape.None, BattleHudSkillPresentationSpecResolver.ResolvePreviewShape(99));
        }

        [Test]
        public void NullSkill_ReturnsHiddenZeroSpec()
        {
            var resolver = new BattleHudSkillPresentationSpecResolver();
            var template = MakeTemplate(1);

            var spec = resolver.Resolve(null, template);

            Assert.AreEqual(BattleHudSkillPreviewShape.None, spec.PreviewShape);
            Assert.AreEqual(0, spec.SkillId);
        }

        private static (BattleHudSkillPresentationSpecResolver, SkillMO, SkillButtonTemplateMO) BuildResolver(
            int indicatorShape,
            bool enableAim = true,
            float indicatorWorldWidth = 1f)
        {
            var skill = new SkillMO(new SkillDTO
            {
                Id = TestSkillId,
                Name = "TestSkill",
                Range = TestRange,
                SkillType = 0,
            });
            var template = MakeTemplate(indicatorShape, enableAim, indicatorWorldWidth);
            return (new BattleHudSkillPresentationSpecResolver(), skill, template);
        }

        private static SkillButtonTemplateMO MakeTemplate(int indicatorShape, bool enableAim = true, float indicatorWorldWidth = 1f)
        {
            var dto = new SkillButtonTemplateDTO
            {
                Id = 7,
                Name = "TestTemplate",
                EnableAim = enableAim,
                AimMode = 0,
                AimMaxRadius = 200f,
                IndicatorShape = indicatorShape,
                IndicatorWorldWidth = indicatorWorldWidth,
                UsePointMode = 1,
                LongPressSeconds = 0.35f,
                DragThreshold = 12f,
                SelectRange = 4f,
                FaceToAim = true,
            };
            return new SkillButtonTemplateMO(dto);
        }

        private static void SetField(SkillButtonTemplateMO mo, string fieldName, object value)
        {
            var field = typeof(SkillButtonTemplateMO).GetField(
                $"<{fieldName}>k__BackingField",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, $"backing field for {fieldName} not found");
            field.SetValue(mo, value);
        }

        private static float MathfMax(float a, float b) => a > b ? a : b;
    }
}
