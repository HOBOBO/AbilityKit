using System;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO
{
    public sealed class AoeMO
    {
        public int Id { get; }
        public string Name { get; }

        public int ModelId { get; }
        public int VfxId { get; }
        public int AttachMode { get; }
        public float OffsetX { get; }
        public float OffsetY { get; }
        public float OffsetZ { get; }

        public float Radius { get; }
        public int DelayMs { get; }
        public int CollisionLayerMask { get; }
        public int MaxTargets { get; }

        public int[] OnDelayTriggerIds { get; }

        public AoeMO(AoeDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Name = dto.Name;

            ModelId = dto.ModelId;
            VfxId = dto.VfxId;
            AttachMode = dto.AttachMode;
            OffsetX = dto.OffsetX;
            OffsetY = dto.OffsetY;
            OffsetZ = dto.OffsetZ;

            Radius = dto.Radius;
            DelayMs = dto.DelayMs;
            CollisionLayerMask = dto.CollisionLayerMask;
            MaxTargets = dto.MaxTargets;

            OnDelayTriggerIds = dto.OnDelayTriggerIds ?? Array.Empty<int>();
        }
    }
}
