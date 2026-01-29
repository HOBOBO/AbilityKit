namespace AbilityKit.Ability
{
    /// <summary>
    /// 节点执行结果
    /// </summary>
    public interface IAbilityPipelineNodeExecuteResult
    {
        public bool IsCompleted { get; set; }
    }
}