using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Blocks;
using AbilityKit.Ability.Flow.Stages;
using UnityEngine;

namespace AbilityKit.Starter.Examples
{
    public sealed class StarterLogContributor : IFlowStageContributor<StarterArgs>
    {
        public int Order => 0;

        public bool CanContribute(FlowStageKey stage)
        {
            return stage == StarterStages.ProjectInit || stage == StarterStages.SdkInit || stage == StarterStages.EnterGame;
        }

        public IFlowNode CreateNode(FlowStageKey stage, StarterArgs args)
        {
            return new DoNode(onEnter: _ => Debug.Log($"[Starter] stage={stage}"));
        }
    }
}
