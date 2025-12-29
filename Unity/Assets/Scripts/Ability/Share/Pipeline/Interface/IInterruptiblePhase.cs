namespace AbilityKit.Ability
{
    /// <summary>
    /// 可中断阶段接口
    /// </summary>
    public interface IInterruptiblePhase
    {
        void OnInterrupt(IAbilityPipelineContext context);
    }
}