using System;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO
{
    public sealed class EmitterMO
    {
        public int Id { get; }
        public string Name { get; }

        public int EmitKind { get; }
        public int TemplateId { get; }

        public int DelayMs { get; }
        public int DurationMs { get; }
        public int IntervalMs { get; }
        public int TotalCount { get; }

        public int CountPerShot { get; }
        public float FanAngleDeg { get; }

        public int CenterMode { get; }
        public float OffsetX { get; }
        public float OffsetY { get; }
        public float OffsetZ { get; }

        public EmitterMO(EmitterDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Name = dto.Name;

            EmitKind = dto.EmitKind;
            TemplateId = dto.TemplateId;

            DelayMs = dto.DelayMs;
            DurationMs = dto.DurationMs;
            IntervalMs = dto.IntervalMs;
            TotalCount = dto.TotalCount;

            CountPerShot = dto.CountPerShot;
            FanAngleDeg = dto.FanAngleDeg;

            CenterMode = dto.CenterMode;
            OffsetX = dto.OffsetX;
            OffsetY = dto.OffsetY;
            OffsetZ = dto.OffsetZ;
        }
    }
}
