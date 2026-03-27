using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityHFSM.Graph.Conditions
{
    /// <summary>
    /// 时间经过条件 - 当节点已经过指定时间后触发
    /// </summary>
    [Serializable]
    public class HfsmTimeElapsedCondition : HfsmTransitionCondition
    {
        /// <summary>
        /// 源状态节点ID
        /// </summary>
        public string SourceNodeId;

        /// <summary>
        /// 需要经过的时间（秒）
        /// </summary>
        public float Duration = 1f;

        /// <summary>
        /// 比较操作符
        /// </summary>
        public HfsmCompareOperator Operator = HfsmCompareOperator.GreaterOrEqual;

        public override string TypeName => "TimeElapsed";

        public override string DisplayName => "Time Elapsed";

        public override string GetDescription()
        {
            string op = Operator switch
            {
                HfsmCompareOperator.GreaterThan => ">",
                HfsmCompareOperator.GreaterOrEqual => ">=",
                HfsmCompareOperator.LessThan => "<",
                HfsmCompareOperator.LessOrEqual => "<=",
                _ => ">="
            };
            return $"Time {op} {Duration:F2}s";
        }

        public override bool Evaluate(IHfsmEvaluationContext context)
        {
            if (string.IsNullOrEmpty(SourceNodeId))
                return false;

            float elapsed = context.GetNodeElapsedTime(SourceNodeId);
            return Compare(elapsed, Duration);
        }

        private bool Compare(float left, float right)
        {
            return Operator switch
            {
                HfsmCompareOperator.GreaterThan => left > right,
                HfsmCompareOperator.GreaterOrEqual => left >= right,
                HfsmCompareOperator.LessThan => left < right,
                HfsmCompareOperator.LessOrEqual => left <= right,
                HfsmCompareOperator.Equal => Mathf.Approximately(left, right),
                HfsmCompareOperator.NotEqual => !Mathf.Approximately(left, right),
                _ => left >= right
            };
        }

        public override HfsmTransitionCondition Clone()
        {
            return new HfsmTimeElapsedCondition
            {
                SourceNodeId = SourceNodeId,
                Duration = Duration,
                Operator = Operator
            };
        }

        public override string[] GetRequiredParameters()
        {
            return Array.Empty<string>();
        }

        public override void SetFromConfig(Dictionary<string, object> config)
        {
            if (config.TryGetValue("SourceNodeId", out var id))
                SourceNodeId = id as string ?? "";

            if (config.TryGetValue("Duration", out var dur))
                Duration = dur is float f ? f : Convert.ToSingle(dur);

            if (config.TryGetValue("Operator", out var op))
                Operator = (HfsmCompareOperator)(int)op;
        }

        public override Dictionary<string, object> ToConfig()
        {
            return new Dictionary<string, object>
            {
                ["SourceNodeId"] = SourceNodeId,
                ["Duration"] = Duration,
                ["Operator"] = (int)Operator
            };
        }
    }
}
