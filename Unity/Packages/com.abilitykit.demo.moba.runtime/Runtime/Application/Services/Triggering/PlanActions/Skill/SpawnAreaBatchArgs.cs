using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// Common Area spawn inputs plus a deterministic, resolver-driven pattern.
    /// Kept under the historical Batch name for serialized compatibility.
    /// </summary>
    public readonly struct SpawnAreaBatchArgs
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
        public readonly float DirectionOffset;
        public readonly float LateralOffset;
        public readonly int ShapeKind;
        public readonly float Width;
        public readonly float Length;
        public readonly float InitialRadius;
        public readonly float RadiusGrowthPerFrame;
        public readonly bool AnchorAtOrigin;
        public readonly int GroupKey;
        public readonly bool ClaimTargetsWithinGroup;
        public readonly int PatternKind;
        public readonly int PatternCount;
        public readonly float PatternApothem;
        public readonly float PatternStartAngleDeg;
        public readonly float PatternAngleStepDeg;
        public readonly float PatternEdgeWidth;
        public readonly float PatternEdgeLength;
        public readonly bool HasTargetRequest;
        public readonly MobaActionTargetRequest TargetRequest;
        public readonly BlackboardWriteTarget ResultTarget;
        public readonly BlackboardWriteTarget ResultCountTarget;

        public SpawnAreaBatchArgs(
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
            float directionOffset,
            float lateralOffset,
            int shapeKind,
            float width,
            float length,
            float initialRadius,
            float radiusGrowthPerFrame,
            bool anchorAtOrigin,
            int groupKey,
            bool claimTargetsWithinGroup,
            int patternCount,
            float patternApothem,
            float patternStartAngleDeg,
            float patternAngleStepDeg,
            float patternEdgeWidth,
            float patternEdgeLength,
            bool hasTargetRequest,
            MobaActionTargetRequest targetRequest,
            BlackboardWriteTarget resultTarget,
            BlackboardWriteTarget resultCountTarget,
            int patternKind = 0)
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
            DirectionOffset = directionOffset;
            LateralOffset = lateralOffset;
            ShapeKind = shapeKind;
            Width = width;
            Length = length;
            InitialRadius = initialRadius;
            RadiusGrowthPerFrame = radiusGrowthPerFrame;
            AnchorAtOrigin = anchorAtOrigin;
            GroupKey = groupKey;
            ClaimTargetsWithinGroup = claimTargetsWithinGroup;
            PatternKind = patternKind;
            PatternCount = patternCount;
            PatternApothem = patternApothem;
            PatternStartAngleDeg = patternStartAngleDeg;
            PatternAngleStepDeg = patternAngleStepDeg;
            PatternEdgeWidth = patternEdgeWidth;
            PatternEdgeLength = patternEdgeLength;
            HasTargetRequest = hasTargetRequest;
            TargetRequest = targetRequest;
            ResultTarget = resultTarget;
            ResultCountTarget = resultCountTarget;
        }

        public SpawnAreaArgs ToAreaArgs(float width, float length, bool anchorAtOrigin)
        {
            return new SpawnAreaArgs(
                AreaId, PositionMode, RadiusOverride, DurationFrames, DurationMs,
                StayIntervalFrames, CollisionLayerMaskOverride, OffsetX, OffsetY, OffsetZ,
                ShapeKind, 0f, width, length, InitialRadius, RadiusGrowthPerFrame,
                anchorAtOrigin, GroupKey, ClaimTargetsWithinGroup,
                HasTargetRequest, TargetRequest, ResultTarget, ResultCountTarget,
                DirectionOffset, LateralOffset);
        }
    }
}
