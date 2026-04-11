using System;
using AbilityKit.Ability.Configs;
using AbilityKit.Core.Math;
using AbilityKit.Ability.Triggering.Runtime;
using Sirenix.OdinInspector;

namespace AbilityKit.Ability.Editor
{
    [Serializable]
    [TriggerActionType(TriggerActionTypes.SpawnSummon, "创造召唤物", "行为/Combat", 0)]
    public sealed class SpawnSummonActionEditorConfig : ActionEditorConfigBase
    {
        public override string Type => TriggerActionTypes.SpawnSummon;

        [LabelText("模板Id(可选)")]
        public int TemplateId;

        [LabelText("启用覆盖")]
        public bool EnableOverrides;

        [LabelText("召唤物Id")]
        public int SummonId;

        [LabelText("目标模式")]
        public SpawnSummonTargetMode TargetMode = SpawnSummonTargetMode.ExplicitTarget;

        [LabelText("位置模式")]
        public SpawnSummonPositionMode PositionMode = SpawnSummonPositionMode.Caster;

        [LabelText("朝向模式")]
        public SpawnSummonRotationMode RotationMode = SpawnSummonRotationMode.Caster;

        [LabelText("OwnerKey模式")]
        public SpawnSummonOwnerKeyMode OwnerKeyMode = SpawnSummonOwnerKeyMode.CasterActorId;

        [LabelText("阵列模式")]
        public SpawnSummonPatternMode PatternMode = SpawnSummonPatternMode.Single;

        [LabelText("阵列数量")]
        public int PatternCount = 1;

        [LabelText("间距")]
        public float Spacing;

        [LabelText("半径")]
        public float Radius;

        [LabelText("起始角度")]
        public float StartAngleDeg;

        [LabelText("弧形角度")]
        public float ArcAngleDeg;

        [LabelText("Yaw偏移")]
        public float YawOffsetDeg;

        [LabelText("随机种子")]
        public int RandomSeed;

        [LabelText("随机半径最小")]
        public float RandomRadiusMin;

        [LabelText("随机半径最大")]
        public float RandomRadiusMax;

        [LabelText("Grid行")]
        public int GridRows;

        [LabelText("Grid列")]
        public int GridCols;

        [LabelText("Grid间距X")]
        public float GridSpacingX;

        [LabelText("Grid间距Z")]
        public float GridSpacingZ;

        [LabelText("每点朝向")]
        public SpawnSummonPerPointRotationMode PerPointRotationMode = SpawnSummonPerPointRotationMode.Inherit;

        [LabelText("每点Yaw偏移")]
        public float PerPointYawOffsetDeg;

        [LabelText("间隔毫秒")]
        public int IntervalMs;

        [LabelText("持续毫秒")]
        public int DurationMs;

        [LabelText("总次数")]
        public int TotalCount;

        [LabelText("CasterKey(可选)")]
        public string CasterKey;

        [LabelText("TargetKey(可选)")]
        public string TargetKey;

        [LabelText("查询模板Id(可选)")]
        public int QueryTemplateId;

        [LabelText("AimPosKey(可选)")]
        public string AimPosKey;

        [LabelText("FixedPosKey(可选)")]
        public string FixedPosKey;

        [LabelText("FixedPosFallback")]
        public Vec3 FixedPosFallback;

        protected override string GetTitleSuffix()
        {
            if (TemplateId > 0) return "tpl=" + TemplateId;
            return SummonId > 0 ? SummonId.ToString() : null;
        }

        public override ActionRuntimeConfigBase ToRuntimeStrong()
        {
            return new SpawnSummonActionConfig
            {
                TemplateId = TemplateId,
                EnableOverrides = EnableOverrides,
                SummonId = SummonId,
                TargetMode = TargetMode,
                PositionMode = PositionMode,
                RotationMode = RotationMode,
                OwnerKeyMode = OwnerKeyMode,
                PatternMode = PatternMode,
                PatternCount = PatternCount,
                Spacing = Spacing,
                Radius = Radius,
                StartAngleDeg = StartAngleDeg,
                ArcAngleDeg = ArcAngleDeg,
                YawOffsetDeg = YawOffsetDeg,
                RandomSeed = RandomSeed,
                RandomRadiusMin = RandomRadiusMin,
                RandomRadiusMax = RandomRadiusMax,
                GridRows = GridRows,
                GridCols = GridCols,
                GridSpacingX = GridSpacingX,
                GridSpacingZ = GridSpacingZ,
                PerPointRotationMode = PerPointRotationMode,
                PerPointYawOffsetDeg = PerPointYawOffsetDeg,
                IntervalMs = IntervalMs,
                DurationMs = DurationMs,
                TotalCount = TotalCount,
                CasterKey = CasterKey,
                TargetKey = TargetKey,
                QueryTemplateId = QueryTemplateId,
                AimPosKey = AimPosKey,
                FixedPosKey = FixedPosKey,
                FixedPosFallback = FixedPosFallback,
            };
        }
    }
}
