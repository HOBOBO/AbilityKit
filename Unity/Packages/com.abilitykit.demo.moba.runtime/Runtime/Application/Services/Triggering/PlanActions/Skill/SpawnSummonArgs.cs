using System;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Demo.Moba.Services.Combat.Magnitude;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public enum SpawnSummonPositionMode
    {
        Caster = 0,
        Target = 1,
        AimPos = 2,
        Fixed = 3,
    }

    public struct SpawnSummonArgs
    {
        public int SummonId;
        public int PositionMode;
        public int RotationMode;
        public float IntervalMs;
        public float DurationMs;
        /// <summary>
        /// 从原 Cast 冻结表读取的持续毫秒参数；与固定 DurationMs 互斥。
        /// </summary>
        public string DurationSkillLevelParameter;
        /// <summary>
        /// 创建前统一求值的出生属性，应用在模板与继承之后。
        /// </summary>
        public MobaSummonBirthAttributeSpec[] BirthAttributes;
        public int TotalCount;
        public int QueryTemplateId;
        public int TargetMode;
        public BlackboardWriteTarget ResultTarget;
        public BlackboardWriteTarget ResultCountTarget;

        public SpawnSummonArgs(int summonId)
        {
            SummonId = summonId;
            PositionMode = 0;
            RotationMode = 0;
            IntervalMs = 0;
            DurationMs = 0;
            DurationSkillLevelParameter = null;
            BirthAttributes = null;
            TotalCount = 0;
            QueryTemplateId = 0;
            TargetMode = 0;
            ResultTarget = default;
            ResultCountTarget = default;
        }
    }

    /// <summary>
    /// 出生时一次求值的属性覆盖；实例只保存求值结果。
    /// </summary>
    public readonly struct MobaSummonBirthAttributeSpec
    {
        /// <summary>
        /// 要覆盖的正式战斗属性。
        /// </summary>
        public readonly BattleAttributeType Attribute;
        /// <summary>
        /// 使用原 Cast 上下文求值的出生数值公式。
        /// </summary>
        public readonly MobaEffectMagnitudeSpec Magnitude;
        /// <summary>
        /// 声明一次出生属性覆盖，不持有召唤实例或额外可变状态。
        /// </summary>
        public MobaSummonBirthAttributeSpec(BattleAttributeType attribute, in MobaEffectMagnitudeSpec magnitude)
        { Attribute = attribute; Magnitude = magnitude; }
    }
}
