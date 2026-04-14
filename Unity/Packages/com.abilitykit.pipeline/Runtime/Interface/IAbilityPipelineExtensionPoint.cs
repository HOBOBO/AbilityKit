namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 扩展管线执行点
    /// </summary>
    public interface IAbilityPipelineExtensionPoint<TCtx>
    {
        void OnPhaseStart(TCtx context, IAbilityPipelinePhase<TCtx> phase);
        void OnPhaseComplete(TCtx context, IAbilityPipelinePhase<TCtx> phase);
    }
}
