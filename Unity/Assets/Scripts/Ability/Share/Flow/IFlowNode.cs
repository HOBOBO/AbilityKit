namespace AbilityKit.Ability.Flow
{
    public interface IFlowNode
    {
        void Enter(FlowContext ctx);
        FlowStatus Tick(FlowContext ctx, float deltaTime);
        void Exit(FlowContext ctx);
        void Interrupt(FlowContext ctx);
    }
}
