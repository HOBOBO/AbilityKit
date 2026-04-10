using System;
using AbilityKit.Core.Math;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Configs
{
    [Serializable]
    public sealed class SpawnSummonActionConfig : ActionRuntimeConfigBase
    {
        public override string Type => TriggerActionTypes.SpawnSummon;

        public int TemplateId;
        public bool EnableOverrides;

        public int SummonId;

        public SpawnSummonTargetMode TargetMode = SpawnSummonTargetMode.ExplicitTarget;
        public SpawnSummonPositionMode PositionMode = SpawnSummonPositionMode.Caster;
        public SpawnSummonRotationMode RotationMode = SpawnSummonRotationMode.Caster;
        public SpawnSummonOwnerKeyMode OwnerKeyMode = SpawnSummonOwnerKeyMode.SourceContextId;

        public SpawnSummonPatternMode PatternMode = SpawnSummonPatternMode.Single;
        public int PatternCount = 1;
        public float Spacing;
        public float Radius;
        public float StartAngleDeg;
        public float ArcAngleDeg;
        public float YawOffsetDeg;

        public int RandomSeed;
        public float RandomRadiusMin;
        public float RandomRadiusMax;

        public int GridRows;
        public int GridCols;
        public float GridSpacingX;
        public float GridSpacingZ;

        public SpawnSummonPerPointRotationMode PerPointRotationMode = SpawnSummonPerPointRotationMode.Inherit;
        public float PerPointYawOffsetDeg;

        public int IntervalMs;
        public int DurationMs;
        public int TotalCount;

        public string CasterKey;
        public string TargetKey;
        public int QueryTemplateId;

        public string AimPosKey;
        public string FixedPosKey;
        public Vec3 FixedPosFallback;

        public override ActionDef ToActionDef()
        {
            var dict = PooledDefArgs.Rent();

            if (TemplateId > 0)
            {
                dict["templateId"] = TemplateId;
                if (!EnableOverrides)
                {
                    return new ActionDef(Type, dict);
                }
            }

            dict["summonId"] = SummonId;

            dict["targetMode"] = (int)TargetMode;
            dict["positionMode"] = (int)PositionMode;
            dict["rotationMode"] = (int)RotationMode;
            dict["ownerKeyMode"] = (int)OwnerKeyMode;

            dict["patternMode"] = (int)PatternMode;
            dict["patternCount"] = PatternCount;

            if (Spacing != 0f) dict["spacing"] = Spacing;
            if (Radius != 0f) dict["radius"] = Radius;
            if (StartAngleDeg != 0f) dict["startAngleDeg"] = StartAngleDeg;
            if (ArcAngleDeg != 0f) dict["arcAngleDeg"] = ArcAngleDeg;
            if (YawOffsetDeg != 0f) dict["yawOffsetDeg"] = YawOffsetDeg;

            if (RandomSeed != 0) dict["randomSeed"] = RandomSeed;
            if (RandomRadiusMin != 0f) dict["randomRadiusMin"] = RandomRadiusMin;
            if (RandomRadiusMax != 0f) dict["randomRadiusMax"] = RandomRadiusMax;

            if (GridRows != 0) dict["gridRows"] = GridRows;
            if (GridCols != 0) dict["gridCols"] = GridCols;
            if (GridSpacingX != 0f) dict["gridSpacingX"] = GridSpacingX;
            if (GridSpacingZ != 0f) dict["gridSpacingZ"] = GridSpacingZ;

            dict["perPointRotationMode"] = (int)PerPointRotationMode;
            if (PerPointYawOffsetDeg != 0f) dict["perPointYawOffsetDeg"] = PerPointYawOffsetDeg;

            if (IntervalMs > 0) dict["intervalMs"] = IntervalMs;
            if (DurationMs > 0) dict["durationMs"] = DurationMs;
            if (TotalCount > 0) dict["totalCount"] = TotalCount;

            if (!string.IsNullOrEmpty(CasterKey)) dict["casterKey"] = CasterKey;
            if (!string.IsNullOrEmpty(TargetKey)) dict["targetKey"] = TargetKey;
            if (QueryTemplateId > 0) dict["queryTemplateId"] = QueryTemplateId;

            if (!string.IsNullOrEmpty(AimPosKey)) dict["aimPosKey"] = AimPosKey;
            if (!string.IsNullOrEmpty(FixedPosKey)) dict["fixedPosKey"] = FixedPosKey;
            if (FixedPosFallback.X != 0f || FixedPosFallback.Y != 0f || FixedPosFallback.Z != 0f) dict["fixedPosFallback"] = FixedPosFallback;

            return new ActionDef(Type, dict);
        }
    }
}
