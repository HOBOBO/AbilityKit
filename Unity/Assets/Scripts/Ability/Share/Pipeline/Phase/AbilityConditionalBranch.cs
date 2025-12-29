namespace AbilityKit.Ability
{
    /// <summary>
    /// 条件分支定义
    /// </summary>
    public class AbilityConditionalBranch
    {
        public IAbilityConditionNode Condition { get; }
        public IAbilityPipelinePhase Phase { get; }
    
        public AbilityConditionalBranch(IAbilityConditionNode condition, IAbilityPipelinePhase phase)
        {
            Condition = condition;
            Phase = phase;
        }
    }
}