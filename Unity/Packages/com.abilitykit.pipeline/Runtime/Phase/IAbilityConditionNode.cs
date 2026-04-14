namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 条件阶段接口
    /// </summary>
    public interface IAbilityConditionNode
    {
        bool Evaluate(IAbilityPipelineContext context);
        // 条件检测策略
        EConditionCheckStrategy CheckStrategy { get; }
    }
}
