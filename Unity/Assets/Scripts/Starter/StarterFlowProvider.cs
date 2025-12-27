using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Blocks;
using AbilityKit.Ability.Flow.Stages;
using AbilityKit.Starter.Nodes;

namespace AbilityKit.Starter
{
    public sealed class StarterFlowProvider : IStagedFlowProvider<StarterArgs>
    {
        public IFlowNode CreateStage(FlowStageKey stage, StarterArgs args)
        {
            if (stage == StarterStages.ProjectInit)
            {
                return new DoNode();
            }

            if (stage == StarterStages.SdkInit)
            {
                return new DoNode();
            }

            if (stage == StarterStages.EnterGame)
            {
                return new DoNode(onEnter: _ => args.OnEnterGame?.Invoke());
            }

            return null;
        }
    }
}
