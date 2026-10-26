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
        public readonly int ShapeKind;
        public readonly float HalfAngleDeg;
        public readonly float Width;
        public readonly float Length;
        public readonly float InitialRadius;
        public readonly float RadiusGrowthPerFrame;
        public readonly bool AnchorAtOrigin;
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
            int shapeKind = 0,
            float halfAngleDeg = 0f,
            float width = 0f,
            float length = 0f,
            float initialRadius = 0f,
            float radiusGrowthPerFrame = 0f,
            bool anchorAtOrigin = true,
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
            ShapeKind = shapeKind;
            HalfAngleDeg = halfAngleDeg;
            Width = width;
            Length = length;
            InitialRadius = initialRadius;
            RadiusGrowthPerFrame = radiusGrowthPerFrame;
            AnchorAtOrigin = anchorAtOrigin;
            HasTargetRequest = hasTargetRequest;
            TargetRequest = targetRequest;
            ResultTarget = resultTarget;
            ResultCountTarget = resultCountTarget;
        }
    }
}
