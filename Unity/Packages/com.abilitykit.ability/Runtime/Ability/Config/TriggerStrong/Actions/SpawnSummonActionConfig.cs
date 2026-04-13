using System;
using AbilityKit.Core.Math;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Config
{
    /// <summary>
    /// 召唤物的所有者键来源模式
    /// </summary>
    [Serializable]
    public enum SpawnSummonOwnerKeyMode
    {
        None = 0,
        CasterActorId = 1,
    }

    /// <summary>
    /// 召唤物目标模式
    /// </summary>
    [Serializable]
    public enum SpawnSummonTargetMode
    {
        ExplicitTarget = 0,
        QueryTargets = 1,
    }

    /// <summary>
    /// 召唤物旋转模式
    /// </summary>
    [Serializable]
    public enum SpawnSummonRotationMode
    {
        Caster = 0,
        AimDir = 1,
        FaceTarget = 2,
    }

    /// <summary>
    /// 召唤物生成模式
    /// </summary>
    [Serializable]
    public enum SpawnSummonPatternMode
    {
        Single = 0,
        Line = 1,
        Ring = 2,
        Arc = 3,
        RandomCircle = 4,
        Grid = 5,
    }

    /// <summary>
    /// 每个生成点的旋转模式
    /// </summary>
    [Serializable]
    public enum SpawnSummonPerPointRotationMode
    {
        Inherit = 0,
        FaceCenter = 1,
        FaceOutward = 2,
        TangentCw = 3,
        TangentCcw = 4,
        FaceTargetActor = 5,
    }

    /// <summary>
    /// 召唤物位置模式
    /// </summary>
    [Serializable]
    public enum SpawnSummonPositionMode
    {
        Caster = 0,
        Target = 1,
        AimPos = 2,
        Fixed = 3,
    }

    [Serializable]
    public sealed class SpawnSummonActionConfig : ActionConfigBase
    {
        public override string Type => TriggerActionTypes.SpawnSummon;

        public int TemplateId;
        public bool EnableOverrides;

        public int SummonId;

        public SpawnSummonTargetMode TargetMode = SpawnSummonTargetMode.ExplicitTarget;
        public SpawnSummonPositionMode PositionMode = SpawnSummonPositionMode.Caster;
        public SpawnSummonRotationMode RotationMode = SpawnSummonRotationMode.Caster;
        public SpawnSummonOwnerKeyMode OwnerKeyMode = SpawnSummonOwnerKeyMode.CasterActorId;

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
