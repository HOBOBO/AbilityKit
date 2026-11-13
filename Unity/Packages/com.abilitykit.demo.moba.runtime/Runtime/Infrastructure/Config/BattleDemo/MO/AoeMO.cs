using System;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Config.BattleDemo.MO
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
        public float InnerRadius { get; }
        public int DelayMs { get; }
        public int DurationMs { get; }
        public int CollisionLayerMask { get; }
        public int MaxTargets { get; }

        public int[] OnDelayTriggerIds { get; }
        public int[] OnEnterTriggerIds { get; }
        public int[] OnCrossTriggerIds { get; }
        public int[] OnExitTriggerIds { get; }
        public int[] OnIntervalTriggerIds { get; }
        public int IntervalMs { get; }
        public bool ConsumeOnFirstEnter { get; }

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
            InnerRadius = dto.InnerRadius > 0f ? Math.Min(dto.InnerRadius, dto.Radius) : 0f;
            DelayMs = dto.DelayMs;
            DurationMs = dto.DurationMs;
            CollisionLayerMask = dto.CollisionLayerMask;
            MaxTargets = dto.MaxTargets;

            OnDelayTriggerIds = dto.OnDelayTriggerIds ?? Array.Empty<int>();
            OnEnterTriggerIds = dto.OnEnterTriggerIds ?? Array.Empty<int>();
            OnCrossTriggerIds = dto.OnCrossTriggerIds ?? Array.Empty<int>();
            OnExitTriggerIds = dto.OnExitTriggerIds ?? Array.Empty<int>();
            OnIntervalTriggerIds = dto.OnIntervalTriggerIds ?? Array.Empty<int>();
            IntervalMs = dto.IntervalMs;
            ConsumeOnFirstEnter = dto.ConsumeOnFirstEnter;
        }
    }
}
