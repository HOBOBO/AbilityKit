namespace AbilityKit.Ability
{
    /// <summary>
    /// 复合条件节点 - 非
    /// </summary>
    public class AbilityNotCondition : AbilityConditionNodeBase
    {
        private readonly IAbilityConditionNode _condition;
        /// <summary>
        /// 默认持续执行
        /// </summary>
        public override EConditionCheckStrategy CheckStrategy => EConditionCheckStrategy.Continuous;
    
        public AbilityNotCondition(IAbilityConditionNode condition)
        {
            _condition = condition;
        }
    
        public override bool Evaluate(IAbilityPipelineContext context)
        {
            if (!_condition.Evaluate(context))
                return true;
            return false;
        }
    }
}