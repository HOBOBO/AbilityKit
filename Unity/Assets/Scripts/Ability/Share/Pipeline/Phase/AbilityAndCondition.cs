using System.Collections.Generic;

namespace AbilityKit.Ability
{
    /// <summary>
    /// 复合条件节点 - 与。
    /// </summary>
    public class AbilityAndCondition : AbilityConditionNodeBase
    {
        private readonly IAbilityConditionNode[] _conditions;
        
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
            _conditions = conditions.ToArray();
        }
    
        public override bool Evaluate(IAbilityPipelineContext context)
        {
            foreach (var condition in _conditions)
            {
                if (!condition.Evaluate(context))
                    return false;
            }
            return true;
        }
    }
}