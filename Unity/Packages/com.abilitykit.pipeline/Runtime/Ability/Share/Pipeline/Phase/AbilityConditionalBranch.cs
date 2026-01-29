namespace AbilityKit.Ability
{
    /// <summary>
    /// 条件分支定义
    /// </summary>
    public class AbilityConditionalBranch<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        public IAbilityConditionNode Condition { get; }
        public IAbilityPipelinePhase<TCtx> Phase { get; }
    
        public AbilityConditionalBranch(IAbilityConditionNode condition, IAbilityPipelinePhase<TCtx> phase)
        {
            Condition = condition;
            Phase = phase;
        }
    }
}