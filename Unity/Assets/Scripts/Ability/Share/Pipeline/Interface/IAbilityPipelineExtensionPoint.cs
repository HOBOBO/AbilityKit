namespace AbilityKit.Ability
{
    /// <summary>
    /// 扩展管线执行点
    /// </summary>
    public interface IAbilityPipelineExtensionPoint
    {
        void OnPhaseStart(IAbilityPipelineContext context,IAbilityPipelinePhase phase);
        void OnPhaseComplete(IAbilityPipelineContext context,IAbilityPipelinePhase phase);
    }
}