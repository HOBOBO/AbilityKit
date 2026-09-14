using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public readonly struct SpawnAreaArgs
    {
        public readonly int AreaId;
        public readonly int PositionMode;
        public readonly float RadiusOverride;
        public readonly int DurationFrames;
        public readonly int DurationMs;
        public readonly int StayIntervalFrames;
        public readonly int CollisionLayerMaskOverride;
        public readonly float OffsetX;
        public readonly float OffsetY;
        public readonly float OffsetZ;
        public readonly bool HasTargetRequest;
        public readonly MobaActionTargetRequest TargetRequest;
        public readonly BlackboardWriteTarget ResultTarget;
        public readonly BlackboardWriteTarget ResultCountTarget;

        public SpawnAreaArgs(
            int areaId,
            int positionMode,
            float radiusOverride,
            int durationFrames,
            int durationMs,
            int stayIntervalFrames,
            int collisionLayerMaskOverride,
            float offsetX,
            float offsetY,
            float offsetZ,
            bool hasTargetRequest = false,
            MobaActionTargetRequest targetRequest = default,
            BlackboardWriteTarget resultTarget = default,
            BlackboardWriteTarget resultCountTarget = default)
        {
            AreaId = areaId;
            PositionMode = positionMode;
            RadiusOverride = radiusOverride;
            DurationFrames = durationFrames;
            DurationMs = durationMs;
            StayIntervalFrames = stayIntervalFrames;
            CollisionLayerMaskOverride = collisionLayerMaskOverride;
            OffsetX = offsetX;
            OffsetY = offsetY;
            OffsetZ = offsetZ;
            HasTargetRequest = hasTargetRequest;
            TargetRequest = targetRequest;
            ResultTarget = resultTarget;
            ResultCountTarget = resultCountTarget;
        }
    }
}
