using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Game.Battle.View.Lib.Skill;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal enum BattleHudSkillPreviewShape
    {
        None = 0,
        DirectionLine = 1,
        TargetCircle = 2,
        SelfCircle = 3,
        Sector = 4,
        DashLine = 5,
        LockProjectile = 6,
        FanArea = 7,
        DirectionArea = 8,
    }

    internal readonly struct BattleHudSkillPresentationSpec
    {
        public readonly int SkillId;
        public readonly string Name;
        public readonly BattleHudSkillPreviewShape PreviewShape;
        public readonly SkillAimIndicatorShape IndicatorShape;
        public readonly float Range;
        public readonly float Width;
        public readonly float Radius;
        public readonly Color Color;
        public readonly bool EnableAim;
        public readonly SkillAimMode AimMode;
        public readonly SkillUsePointMode UsePointMode;
        public readonly bool FaceToAim;
        public readonly float UiAimRadiusPixels;

        // ---- Extended geometry parameters (data-driven) ----
        /// <summary>扇形指示器中心角（度），Sector / FanArea 形状使用。</summary>
        public readonly float AngleDegrees;
        /// <summary>锁定型投射指示器的瞄准时长（秒），用于 LockProjectile 形状。</summary>
        public readonly float LockOnDurationSeconds;
        /// <summary>冲刺位移距离，DashLine 形状使用。</summary>
        public readonly float DashDistance;
        /// <summary>扇形技能指示器的中心角度（度），与 AngleDegrees 同义，FanArea 形状使用。</summary>
        public readonly float FanAngleDegrees;
        /// <summary>扇形技能指示器的半径，FanArea 形状使用。</summary>
        public readonly float FanRadius;
        /// <summary>以自身为中心的技能指示器的半径，SelfCircle 形状使用。</summary>
        public readonly float SelfRadius;
        /// <summary>锁定投射指示器的目标吸附半径，LockProjectile 形状使用。</summary>
        public readonly float LockProjectileRadius;

        public BattleHudSkillPresentationSpec(
            int skillId,
            string name,
            BattleHudSkillPreviewShape previewShape,
            SkillAimIndicatorShape indicatorShape,
            float range,
            float width,
            float radius,
            Color color,
            bool enableAim = true,
            SkillAimMode aimMode = SkillAimMode.Direction,
            SkillUsePointMode usePointMode = SkillUsePointMode.Aim,
            bool faceToAim = true,
            float uiAimRadiusPixels = 220f,
            float angleDegrees = 90f,
            float lockOnDurationSeconds = 1.5f,
            float dashDistance = 0f,
            float fanAngleDegrees = 90f,
            float fanRadius = 0f,
            float selfRadius = 0f,
            float lockProjectileRadius = 0f)
        {
            SkillId = skillId;
            Name = name;
            PreviewShape = previewShape;
            IndicatorShape = indicatorShape;
            Range = range;
            Width = width;
            Radius = radius;
            Color = color;
            EnableAim = enableAim;
            AimMode = aimMode;
            UsePointMode = usePointMode;
            FaceToAim = faceToAim;
            UiAimRadiusPixels = Mathf.Max(1f, uiAimRadiusPixels);

            AngleDegrees = Mathf.Clamp(angleDegrees, 1f, 360f);
            LockOnDurationSeconds = Mathf.Max(0f, lockOnDurationSeconds);
            DashDistance = Mathf.Max(0f, dashDistance);
            FanAngleDegrees = Mathf.Clamp(fanAngleDegrees, 1f, 360f);
            FanRadius = Mathf.Max(0f, fanRadius);
            SelfRadius = Mathf.Max(0f, selfRadius);
            LockProjectileRadius = Mathf.Max(0f, lockProjectileRadius);
        }

        public bool UsesTargetPoint => EnableAim && UsePointMode == SkillUsePointMode.TargetPoint;
        public float UiLengthPixels => Mathf.Max(80f, Range * 22f);
        public float UiWidthPixels => Mathf.Max(28f, (UsesTargetPoint ? Radius : Width) * 24f);

        public static BattleHudSkillPresentationSpec Hidden(int skillId, string name)
        {
            return new BattleHudSkillPresentationSpec(
                skillId,
                name,
                BattleHudSkillPreviewShape.None,
                SkillAimIndicatorShape.Hidden,
                0f,
                0f,
                0f,
                new Color(0.2f, 0.75f, 1f, 0.25f),
                enableAim: false,
                usePointMode: SkillUsePointMode.None,
                faceToAim: false);
        }
    }

    /// <summary>
    /// 把 SkillMO + SkillButtonTemplateMO 转换为 BattleHudSkillPresentationSpec 的解析器。
    /// 支持全部 9 种 IndicatorShape（Hidden / DirectionLine / TargetCircle / SelfCircle /
    /// Sector / DirectionArea / DashLine / LockProjectile / FanArea）的 PreviewShape + 几何参数映射。
    /// </summary>
    internal sealed class BattleHudSkillPresentationSpecResolver
    {
        private const float DefaultSectorAngleDegrees = 90f;
        private const float DefaultFanAngleDegrees = 90f;
        private const float DefaultFanRadius = 2.8f;
        private const float DefaultSelfRadius = 2.5f;
        private const float DefaultLockOnDurationSeconds = 1.5f;
        private const float DefaultLockProjectileRadius = 3f;
        private const float DefaultDashDistance = 4f;

        private static readonly Color s_directionColor = new Color(0.2f, 0.75f, 1f, 0.3f);
        private static readonly Color s_targetColor = new Color(0.2f, 0.75f, 1f, 0.28f);
        private static readonly Color s_selfColor = new Color(0.4f, 0.85f, 1f, 0.32f);
        private static readonly Color s_sectorColor = new Color(0.85f, 0.65f, 1f, 0.32f);
        private static readonly Color s_dashColor = new Color(0.95f, 0.78f, 0.25f, 0.32f);
        private static readonly Color s_lockColor = new Color(0.95f, 0.45f, 0.55f, 0.32f);
        private static readonly Color s_fanColor = new Color(0.7f, 0.95f, 0.45f, 0.32f);

        public BattleHudSkillPresentationSpec Resolve(SkillMO skill, SkillButtonTemplateMO template)
        {
            if (skill == null)
            {
                return BattleHudSkillPresentationSpec.Hidden(0, string.Empty);
            }

            if (template == null)
            {
                return BattleHudSkillPresentationSpec.Hidden(skill.Id, skill.Name);
            }

            var range = Mathf.Max(1f, skill.Range);
            var uiAimRadius = template.AimMaxRadius > 0f ? template.AimMaxRadius : 220f;
            var usePointMode = ResolveUsePointMode(template.UsePointMode);

            var indicatorShape = ResolveIndicatorShape(template.IndicatorShape);
            var previewShape = ResolvePreviewShape(template.IndicatorShape);
            if (!template.EnableAim && indicatorShape != SkillAimIndicatorShape.SelfCircle)
            {
                return BattleHudSkillPresentationSpec.Hidden(skill.Id, skill.Name);
            }

            // Point-mode target circles use their configured world radius.
            if (template.AimMode == (int)SkillAimMode.Point && indicatorShape == SkillAimIndicatorShape.TargetCircle)
            {
                var targetRadius = template.IndicatorWorldWidth > 0f
                    ? template.IndicatorWorldWidth
                    : 2.8f;
                return new BattleHudSkillPresentationSpec(
                    skill.Id,
                    skill.Name,
                    BattleHudSkillPreviewShape.TargetCircle,
                    SkillAimIndicatorShape.TargetCircle,
                    range,
                    targetRadius * 2f,
                    targetRadius,
                    s_targetColor,
                    enableAim: true,
                    aimMode: SkillAimMode.Point,
                    usePointMode: usePointMode,
                    faceToAim: template.FaceToAim,
                    uiAimRadiusPixels: uiAimRadius,
                    lockProjectileRadius: targetRadius);
            }

            switch (indicatorShape)
            {
                case SkillAimIndicatorShape.Hidden:
                    return BattleHudSkillPresentationSpec.Hidden(skill.Id, skill.Name);

                case SkillAimIndicatorShape.DirectionLine:
                    return new BattleHudSkillPresentationSpec(
                        skill.Id,
                        skill.Name,
                        BattleHudSkillPreviewShape.DirectionLine,
                        SkillAimIndicatorShape.DirectionLine,
                        range,
                        template.IndicatorWorldWidth > 0f ? template.IndicatorWorldWidth : 1.5f,
                        0f,
                        s_directionColor,
                        enableAim: true,
                        aimMode: SkillAimMode.Direction,
                        usePointMode: usePointMode,
                        faceToAim: template.FaceToAim,
                        uiAimRadiusPixels: uiAimRadius);

                case SkillAimIndicatorShape.TargetCircle:
                    var targetRadius = template.IndicatorWorldWidth > 0f ? template.IndicatorWorldWidth : 2.8f;
                    return new BattleHudSkillPresentationSpec(
                        skill.Id,
                        skill.Name,
                        BattleHudSkillPreviewShape.TargetCircle,
                        SkillAimIndicatorShape.TargetCircle,
                        range,
                        5.6f,
                        targetRadius,
                        s_targetColor,
                        enableAim: true,
                        aimMode: SkillAimMode.Point,
                        usePointMode: usePointMode,
                        faceToAim: template.FaceToAim,
                        uiAimRadiusPixels: uiAimRadius,
                        lockProjectileRadius: targetRadius);

                case SkillAimIndicatorShape.SelfCircle:
                    var selfRadius = template.SelfRadius > 0f ? template.SelfRadius : DefaultSelfRadius;
                    return new BattleHudSkillPresentationSpec(
                        skill.Id,
                        skill.Name,
                        BattleHudSkillPreviewShape.SelfCircle,
                        SkillAimIndicatorShape.SelfCircle,
                        range,
                        0f,
                        selfRadius,
                        s_selfColor,
                        enableAim: template.EnableAim,
                        aimMode: SkillAimMode.Direction,
                        usePointMode: usePointMode,
                        faceToAim: template.FaceToAim,
                        uiAimRadiusPixels: uiAimRadius,
                        selfRadius: selfRadius);

                case SkillAimIndicatorShape.Sector:
                    var sectorAngle = template.SectorAngleDegrees > 0f ? template.SectorAngleDegrees : DefaultSectorAngleDegrees;
                    var sectorRadius = template.IndicatorWorldWidth > 0f ? template.IndicatorWorldWidth : 2.8f;
                    return new BattleHudSkillPresentationSpec(
                        skill.Id,
                        skill.Name,
                        BattleHudSkillPreviewShape.Sector,
                        SkillAimIndicatorShape.Sector,
                        range,
                        sectorRadius,
                        sectorRadius,
                        s_sectorColor,
                        enableAim: true,
                        aimMode: SkillAimMode.Direction,
                        usePointMode: usePointMode,
                        faceToAim: template.FaceToAim,
                        uiAimRadiusPixels: uiAimRadius,
                        angleDegrees: sectorAngle,
                        fanAngleDegrees: sectorAngle,
                        fanRadius: sectorRadius);

                case SkillAimIndicatorShape.DirectionArea:
                    var areaWidth = template.IndicatorWorldWidth > 0f ? template.IndicatorWorldWidth : 1.5f;
                    return new BattleHudSkillPresentationSpec(
                        skill.Id,
                        skill.Name,
                        BattleHudSkillPreviewShape.DirectionArea,
                        SkillAimIndicatorShape.DirectionArea,
                        range,
                        areaWidth,
                        0f,
                        s_directionColor,
                        enableAim: true,
                        aimMode: SkillAimMode.Direction,
                        usePointMode: usePointMode,
                        faceToAim: template.FaceToAim,
                        uiAimRadiusPixels: uiAimRadius);

                case SkillAimIndicatorShape.DashLine:
                    var dashDistance = template.DashDistance > 0f ? template.DashDistance : DefaultDashDistance;
                    var dashWidth = template.IndicatorWorldWidth > 0f ? template.IndicatorWorldWidth : 1.5f;
                    // 把 DashDistance 当作距离上限，使用时也回退到 range
                    var dashRange = Mathf.Max(range, dashDistance);
                    return new BattleHudSkillPresentationSpec(
                        skill.Id,
                        skill.Name,
                        BattleHudSkillPreviewShape.DashLine,
                        SkillAimIndicatorShape.DashLine,
                        dashRange,
                        dashWidth,
                        0f,
                        s_dashColor,
                        enableAim: true,
                        aimMode: SkillAimMode.Direction,
                        usePointMode: usePointMode,
                        faceToAim: template.FaceToAim,
                        uiAimRadiusPixels: uiAimRadius,
                        dashDistance: dashDistance);

                case SkillAimIndicatorShape.LockProjectile:
                    var lockDurationMs = template.LockOnDurationMs > 0 ? template.LockOnDurationMs : (int)(DefaultLockOnDurationSeconds * 1000f);
                    var lockRadius = template.LockProjectileRadius > 0f ? template.LockProjectileRadius : DefaultLockProjectileRadius;
                    var lockLineWidth = template.IndicatorWorldWidth > 0f ? template.IndicatorWorldWidth : 0.6f;
                    return new BattleHudSkillPresentationSpec(
                        skill.Id,
                        skill.Name,
                        BattleHudSkillPreviewShape.LockProjectile,
                        SkillAimIndicatorShape.LockProjectile,
                        range,
                        lockLineWidth,
                        lockRadius,
                        s_lockColor,
                        enableAim: true,
                        aimMode: SkillAimMode.Point,
                        usePointMode: usePointMode,
                        faceToAim: template.FaceToAim,
                        uiAimRadiusPixels: uiAimRadius,
                        lockOnDurationSeconds: lockDurationMs / 1000f,
                        lockProjectileRadius: lockRadius);

                case SkillAimIndicatorShape.FanArea:
                    var fanAngle = template.FanAngleDegrees > 0f ? template.FanAngleDegrees : DefaultFanAngleDegrees;
                    var fanRadius = template.FanRadius > 0f ? template.FanRadius : DefaultFanRadius;
                    var fanLineWidth = template.IndicatorWorldWidth > 0f ? template.IndicatorWorldWidth : 0.6f;
                    return new BattleHudSkillPresentationSpec(
                        skill.Id,
                        skill.Name,
                        BattleHudSkillPreviewShape.FanArea,
                        SkillAimIndicatorShape.FanArea,
                        range,
                        fanLineWidth,
                        fanRadius,
                        s_fanColor,
                        enableAim: true,
                        aimMode: SkillAimMode.Direction,
                        usePointMode: usePointMode,
                        faceToAim: template.FaceToAim,
                        uiAimRadiusPixels: uiAimRadius,
                        angleDegrees: fanAngle,
                        fanAngleDegrees: fanAngle,
                        fanRadius: fanRadius);

                default:
                    return BattleHudSkillPresentationSpec.Hidden(skill.Id, skill.Name);
            }
        }

        /// <summary>把模板中的 int 转成 SkillAimIndicatorShape，未知值落到 Hidden。</summary>
        public static SkillAimIndicatorShape ResolveIndicatorShape(int rawValue)
        {
            switch (rawValue)
            {
                case 1: return SkillAimIndicatorShape.DirectionLine;
                case 2: return SkillAimIndicatorShape.TargetCircle;
                case 3: return SkillAimIndicatorShape.SelfCircle;
                case 4: return SkillAimIndicatorShape.Sector;
                case 5: return SkillAimIndicatorShape.DirectionArea;
                case 6: return SkillAimIndicatorShape.DashLine;
                case 7: return SkillAimIndicatorShape.LockProjectile;
                case 8: return SkillAimIndicatorShape.FanArea;
                default: return SkillAimIndicatorShape.Hidden;
            }
        }

        /// <summary>把模板中的 int 转成 BattleHudSkillPreviewShape，未知值落到 None。</summary>
        public static BattleHudSkillPreviewShape ResolvePreviewShape(int rawValue)
        {
            switch (rawValue)
            {
                case 1: return BattleHudSkillPreviewShape.DirectionLine;
                case 2: return BattleHudSkillPreviewShape.TargetCircle;
                case 3: return BattleHudSkillPreviewShape.SelfCircle;
                case 4: return BattleHudSkillPreviewShape.Sector;
                case 5: return BattleHudSkillPreviewShape.DashLine;
                case 6: return BattleHudSkillPreviewShape.LockProjectile;
                case 7: return BattleHudSkillPreviewShape.FanArea;
                case 8: return BattleHudSkillPreviewShape.DirectionArea;
                default: return BattleHudSkillPreviewShape.None;
            }
        }

        private static SkillUsePointMode ResolveUsePointMode(int value)
        {
            if (value == (int)SkillUsePointMode.Aim) return SkillUsePointMode.Aim;
            if (value == (int)SkillUsePointMode.TargetPoint) return SkillUsePointMode.TargetPoint;
            return SkillUsePointMode.None;
        }
    }
}
