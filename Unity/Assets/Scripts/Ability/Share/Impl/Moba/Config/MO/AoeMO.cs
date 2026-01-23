#if false
using System;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO
{
    public sealed class AoeMO
    {
        public int Id { get; }
        public string Name { get; }

        public float Radius { get; }
        public int DelayMs { get; }
        public int CollisionLayerMask { get; }
        public int MaxTargets { get; }

        public int[] OnDelayTriggerIds { get; }

        public AoeMO(global::AbilityKit.Ability.Impl.BattleDemo.Moba.Config.AoeDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Name = dto.Name;

            Radius = dto.Radius;
            DelayMs = dto.DelayMs;
            CollisionLayerMask = dto.CollisionLayerMask;
            MaxTargets = dto.MaxTargets;

            OnDelayTriggerIds = dto.OnDelayTriggerIds;
        }
    }
}
#endif
