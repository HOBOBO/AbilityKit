using System;
using AbilityKit.Ability.Config;
using AbilityKit.Core.Mathematics;
using AbilityKit.Ability.Triggering.Runtime;
using Sirenix.OdinInspector;

namespace AbilityKit.Ability.Editor
{
    [Serializable]
    [TriggerActionType(TriggerActionTypes.SpawnSummon, "创建召唤物", "行为/战斗", 0)]
    public sealed class SpawnSummonActionEditorConfig : ActionEditorConfigBase
    {
        public override string Type => TriggerActionTypes.SpawnSummon;

        [LabelText("模板 ID（可选）")]
        public int TemplateId;

        [LabelText("启用覆盖")]
        public bool EnableOverrides;

        [LabelText("召唤物 ID")]
        public int SummonId;

        [LabelText("目标模式")]
        public SpawnSummonTargetMode TargetMode = SpawnSummonTargetMode.ExplicitTarget;

        [LabelText("位置模式")]
        public SpawnSummonPositionMode PositionMode = SpawnSummonPositionMode.Caster;

        [LabelText("朝向模式")]
        public SpawnSummonRotationMode RotationMode = SpawnSummonRotationMode.Caster;

        [LabelText("所有者键模式")]
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

        [LabelText("偏航角偏移")]
        public float YawOffsetDeg;

        [LabelText("随机种子")]
        public int RandomSeed;

        [LabelText("随机半径最小")]
        public float RandomRadiusMin;

        [LabelText("随机半径最大")]
        public float RandomRadiusMax;

        [LabelText("网格行数")]
        public int GridRows;

        [LabelText("网格列数")]
        public int GridCols;

        [LabelText("网格 X 轴间距")]
        public float GridSpacingX;

        [LabelText("网格 Z 轴间距")]
        public float GridSpacingZ;

        [LabelText("每点朝向")]
        public SpawnSummonPerPointRotationMode PerPointRotationMode = SpawnSummonPerPointRotationMode.Inherit;

        [LabelText("每点偏航角偏移")]
        public float PerPointYawOffsetDeg;

        [LabelText("间隔毫秒")]
        public int IntervalMs;

        [LabelText("持续毫秒")]
        public int DurationMs;

        [LabelText("总次数")]
        public int TotalCount;

        [LabelText("施法者键（可选）")]
        public string CasterKey;

        [LabelText("目标键（可选）")]
        public string TargetKey;

        [LabelText("查询模板 ID（可选）")]
        public int QueryTemplateId;

        [LabelText("瞄准位置键（可选）")]
        public string AimPosKey;

        [LabelText("固定位置键（可选）")]
        public string FixedPosKey;

        [LabelText("固定位置回退值")]
        public Vec3 FixedPosFallback;

        protected override string GetTitleSuffix()
        {
            if (TemplateId > 0) return "模板=" + TemplateId;
            return SummonId > 0 ? SummonId.ToString() : null;
        }

        public override ActionConfigBase ToRuntimeConfig()
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
