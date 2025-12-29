using System.Collections.Generic;

namespace AbilityKit.Ability
{
    /// <summary>
    /// 复合条件节点 - 或
    /// </summary>
    public class AbilityOrCondition : AbilityConditionNodeBase
    {
        private readonly IAbilityConditionNode[] _conditions;
        /// <summary>
        /// 默认持续执行
        /// </summary>
        public override EConditionCheckStrategy CheckStrategy => EConditionCheckStrategy.Continuous;
    
        public AbilityOrCondition(params IAbilityConditionNode[] conditions)
        {
            _conditions = conditions;
        }

        public AbilityOrCondition(List<IAbilityConditionNode> conditions)
        {
            _conditions = conditions.ToArray();
        }
    
        public override bool Evaluate(IAbilityPipelineContext context)
        {
            foreach (var condition in _conditions)
            {
                if (condition.Evaluate(context))
                    return true;
            }
            return false;
        }
    }
}