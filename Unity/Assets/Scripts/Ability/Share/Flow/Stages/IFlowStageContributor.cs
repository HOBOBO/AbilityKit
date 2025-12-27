namespace AbilityKit.Ability.Flow.Stages
{
    public interface IFlowStageContributor<in TArgs>
    {
        int Order { get; }
        bool CanContribute(FlowStageKey stage);
        IFlowNode CreateNode(FlowStageKey stage, TArgs args);
    }
}
