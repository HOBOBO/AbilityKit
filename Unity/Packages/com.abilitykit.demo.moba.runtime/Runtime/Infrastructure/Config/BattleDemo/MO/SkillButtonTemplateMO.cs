using System;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Config.BattleDemo.MO
{
    public sealed class SkillButtonTemplateMO
    {
        public int Id { get; }
        public string Name { get; }

        public float LongPressSeconds { get; }
        public float DragThreshold { get; }
        public bool EnableAim { get; }

        public int AimMode { get; }
        public float AimMaxRadius { get; }
        public int IndicatorShape { get; }
        public float IndicatorWorldWidth { get; }

        public int UsePointMode { get; }
        public float SelectRange { get; }
        public PointRangePolicy PointRangePolicy { get; }
        public bool FaceToAim { get; }

        public float SectorAngleDegrees { get; }
        public float DashDistance { get; }
        public int LockOnDurationMs { get; }
        public float FanRadius { get; }
        public float FanAngleDegrees { get; }
        public float SelfRadius { get; }
        public float LockProjectileRadius { get; }

        public SkillButtonTemplateMO(SkillButtonTemplateDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Name = dto.Name;

            LongPressSeconds = dto.LongPressSeconds;
            DragThreshold = dto.DragThreshold;
            EnableAim = dto.EnableAim;

            AimMode = dto.AimMode;
            AimMaxRadius = dto.AimMaxRadius;
            IndicatorShape = dto.IndicatorShape;
            IndicatorWorldWidth = dto.IndicatorWorldWidth;

            UsePointMode = dto.UsePointMode;
            SelectRange = dto.SelectRange;
            PointRangePolicy = dto.PointRangePolicy;
            if (!Enum.IsDefined(typeof(PointRangePolicy), PointRangePolicy) ||
                (PointRangePolicy == PointRangePolicy.ClampToResolvedRange &&
                 (dto.AimMode != 1 || dto.UsePointMode != 2 || float.IsNaN(SelectRange) || float.IsInfinity(SelectRange))))
                throw new ArgumentException("Point range clamp requires Point/TargetPoint and a finite selection range.");
            FaceToAim = dto.FaceToAim;

            SectorAngleDegrees = dto.SectorAngleDegrees;
            DashDistance = dto.DashDistance;
            LockOnDurationMs = dto.LockOnDurationMs;
            FanRadius = dto.FanRadius;
            FanAngleDegrees = dto.FanAngleDegrees;
            SelfRadius = dto.SelfRadius;
            LockProjectileRadius = dto.LockProjectileRadius;
        }
    }
}
