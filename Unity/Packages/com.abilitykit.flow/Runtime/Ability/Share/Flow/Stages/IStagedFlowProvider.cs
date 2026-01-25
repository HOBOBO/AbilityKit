namespace AbilityKit.Ability.Flow.Stages
{
    public interface IStagedFlowProvider<in TArgs>
    {
        IFlowNode CreateStage(FlowStageKey stage, TArgs args);
    }
}
