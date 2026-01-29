namespace AbilityKit.Ability
{
    /// <summary>
    /// 管线节点接口
    /// </summary>
    public interface IAbilityPipelineNode
    {
        string Id { get; }
        IAbilityPipelineNodeExecuteResult Execute(IAbilityPipelineContext context);
    }
}