using System;
using System.Collections.Generic;

namespace AbilityKit.HFSM.Graph.Conditions
{
    /// <summary>
    /// 行为完成条件 - 当状态的所有行为（entry/logic/exit）执行完毕后自动触发切换
    /// </summary>
    [Serializable]
    public class BehaviorCompleteCondition : TransitionCondition
    {
        /// <summary>
        /// 源状态节点ID
        /// </summary>
        public string SourceNodeId;

        public override string TypeName => "BehaviorComplete";

        public override string DisplayName => "Behavior Complete";

        public override string GetDescription()
        {
            return "All behaviors completed";
        }

        public override bool Evaluate(IEvaluationContext context)
        {
            if (string.IsNullOrEmpty(SourceNodeId))
                return false;
            return context.HasAllActionsCompleted(SourceNodeId);
        }

        public override TransitionCondition Clone()
        {
            return new BehaviorCompleteCondition
            {
                SourceNodeId = SourceNodeId
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
        }

        public override Dictionary<string, object> ToConfig()
        {
            return new Dictionary<string, object>
            {
                ["SourceNodeId"] = SourceNodeId
            };
        }
    }
}
