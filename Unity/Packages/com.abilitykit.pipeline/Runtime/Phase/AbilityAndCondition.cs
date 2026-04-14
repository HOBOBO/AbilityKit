using System.Collections.Generic;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 复合条件节点 - 与。
    /// </summary>
    public class AbilityAndCondition : AbilityConditionNodeBase
    {
        private readonly IReadOnlyList<IAbilityConditionNode> _conditions;
        
        /// <summary>
        /// 默认持续执行
        /// </summary>
        public override EConditionCheckStrategy CheckStrategy => EConditionCheckStrategy.Continuous;

    
        public AbilityAndCondition(params IAbilityConditionNode[] conditions)
        {
            _conditions = conditions;
        }

        public AbilityAndCondition(List<IAbilityConditionNode> conditions)
        {
            _conditions = conditions;
        }
    
        public override bool Evaluate(IAbilityPipelineContext context)
        {
            for (int i = 0; i < _conditions.Count; i++)
            {
                if (!_conditions[i].Evaluate(context))
                    return false;
            }
            return true;
        }
    }
}
