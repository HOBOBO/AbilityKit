using System;
using AbilityKit.Triggering.Runtime.Plan;

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
            TotalCount = 0;
            QueryTemplateId = 0;
            TargetMode = 0;
            ResultTarget = default;
            ResultCountTarget = default;
        }
    }
}
