using System;
using AbilityKit.Ability.Impl.Moba;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO
{
    public sealed class ProjectileLauncherMO
    {
        public int Id { get; }
        public string Name { get; }
        public ProjectileEmitterType EmitterType { get; }

        public int DurationMs { get; }
        public int IntervalMs { get; }

        public int CountPerShot { get; }
        public float FanAngleDeg { get; }

        public ProjectileLauncherMO(global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.ProjectileLauncherDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Name = dto.Name;
            EmitterType = (ProjectileEmitterType)dto.EmitterType;

            DurationMs = dto.DurationMs;
            IntervalMs = dto.IntervalMs;

            CountPerShot = dto.CountPerShot;
            FanAngleDeg = dto.FanAngleDeg;
        }
    }
}
