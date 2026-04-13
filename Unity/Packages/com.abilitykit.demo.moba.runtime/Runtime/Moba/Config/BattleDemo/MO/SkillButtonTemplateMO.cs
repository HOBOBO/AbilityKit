using System;

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

        public int UsePointMode { get; }
        public float SelectRange { get; }
        public bool FaceToAim { get; }

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

            UsePointMode = dto.UsePointMode;
            SelectRange = dto.SelectRange;
            FaceToAim = dto.FaceToAim;
        }
    }
}
