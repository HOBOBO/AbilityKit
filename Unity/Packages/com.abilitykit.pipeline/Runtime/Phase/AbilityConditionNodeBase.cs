namespace AbilityKit.Ability
{
    /// <summary>
    /// 基础条件节点实现
    /// </summary>
    public abstract class AbilityConditionNodeBase : IAbilityConditionNode
    {
        public virtual EConditionCheckStrategy CheckStrategy { get; }
    
        public abstract bool Evaluate(IAbilityPipelineContext context);
    }
}